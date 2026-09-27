namespace CollectiblesApi.Models
{
  public class CreateItemRequest
  {
    public string Item { get; set; } = null!;
    public IFormFile File { get; set; } = null!;
  }
}