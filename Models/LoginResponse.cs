namespace CollectiblesApi.Models;

public class LoginResponse
{
  public string Token { get; set; } = string.Empty;
  public int Id { get; set; }
  public DateTime ExpiresAtUtc { get; set; }
  public string Email { get; set; } = string.Empty;
  public string FirstName { get; set; } = string.Empty;
  public string LastName { get; set; } = string.Empty;
  public List<string> Groups { get; set; } = new();
  public bool IsVendor { get; set; }
  public int VendorCompanyId { get; set; }
}
