namespace GameWebApp.Shared;

public class GameSessionDto
{
    public PlayerDto Player1 { get; set; } = new();

    public PlayerDto Player2 { get; set; } = new();

    public string Message { get; set; } = string.Empty;
}
