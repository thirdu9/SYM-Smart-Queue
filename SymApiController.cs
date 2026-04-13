using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SymSmartQueue.Data;

namespace SymSmartQueue.Api
{
    // --- Data Transfer Objects (DTOs) for incoming requests ---
    public class GenerateQueueRequest
    {
        public string UserId { get; set; } = string.Empty;
        public List<string> SeedItemIds { get; set; } = new List<string>();
        public float ExplorationWeight { get; set; } = 0.3f;
        public int Limit { get; set; } = 50;
        public string TimeOfDay { get; set; } = "UNKNOWN";
    }

    public class RecordEventRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        
        // Expected to be "COMPLETE" or "SKIP"
        public string Action { get; set; } = string.Empty; 
        public string TimeOfDay { get; set; } = "UNKNOWN";
    }

    // --- The API Controller ---
    [ApiController]
    [Route("SymSmartQueue")]
    [Produces(MediaTypeNames.Application.Json)]
    public class SymApiController : ControllerBase
    {
        private readonly DatabaseManager _dbManager;
        private readonly ILogger<SymApiController> _logger;

        public SymApiController(DatabaseManager dbManager, ILogger<SymApiController> logger)
        {
            _dbManager = dbManager;
            _logger = logger;
        }

        /// <summary>
        /// STANDARD QUEUE: Generates a dynamic queue based on seed tracks and user's taste profile.
        /// Route: POST /SymSmartQueue/Queue/Generate
        /// </summary>
        [HttpPost("Queue/Generate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<List<string>> GenerateSmartQueue([FromBody] GenerateQueueRequest request)
        {
            try
            {
                var queue = _dbManager.GetSmartQueue(
                    request.UserId, 
                    request.SeedItemIds, 
                    request.ExplorationWeight, 
                    request.Limit, 
                    request.TimeOfDay
                );
                return Ok(queue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SYM API] Error generating standard smart queue.");
                return StatusCode(500, "Internal server error generating queue.");
            }
        }

        /// <summary>
        /// MOOD QUEUE: Generates a queue based on the 12 specific heuristic mood parameters and language.
        /// Route: GET /SymSmartQueue/Queue/Mood?mood=workout&language=latn&limit=50
        /// </summary>
        [HttpGet("Queue/Mood")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<List<string>> GetMoodQueue([FromQuery] string mood, [FromQuery] string language = "any", [FromQuery] int limit = 50)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mood))
                {
                    return BadRequest("Mood parameter is required.");
                }

                var queue = _dbManager.GetMoodQueue(mood, language, limit);
                return Ok(queue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SYM API] Error generating mood queue for {Mood}.", mood);
                return StatusCode(500, "Internal server error generating mood queue.");
            }
        }

        /// <summary>
        /// GENRE QUEUE: Generates a queue based on Jellyfin/ID3 genre tags and language.
        /// Route: GET /SymSmartQueue/Queue/Genre?genre=rock&language=latn&limit=50
        /// </summary>
        [HttpGet("Queue/Genre")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<List<string>> GetGenreQueue([FromQuery] string genre, [FromQuery] string language = "any", [FromQuery] int limit = 50)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(genre))
                {
                    return BadRequest("Genre parameter is required.");
                }

                var queue = _dbManager.GetGenreQueue(genre, language, limit);
                return Ok(queue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SYM API] Error generating genre queue for {Genre}.", genre);
                return StatusCode(500, "Internal server error generating genre queue.");
            }
        }

        /// <summary>
        /// TELEMETRY: Records user track completions or skips to adjust their dynamic tolerance multiplier.
        /// Route: POST /SymSmartQueue/Telemetry/Event
        /// </summary>
        [HttpPost("Telemetry/Event")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult RecordEvent([FromBody] RecordEventRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Action) || (request.Action != "COMPLETE" && request.Action != "SKIP"))
                {
                    return BadRequest("Invalid action. Must be 'COMPLETE' or 'SKIP'.");
                }

                long epochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _dbManager.RecordEvent(request.UserId, request.ItemId, request.Action, epochTime, request.TimeOfDay);
                
                return Ok(new { status = "success", message = $"Recorded {request.Action} for item {request.ItemId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SYM API] Error recording telemetry event.");
                return StatusCode(500, "Internal server error recording event.");
            }
        }
    }
}