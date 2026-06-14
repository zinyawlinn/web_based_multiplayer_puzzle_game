using System.Collections.Concurrent;
using GameWebApp.Api.Models;
using GameWebApp.Shared;

namespace GameWebApp.Api.Services;

public class MultiplayerGameSessionService
{
    private const string CodeCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int DefaultQuestionTimeLimitSeconds = 30;
    private readonly ConcurrentDictionary<string, GameSession> sessions = new();

    public GameSession CreateGame(string playerName, string connectionId)
    {
        var session = new GameSession
        {
            GameCode = CreateGameCode(),
            Player1Name = playerName.Trim(),
            Player1ConnectionId = connectionId,
            Player1Connected = true,
            CreatedAt = DateTime.UtcNow
        };

        sessions[session.GameCode] = session;
        return session;
    }

    public bool TryJoinGame(
        string gameCode,
        string playerName,
        string connectionId,
        out GameSession? session,
        out bool isReconnect,
        out string errorMessage)
    {
        session = null;
        isReconnect = false;
        errorMessage = string.Empty;
        var cleanGameCode = gameCode.Trim().ToUpperInvariant();
        var cleanPlayerName = playerName.Trim();

        if (!sessions.TryGetValue(cleanGameCode, out var foundSession))
        {
            errorMessage = "Game code not found.";
            return false;
        }

        if (string.Equals(foundSession.Player1Name, cleanPlayerName, StringComparison.OrdinalIgnoreCase))
        {
            foundSession.Player1Name = cleanPlayerName;
            foundSession.Player1ConnectionId = connectionId;
            foundSession.Player1Connected = true;
            foundSession.LastDisconnectedAt = null;
            session = foundSession;
            isReconnect = true;
            return true;
        }

        if (string.Equals(foundSession.Player2Name, cleanPlayerName, StringComparison.OrdinalIgnoreCase))
        {
            foundSession.Player2Name = cleanPlayerName;
            foundSession.Player2ConnectionId = connectionId;
            foundSession.Player2Connected = true;
            foundSession.LastDisconnectedAt = null;
            session = foundSession;
            isReconnect = true;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(foundSession.Player2Name))
        {
            errorMessage = "Game is already full.";
            return false;
        }

        if (foundSession.Player1ConnectionId == connectionId)
        {
            errorMessage = "A second browser or player must join this game.";
            return false;
        }

        foundSession.Player2Name = cleanPlayerName;
        foundSession.Player2ConnectionId = connectionId;
        foundSession.Player2Connected = true;
        session = foundSession;
        return true;
    }

    public bool TryMarkDisconnected(string connectionId, out GameSession? session, out string playerName)
    {
        session = null;
        playerName = string.Empty;

        foreach (var foundSession in sessions.Values)
        {
            if (foundSession.Player1ConnectionId == connectionId)
            {
                foundSession.Player1Connected = false;
                foundSession.Player1ConnectionId = string.Empty;
                foundSession.LastDisconnectedAt = DateTime.UtcNow;
                session = foundSession;
                playerName = foundSession.Player1Name;
                return true;
            }

            if (foundSession.Player2ConnectionId == connectionId)
            {
                foundSession.Player2Connected = false;
                foundSession.Player2ConnectionId = string.Empty;
                foundSession.LastDisconnectedAt = DateTime.UtcNow;
                session = foundSession;
                playerName = foundSession.Player2Name;
                return true;
            }
        }

        return false;
    }

