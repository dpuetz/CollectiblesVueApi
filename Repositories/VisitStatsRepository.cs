using CollectiblesApi.Data;
using CollectiblesApi.Models;
using Dapper;

namespace CollectiblesApi.Repositories
{
  public class VisitStatsRepository : IVisitStatsRepository
  {
    private readonly IDbConnectionFactory _dbFactory;

    public VisitStatsRepository(IDbConnectionFactory dbFactory)
    {
      _dbFactory = dbFactory;
    }

    public async Task LogRequestAsync(string ipAddress, string method, string path)
    {
      using var conn = _dbFactory.CreateConnection();
      await conn.ExecuteAsync(
          "INSERT INTO RequestLog (Timestamp, IpAddress, Method, Path) VALUES (@Timestamp, @IpAddress, @Method, @Path)",
          new
          {
            Timestamp = DateTime.UtcNow.ToString("o"),
            IpAddress = ipAddress,
            Method = method,
            Path = path
          });
    }

    public async Task<VisitSummary> GetSummaryAsync()
    {
      using var conn = _dbFactory.CreateConnection();

      var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RequestLog");
      var uniqueIps = await conn.ExecuteScalarAsync<int>("SELECT COUNT(DISTINCT IpAddress) FROM RequestLog");
      var recent = await conn.QueryAsync<RequestLogEntry>(
          "SELECT Id, Timestamp, IpAddress, Method, Path FROM RequestLog ORDER BY Id DESC LIMIT 20");

      return new VisitSummary
      {
        TotalRequests = total,
        UniqueIps = uniqueIps,
        Recent = recent
      };
    }

    public async Task<IEnumerable<RequestLogEntry>> GetRecentAsync(int count = 20)
    {
      using var conn = _dbFactory.CreateConnection();
      return await conn.QueryAsync<RequestLogEntry>(
          "SELECT Id, Timestamp, IpAddress, Method, Path FROM RequestLog ORDER BY Id DESC LIMIT @Count",
          new { Count = count });
    }
  }
}