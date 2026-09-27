namespace CollectiblesApi.Models;

public class UserWithGroups
{
  public User User { get; set; } = null!;
  public List<string> Groups { get; set; } = new();
}