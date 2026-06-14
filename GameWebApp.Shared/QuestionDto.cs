namespace GameWebApp.Shared;

public class QuestionDto
{
    public int QuestionId { get; set; }

    public string Text { get; set; } = string.Empty;

    public List<AnswerOptionDto> Options { get; set; } = new();
}
