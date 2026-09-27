using CollectiblesApi.Models;

namespace CollectiblesApi.Repositories
{
  public interface IVisitStatsRepository
  {
    Task LogRequestAsync(string ipAddress, string method, string path);
    Task<VisitSummary> GetSummaryAsync();
    Task<IEnumerable<RequestLogEntry>> GetRecentAsync(int count = 20);
  }
}