using Microsoft.AspNetCore.SignalR;
using GameWebApp.Api.Services;
using GameWebApp.Shared;

namespace GameWebApp.Api.Hubs;

public class GameHub : Hub
{
    private readonly MultiplayerGameSessionService multiplayerGameSessionService;
    private readonly QuestionService questionService;

    public GameHub(
        MultiplayerGameSessionService multiplayerGameSessionService,
        QuestionService questionService)
    {
        this.multiplayerGameSessionService = multiplayerGameSessionService;
        this.questionService = questionService;
    }

    public async Task SendTestMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveTestMessage", message);
    }

    public async Task CreateGame(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            await Clients.Caller.SendAsync("MultiplayerError", "Player name is required.");
            return;
        }

        var session = multiplayerGameSessionService.CreateGame(playerName, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, session.GameCode);
        await Clients.Caller.SendAsync("GameCreated", session.GameCode);
        await Clients.Caller.SendAsync("MultiplayerStatus", "Waiting for Player 2");
    }

    public async Task JoinGame(string gameCode, string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            await Clients.Caller.SendAsync("MultiplayerError", "Player name is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(gameCode))
        {
            await Clients.Caller.SendAsync("MultiplayerError", "Game code is required.");
            return;
        }

        var joined = multiplayerGameSessionService.TryJoinGame(
            gameCode,
            playerName,
            Context.ConnectionId,
            out var session,
            out var isReconnect,
            out var errorMessage);

        if (!joined || session is null)
        {
            await Clients.Caller.SendAsync("MultiplayerError", errorMessage);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, session.GameCode);

        if (isReconnect)
        {
            await Clients.Group(session.GameCode).SendAsync(
                "MultiplayerPlayerReconnected",
                session.GameCode,
                playerName.Trim(),
                $"{playerName.Trim()} reconnected.");
            await SendSessionRestoredToCaller(session);
            return;
        }

        await Clients.Caller.SendAsync("MultiplayerStatus", "Joined game");
        await Clients.Group(session.GameCode).SendAsync(
            "GameReady",
            session.GameCode,
            session.Player1Name,
            session.Player2Name);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var disconnected = multiplayerGameSessionService.TryMarkDisconnected(
            Context.ConnectionId,
            out var session,
            out var playerName);

        if (disconnected && session is not null)
        {
            await Clients.Group(session.GameCode).SendAsync(
                "MultiplayerPlayerDisconnected",
                session.GameCode,
                playerName,
                $"{playerName} disconnected.");
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task StartMultiplayerGame(string gameCode)
    {
        if (string.IsNullOrWhiteSpace(gameCode))
        {
            await Clients.Caller.SendAsync("MultiplayerError", "Game code is required.");
            return;
        }

        var started = multiplayerGameSessionService.TryStartGame(
            gameCode,
            out var session,
            out var errorMessage);

        if (!started || session is null)
        {
            await Clients.Caller.SendAsync("MultiplayerError", errorMessage);
            return;
        }

        await Clients.Group(session.GameCode).SendAsync(
            "MultiplayerGameStarted",
            session.GameCode,
            session.Player1Name,
            session.Player2Name,
            session.Player1Score,
            session.Player2Score,
            session.Player1Position,
            session.Player2Position);
    }

    public async Task RollFirstPlayerDice(string gameCode)
    {
        if (string.IsNullOrWhiteSpace(gameCode))
        {
            await Clients.Caller.SendAsync("MultiplayerError", "Game code is required.");
            return;
        }

        var rolled = multiplayerGameSessionService.TryRollFirstPlayerDice(
            gameCode,
            out var session,
            out var isDraw,
            out var rollMessage,
            out var errorMessage);

        if (!rolled || session is null)
        {
            await Clients.Caller.SendAsync("MultiplayerError", errorMessage);
            return;
        }

        await Clients.Group(session.GameCode).SendAsync(
            "MultiplayerFirstDiceRolled",
            session.GameCode,
            session.Player1FirstDiceValue,
            session.Player2FirstDiceValue,
            session.FirstPlayerName,
            session.CurrentTurnPlayerName,
            isDraw,
            rollMessage);
    }

    public async Task RollMultiplayerMovementDice(string gameCode)
    {
        if (string.IsNullOrWhiteSpace(gameCode))
        {
            await Clients.Caller.SendAsync("MultiplayerError", "Game code is required.");
            return;
        }

        var rolled = multiplayerGameSessionService.TryRollMovementDice(
            gameCode,
            Context.ConnectionId,
            questionService,
            out var session,
            out var question,
            out var errorMessage);

        if (!rolled || session is null || question is null)
        {
            await Clients.Caller.SendAsync("MultiplayerError", errorMessage);
            return;
        }

        await Clients.Group(session.GameCode).SendAsync(
            "MultiplayerMovementDiceRolled",
            session.GameCode,
            session.CurrentTurnPlayerName,
            session.CurrentMovementDiceValue);

        await Clients.Group(session.GameCode).SendAsync(
            "MultiplayerQuestionLoaded",
            session.GameCode,
            session.CurrentTurnPlayerName,
            session.CurrentMovementDiceValue,
            question,
            session.QuestionStartedAt,
            session.QuestionTimeLimitSeconds);
    }

    public async Task SubmitMultiplayerAnswer(string gameCode, int questionId, int selectedAnswerNumber)
    {
        if (string.IsNullOrWhiteSpace(gameCode))
        {
            await Clients.Caller.SendAsync("MultiplayerError", "Game code is required.");
            return;
        }

        var submitted = multiplayerGameSessionService.TrySubmitAnswer(
            gameCode,
            Context.ConnectionId,
            questionId,
            selectedAnswerNumber,
            questionService,
            out var session,
            out var answerResult,
            out var answeringPlayerName,
            out var errorMessage);

        if (!submitted || session is null || answerResult is null)
        {
            await Clients.Caller.SendAsync("MultiplayerError", errorMessage);
            return;
        }

        await Clients.Group(session.GameCode).SendAsync(
            "MultiplayerAnswerSubmitted",
            session.GameCode,
            answeringPlayerName,
            answerResult);

        await Clients.Group(session.GameCode).SendAsync(
            "MultiplayerGameStateUpdated",
            session.GameCode,
            session.Player1Score,
            session.Player2Score,
            session.Player1Position,
            session.Player2Position,
            session.CurrentTurnPlayerName,
            session.WinnerName);
    }

    public async Task MultiplayerQuestionTimedOut(string gameCode, int questionId)
    {
        if (string.IsNullOrWhiteSpace(gameCode))
        {
            await Clients.Caller.SendAsync("MultiplayerError", "Game code is required.");
            return;
        }

        var timedOut = multiplayerGameSessionService.TryProcessQuestionTimeout(
            gameCode,
            questionId,
            out var session,
            out var timeoutResult,
            out var timedOutPlayerName,
            out var errorMessage);

        if (!timedOut || session is null || timeoutResult is null)
        {
            if (errorMessage == "The question is no longer active.")
            {
                return;
            }

            await Clients.Caller.SendAsync("MultiplayerError", errorMessage);
            return;
        }

        await Clients.Group(session.GameCode).SendAsync(
            "MultiplayerAnswerSubmitted",
            session.GameCode,
            timedOutPlayerName,
            timeoutResult);

        await Clients.Group(session.GameCode).SendAsync(
            "MultiplayerGameStateUpdated",
            session.GameCode,
            session.Player1Score,
            session.Player2Score,
            session.Player1Position,
            session.Player2Position,
            session.CurrentTurnPlayerName,
            session.WinnerName);
    }

    public async Task ResetMultiplayerGame(string gameCode)
    {
        if (string.IsNullOrWhiteSpace(gameCode))
        {
            await Clients.Caller.SendAsync("MultiplayerError", "Game code is required.");
            return;
        }

        var reset = multiplayerGameSessionService.TryResetGame(
            gameCode,
            out var session,
            out var errorMessage);

        if (!reset || session is null)
        {
            await Clients.Caller.SendAsync("MultiplayerError", errorMessage);
            return;
        }

        await Clients.Group(session.GameCode).SendAsync(
            "MultiplayerGameReset",
            session.GameCode,
            session.Player1Name,
            session.Player2Name,
            session.Player1Score,
            session.Player2Score,
            session.Player1Position,
            session.Player2Position);
    }

    private async Task SendSessionRestoredToCaller(GameWebApp.Api.Models.GameSession session)
    {
        var restoredState = new MultiplayerSessionStateDto
        {
            GameCode = session.GameCode,
            Player1Name = session.Player1Name,
            Player2Name = session.Player2Name,
            Player1Score = session.Player1Score,
            Player2Score = session.Player2Score,
            Player1Position = session.Player1Position,
            Player2Position = session.Player2Position,
            IsGameStarted = session.IsGameStarted,
            Player1FirstDiceValue = session.Player1FirstDiceValue,
            Player2FirstDiceValue = session.Player2FirstDiceValue,
            FirstPlayerName = session.FirstPlayerName,
            CurrentTurnPlayerName = session.CurrentTurnPlayerName,
            CurrentMovementDiceValue = session.CurrentMovementDiceValue,
            WinnerName = session.WinnerName,
            IsQuestionActive = session.IsQuestionActive,
            CurrentQuestionId = session.CurrentQuestionId,
            CurrentQuestion = session.CurrentQuestion,
            QuestionStartedAt = session.QuestionStartedAt,
            QuestionTimeLimitSeconds = session.QuestionTimeLimitSeconds
        };

        await Clients.Caller.SendAsync("MultiplayerSessionRestored", restoredState);
    }
}
