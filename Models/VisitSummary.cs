namespace CollectiblesApi.Models
{
  public class VisitSummary
  {
    public int TotalRequests { get; set; }
    public int UniqueIps { get; set; }
    public IEnumerable<RequestLogEntry> Recent { get; set; } = Enumerable.Empty<RequestLogEntry>();
  }
}