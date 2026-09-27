using CollectiblesApi.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CollectiblesApi.Controllers
{
  [ApiController]
  [Route("api/stats")]
  public class StatsController : ControllerBase
  {
    private readonly IVisitStatsRepository _visitStats;

    public StatsController(IVisitStatsRepository visitStats)
    {
      _visitStats = visitStats;
    }

    [HttpGet("visits")]
    public async Task<IActionResult> GetVisitStats()
    {
      var summary = await _visitStats.GetSummaryAsync();
      return Ok(summary);
    }
  }
}