    public bool TryStartGame(string gameCode, out GameSession? session, out string errorMessage)
    {
        session = null;
        errorMessage = string.Empty;
        var cleanGameCode = gameCode.Trim().ToUpperInvariant();

        if (!sessions.TryGetValue(cleanGameCode, out var foundSession))
        {
            errorMessage = "Game code not found.";
            return false;
        }

        if (!foundSession.Player1Connected || !foundSession.Player2Connected)
        {
            errorMessage = "Both players must join before starting the game.";
            return false;
        }

        foundSession.Player1Score = 0;
        foundSession.Player2Score = 0;
        foundSession.Player1Position = 0;
        foundSession.Player2Position = 0;
        foundSession.IsGameStarted = true;
        foundSession.Player1FirstDiceValue = 0;
        foundSession.Player2FirstDiceValue = 0;
        foundSession.FirstPlayerName = string.Empty;
        foundSession.CurrentTurnPlayerName = string.Empty;
        foundSession.CurrentMovementDiceValue = 0;
        foundSession.CurrentQuestionId = 0;
        foundSession.CurrentQuestion = null;
        foundSession.UsedQuestionIds.Clear();
        foundSession.IsQuestionActive = false;
        foundSession.WinnerName = string.Empty;
        foundSession.QuestionStartedAt = default;
        foundSession.QuestionTimeLimitSeconds = DefaultQuestionTimeLimitSeconds;

        session = foundSession;
        return true;
    }

    public bool TryRollFirstPlayerDice(
        string gameCode,
        out GameSession? session,
        out bool isDraw,
        out string rollMessage,
        out string errorMessage)
    {
        session = null;
        isDraw = false;
        rollMessage = string.Empty;
        errorMessage = string.Empty;
        var cleanGameCode = gameCode.Trim().ToUpperInvariant();

        if (!sessions.TryGetValue(cleanGameCode, out var foundSession))
        {
            errorMessage = "Game code not found.";
            return false;
        }

        if (!foundSession.IsGameStarted)
        {
            errorMessage = "Start the multiplayer game before rolling dice.";
            return false;
        }

        foundSession.Player1FirstDiceValue = Random.Shared.Next(1, 7);
        foundSession.Player2FirstDiceValue = Random.Shared.Next(1, 7);

        if (foundSession.Player1FirstDiceValue == foundSession.Player2FirstDiceValue)
        {
            foundSession.FirstPlayerName = string.Empty;
            foundSession.CurrentTurnPlayerName = string.Empty;
            isDraw = true;
            rollMessage = "The dice values are the same. Please roll again.";
            session = foundSession;
            return true;
        }

        foundSession.FirstPlayerName = foundSession.Player1FirstDiceValue > foundSession.Player2FirstDiceValue
            ? foundSession.Player1Name
            : foundSession.Player2Name;
        foundSession.CurrentTurnPlayerName = foundSession.FirstPlayerName;

        rollMessage = $"{foundSession.FirstPlayerName} will play first.";
        session = foundSession;
        return true;
    }

