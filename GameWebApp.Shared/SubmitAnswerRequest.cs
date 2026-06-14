namespace GameWebApp.Shared;

public class SubmitAnswerRequest
{
    public int QuestionId { get; set; }

    public int SelectedAnswerNumber { get; set; }

    public string CurrentPlayerName { get; set; } = string.Empty;

    public int CurrentPlayerPosition { get; set; }

    public int CurrentPlayerScore { get; set; }

    public int MovementDiceValue { get; set; }
}
