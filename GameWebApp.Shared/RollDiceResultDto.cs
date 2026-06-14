namespace GameWebApp.Shared;

public class RollDiceResultDto
{
    public int Player1DiceValue { get; set; }

    public int Player2DiceValue { get; set; }

    public string FirstPlayerName { get; set; } = string.Empty;

    public bool IsDraw { get; set; }

    public string Message { get; set; } = string.Empty;
}
