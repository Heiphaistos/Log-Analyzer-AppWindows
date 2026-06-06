using Microsoft.AspNetCore.Mvc;
using WinLogAnalyzer.Core.Knowledge;
using WinLogAnalyzer.Core.Models;
using WinLogAnalyzer.Core.Reader;

namespace WinLogAnalyzer.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class EventsController : ControllerBase
{
    private static readonly string[] AllowedLogs = { "System", "Application", "Security" };

    private readonly EventLogService _logService;
    private readonly SolutionProvider _solutions;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        EventLogService logService,
        SolutionProvider solutions,
        ILogger<EventsController> logger)
    {
        _logService = logService;
        _solutions = solutions;
        _logger = logger;
    }

    /// <summary>GET /api/events?log=System&max=100</summary>
    [HttpGet("events")]
    public ActionResult<IReadOnlyList<EventEntry>> GetEvents(
        [FromQuery] string log = "System",
        [FromQuery] int max = 100)
    {
        // Validation (zero confiance).
        if (max is < 1 or > 1000)
            return BadRequest(new { error = "max doit etre entre 1 et 1000." });

        if (!AllowedLogs.Contains(log, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = $"log invalide. Autorises: {string.Join(", ", AllowedLogs)}." });

        try
        {
            var entries = _logService.GetRecentCritical(log, max);
            return Ok(entries);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Lecture log {Log} echouee", log);
            return Problem(detail: ex.Message, statusCode: 403);
        }
    }

    /// <summary>GET /api/health — statut agent + taille dictionnaire.</summary>
    [HttpGet("health")]
    public ActionResult GetHealth() => Ok(new
    {
        status = "ok",
        solutions = _solutions.Count,
        logs = AllowedLogs
    });
}
