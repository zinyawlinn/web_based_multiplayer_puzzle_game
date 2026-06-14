using GameWebApp.Shared;

namespace GameWebApp.Api.Models;

public class GameSession
{
    public string GameCode { get; set; } = string.Empty;

    public string Player1Name { get; set; } = string.Empty;

    public string Player2Name { get; set; } = string.Empty;

    public string Player1ConnectionId { get; set; } = string.Empty;

    public string Player2ConnectionId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool Player1Connected { get; set; }

    public bool Player2Connected { get; set; }

    public DateTime? LastDisconnectedAt { get; set; }

    public int Player1Score { get; set; }

    public int Player2Score { get; set; }

    public int Player1Position { get; set; }

    public int Player2Position { get; set; }

    public bool IsGameStarted { get; set; }

    public int Player1FirstDiceValue { get; set; }

    public int Player2FirstDiceValue { get; set; }

    public string FirstPlayerName { get; set; } = string.Empty;

    public string CurrentTurnPlayerName { get; set; } = string.Empty;

    public int CurrentMovementDiceValue { get; set; }

    public int CurrentQuestionId { get; set; }

    public QuestionDto? CurrentQuestion { get; set; }

    public List<int> UsedQuestionIds { get; set; } = new();

    public bool IsQuestionActive { get; set; }

    public string WinnerName { get; set; } = string.Empty;

    public DateTime QuestionStartedAt { get; set; }

    public int QuestionTimeLimitSeconds { get; set; } = 30;
}
