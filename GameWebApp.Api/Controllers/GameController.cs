using GameWebApp.Shared;
using GameWebApp.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameWebApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    private readonly QuestionService questionService;

    public GameController(QuestionService questionService)
    {
        this.questionService = questionService;
    }

    [HttpPost("start")]
    public ActionResult<GameSessionDto> Start(StartGameRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Player1Name) || string.IsNullOrWhiteSpace(request.Player2Name))
        {
            return BadRequest("Please enter names for both groups.");
        }

        var session = new GameSessionDto
        {
            Player1 = new PlayerDto
            {
                Name = request.Player1Name.Trim(),
                Score = 0,
                Position = 0
            },
            Player2 = new PlayerDto
            {
                Name = request.Player2Name.Trim(),
                Score = 0,
                Position = 0
            },
            Message = "The game has started."
        };

        return Ok(session);
    }

    [HttpPost("roll-start-dice")]
    public ActionResult<RollDiceResultDto> RollStartDice(RollDiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Player1Name) || string.IsNullOrWhiteSpace(request.Player2Name))
        {
            return BadRequest("Please start the game with two group names first.");
        }

        var player1Name = request.Player1Name.Trim();
        var player2Name = request.Player2Name.Trim();
        var player1DiceValue = Random.Shared.Next(1, 7);
        var player2DiceValue = Random.Shared.Next(1, 7);

        if (player1DiceValue == player2DiceValue)
        {
            return Ok(new RollDiceResultDto
            {
                Player1DiceValue = player1DiceValue,
                Player2DiceValue = player2DiceValue,
                IsDraw = true,
                Message = "The dice values are the same. Please roll again."
            });
        }

        var firstPlayerName = player1DiceValue > player2DiceValue
            ? player1Name
            : player2Name;

        return Ok(new RollDiceResultDto
        {
            Player1DiceValue = player1DiceValue,
            Player2DiceValue = player2DiceValue,
            FirstPlayerName = firstPlayerName,
            IsDraw = false,
            Message = $"{firstPlayerName} will play first."
        });
    }

    [HttpGet("question")]
    public ActionResult<QuestionDto> GetQuestion()
    {
        var question = questionService.GetRandomQuestion();

        if (question is null)
        {
            return NotFound("No more questions available.");
        }

        return Ok(question);
    }

    [HttpPost("question")]
    public ActionResult<QuestionDto> GetQuestion(LoadQuestionRequest request)
    {
        var question = questionService.GetRandomQuestion(request.UsedQuestionIds);

        if (question is null)
        {
            return NotFound("No more questions available.");
        }

        return Ok(question);
    }

    [HttpPost("submit-answer")]
    public ActionResult<SubmitAnswerResultDto> SubmitAnswer(SubmitAnswerRequest request)
    {
        if (request.QuestionId <= 0)
        {
            return BadRequest("Please load a question first.");
        }

        if (request.SelectedAnswerNumber <= 0)
        {
            return BadRequest("Please select an answer.");
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPlayerName))
        {
            return BadRequest("Current player is missing.");
        }

        if (request.MovementDiceValue < 1 || request.MovementDiceValue > 6)
        {
            return BadRequest("Please roll the movement dice before submitting an answer.");
        }

        return Ok(questionService.CheckAnswer(request));
    }
}
