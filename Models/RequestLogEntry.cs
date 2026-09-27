namespace CollectiblesApi.Models
{
  public class RequestLogEntry
  {
    public int Id { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
  }
}