    public bool TryRollMovementDice(
        string gameCode,
        string connectionId,
        QuestionService questionService,
        out GameSession? session,
        out QuestionDto? question,
        out string errorMessage)
    {
        session = null;
        question = null;
        errorMessage = string.Empty;

        if (!TryGetPlayableSession(gameCode, out var foundSession, out errorMessage))
        {
            return false;
        }

        if (!IsCurrentPlayerConnection(foundSession, connectionId))
        {
            errorMessage = $"Waiting for {foundSession.CurrentTurnPlayerName} to roll.";
            return false;
        }

        if (foundSession.IsQuestionActive)
        {
            errorMessage = $"{foundSession.CurrentTurnPlayerName} is already answering a question.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(foundSession.WinnerName))
        {
            errorMessage = "The game is already over.";
            return false;
        }

        question = questionService.GetRandomQuestion(foundSession.UsedQuestionIds);

        if (question is null)
        {
            errorMessage = "No more questions available.";
            return false;
        }

        foundSession.CurrentMovementDiceValue = Random.Shared.Next(1, 7);
        foundSession.CurrentQuestionId = question.QuestionId;
        foundSession.CurrentQuestion = question;
        foundSession.UsedQuestionIds.Add(question.QuestionId);
        foundSession.IsQuestionActive = true;
        foundSession.QuestionStartedAt = DateTime.UtcNow;
        foundSession.QuestionTimeLimitSeconds = DefaultQuestionTimeLimitSeconds;

        session = foundSession;
        return true;
    }

    public bool TrySubmitAnswer(
        string gameCode,
        string connectionId,
        int questionId,
        int selectedAnswerNumber,
        QuestionService questionService,
        out GameSession? session,
        out SubmitAnswerResultDto? answerResult,
        out string answeringPlayerName,
        out string errorMessage)
    {
        session = null;
        answerResult = null;
        answeringPlayerName = string.Empty;
        errorMessage = string.Empty;

        if (!TryGetPlayableSession(gameCode, out var foundSession, out errorMessage))
        {
            return false;
        }

        if (!IsCurrentPlayerConnection(foundSession, connectionId))
        {
            errorMessage = $"Waiting for {foundSession.CurrentTurnPlayerName} to answer.";
            return false;
        }

        if (!foundSession.IsQuestionActive || foundSession.CurrentQuestionId != questionId)
        {
            errorMessage = "There is no active question for this turn.";
            return false;
        }

        if (IsQuestionTimeExpired(foundSession))
        {
            answeringPlayerName = foundSession.CurrentTurnPlayerName;
            answerResult = ProcessQuestionTimeout(foundSession);
            session = foundSession;
            return true;
        }

        if (selectedAnswerNumber < 0)
        {
            errorMessage = "Please select an answer.";
            return false;
        }

        answeringPlayerName = foundSession.CurrentTurnPlayerName;
        var isPlayer1Turn = foundSession.CurrentTurnPlayerName == foundSession.Player1Name;
        var currentScore = isPlayer1Turn ? foundSession.Player1Score : foundSession.Player2Score;
        var currentPosition = isPlayer1Turn ? foundSession.Player1Position : foundSession.Player2Position;

        var submitRequest = new SubmitAnswerRequest
        {
            QuestionId = questionId,
            SelectedAnswerNumber = selectedAnswerNumber,
            CurrentPlayerName = answeringPlayerName,
            CurrentPlayerPosition = currentPosition,
            CurrentPlayerScore = currentScore,
            MovementDiceValue = foundSession.CurrentMovementDiceValue
        };

        answerResult = questionService.CheckAnswer(submitRequest);

        if (selectedAnswerNumber == 0)
        {
            answerResult.Message = "Time is up";
        }

        if (isPlayer1Turn)
        {
            foundSession.Player1Score = answerResult.UpdatedScore;
            foundSession.Player1Position = answerResult.UpdatedPosition;
        }
        else
        {
            foundSession.Player2Score = answerResult.UpdatedScore;
            foundSession.Player2Position = answerResult.UpdatedPosition;
        }

        if (answerResult.IsWinner)
        {
            foundSession.WinnerName = answeringPlayerName;
        }
        else
        {
            foundSession.CurrentTurnPlayerName = isPlayer1Turn
                ? foundSession.Player2Name
                : foundSession.Player1Name;
        }

        foundSession.CurrentMovementDiceValue = 0;
        foundSession.CurrentQuestionId = 0;
        foundSession.CurrentQuestion = null;
        foundSession.IsQuestionActive = false;
        foundSession.QuestionStartedAt = default;

        session = foundSession;
        return true;
    }

    public bool TryProcessQuestionTimeout(
        string gameCode,
        int questionId,
        out GameSession? session,
        out SubmitAnswerResultDto? timeoutResult,
        out string timedOutPlayerName,
        out string errorMessage)
    {
        session = null;
        timeoutResult = null;
        timedOutPlayerName = string.Empty;
        errorMessage = string.Empty;

        if (!TryGetPlayableSession(gameCode, out var foundSession, out errorMessage))
        {
            return false;
        }

        if (!foundSession.IsQuestionActive || foundSession.CurrentQuestionId != questionId)
        {
            errorMessage = "The question is no longer active.";
            return false;
        }

        if (!IsQuestionTimeExpired(foundSession))
        {
            errorMessage = "The question still has time remaining.";
            return false;
        }

        timedOutPlayerName = foundSession.CurrentTurnPlayerName;
        timeoutResult = ProcessQuestionTimeout(foundSession);
        session = foundSession;
        return true;
    }

    public bool TryResetGame(string gameCode, out GameSession? session, out string errorMessage)
    {
        session = null;
        errorMessage = string.Empty;
        var cleanGameCode = gameCode.Trim().ToUpperInvariant();

        if (!sessions.TryGetValue(cleanGameCode, out var foundSession))
        {
            errorMessage = "Game code not found.";
            return false;
        }

        foundSession.Player1Score = 0;
        foundSession.Player2Score = 0;
        foundSession.Player1Position = 0;
        foundSession.Player2Position = 0;
        foundSession.IsGameStarted = false;
        foundSession.Player1FirstDiceValue = 0;
        foundSession.Player2FirstDiceValue = 0;
        foundSession.FirstPlayerName = string.Empty;
        foundSession.CurrentTurnPlayerName = string.Empty;
        foundSession.CurrentMovementDiceValue = 0;
        foundSession.CurrentQuestionId = 0;
        foundSession.CurrentQuestion = null;
        foundSession.UsedQuestionIds.Clear();
        foundSession.IsQuestionActive = false;
        foundSession.WinnerName = string.Empty;
        foundSession.QuestionStartedAt = default;
        foundSession.QuestionTimeLimitSeconds = DefaultQuestionTimeLimitSeconds;

        session = foundSession;
        return true;
    }

    private static SubmitAnswerResultDto ProcessQuestionTimeout(GameSession session)
    {
        var isPlayer1Turn = session.CurrentTurnPlayerName == session.Player1Name;
        var currentScore = isPlayer1Turn ? session.Player1Score : session.Player2Score;
        var currentPosition = isPlayer1Turn ? session.Player1Position : session.Player2Position;

        var timeoutResult = new SubmitAnswerResultDto
        {
            IsCorrect = false,
            Message = "Time is up",
            UpdatedScore = currentScore,
            UpdatedPosition = currentPosition,
            IsWinner = false
        };

        session.CurrentTurnPlayerName = isPlayer1Turn
            ? session.Player2Name
            : session.Player1Name;
        session.CurrentMovementDiceValue = 0;
        session.CurrentQuestionId = 0;
        session.CurrentQuestion = null;
        session.IsQuestionActive = false;
        session.QuestionStartedAt = default;

        return timeoutResult;
    }

    private static bool IsQuestionTimeExpired(GameSession session)
    {
        if (!session.IsQuestionActive || session.QuestionStartedAt == default)
        {
            return false;
        }

        return DateTime.UtcNow >= session.QuestionStartedAt.AddSeconds(session.QuestionTimeLimitSeconds);
    }

    private bool TryGetPlayableSession(string gameCode, out GameSession session, out string errorMessage)
    {
        session = new GameSession();
        errorMessage = string.Empty;
        var cleanGameCode = gameCode.Trim().ToUpperInvariant();

        if (!sessions.TryGetValue(cleanGameCode, out var foundSession))
        {
            errorMessage = "Game code not found.";
            return false;
        }

        if (!foundSession.IsGameStarted)
        {
            errorMessage = "Start the multiplayer game first.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(foundSession.CurrentTurnPlayerName))
        {
            errorMessage = "Roll dice to decide the first player first.";
            return false;
        }

        session = foundSession;
        return true;
    }

    private static bool IsCurrentPlayerConnection(GameSession session, string connectionId)
    {
        if (session.CurrentTurnPlayerName == session.Player1Name)
        {
            return session.Player1Connected && session.Player1ConnectionId == connectionId;
        }

        if (session.CurrentTurnPlayerName == session.Player2Name)
        {
            return session.Player2Connected && session.Player2ConnectionId == connectionId;
        }

        return false;
    }

    private string CreateGameCode()
    {
        string gameCode;

        do
        {
            var characters = Enumerable.Range(0, 6)
                .Select(index => CodeCharacters[Random.Shared.Next(CodeCharacters.Length)])
                .ToArray();

            gameCode = new string(characters);
        }
        while (sessions.ContainsKey(gameCode));

        return gameCode;
    }
}
