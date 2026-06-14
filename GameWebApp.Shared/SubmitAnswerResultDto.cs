namespace GameWebApp.Shared;

public class SubmitAnswerResultDto
{
    public bool IsCorrect { get; set; }

    public string Message { get; set; } = string.Empty;

    public int UpdatedScore { get; set; }

    public int UpdatedPosition { get; set; }

    public bool IsWinner { get; set; }
}
