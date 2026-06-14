using GameWebApp.Shared;

namespace GameWebApp.Api.Services;

public class QuestionService
{
    private readonly List<QuestionWithAnswer> questions = new();

    public QuestionService(IWebHostEnvironment environment)
    {
        var filePath = Path.Combine(environment.ContentRootPath, "Data", "questions.txt");
        LoadQuestions(filePath);
    }

    public QuestionDto? GetRandomQuestion()
    {
        return GetRandomQuestion([]);
    }

    public QuestionDto? GetRandomQuestion(IEnumerable<int> usedQuestionIds)
    {
        if (questions.Count == 0)
        {
            return null;
        }

        var usedQuestionIdSet = usedQuestionIds.ToHashSet();
        var availableQuestions = questions
            .Where(question => !usedQuestionIdSet.Contains(question.QuestionId))
            .ToList();

        if (availableQuestions.Count == 0)
        {
            return null;
        }

        var question = availableQuestions[Random.Shared.Next(availableQuestions.Count)];

        return new QuestionDto
        {
            QuestionId = question.QuestionId,
            Text = question.Text,
            Options = question.Options
        };
    }

    public SubmitAnswerResultDto CheckAnswer(SubmitAnswerRequest request)
    {
        var question = questions.FirstOrDefault(question => question.QuestionId == request.QuestionId);

        if (question is null)
        {
            return new SubmitAnswerResultDto
            {
                IsCorrect = false,
                Message = "Question not found.",
                UpdatedScore = request.CurrentPlayerScore,
                UpdatedPosition = request.CurrentPlayerPosition,
                IsWinner = false
            };
        }

        var isCorrect = request.SelectedAnswerNumber == question.CorrectAnswerNumber;
        var updatedScore = request.CurrentPlayerScore;
        var updatedPosition = request.CurrentPlayerPosition;

        if (isCorrect)
        {
            updatedScore++;
            updatedPosition += request.MovementDiceValue;

            if (updatedPosition > 44)
            {
                updatedPosition = 44;
            }
        }

        var isWinner = updatedPosition >= 44;

        return new SubmitAnswerResultDto
        {
            IsCorrect = isCorrect,
            Message = isCorrect ? "Correct answer" : "Wrong answer",
            UpdatedScore = updatedScore,
            UpdatedPosition = updatedPosition,
            IsWinner = isWinner
        };
    }

    private void LoadQuestions(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var questionId = 1;

        foreach (var line in File.ReadLines(filePath))
        {
            var parts = line.Split(';', StringSplitOptions.TrimEntries);

            if (parts.Length < 4)
            {
                continue;
            }

            var answerNumberText = parts[^1];

            if (!int.TryParse(answerNumberText, out var correctAnswerNumber))
            {
                continue;
            }

            var optionTexts = parts[1..^1];

            if (optionTexts.Length < 2 || optionTexts.Length > 4)
            {
                continue;
            }

            var options = new List<AnswerOptionDto>();

            for (var index = 0; index < optionTexts.Length; index++)
            {
                options.Add(new AnswerOptionDto
                {
                    Number = index + 1,
                    Text = optionTexts[index]
                });
            }

            questions.Add(new QuestionWithAnswer
            {
                QuestionId = questionId,
                Text = parts[0],
                Options = options,
                CorrectAnswerNumber = correctAnswerNumber
            });

            questionId++;
        }
    }

    private class QuestionWithAnswer
    {
        public int QuestionId { get; set; }

        public string Text { get; set; } = string.Empty;

        public List<AnswerOptionDto> Options { get; set; } = new();

        public int CorrectAnswerNumber { get; set; }
    }
}
