namespace CollectiblesApi.Models;

public class User
{
  public int Id { get; set; }
  public string Email { get; set; } = string.Empty;
  public string FirstName { get; set; } = string.Empty;
  public string LastName { get; set; } = string.Empty;
  public string PasswordHash { get; set; } = string.Empty;
  public bool IsActive { get; set; }
  public DateTime CreatedAtUtc { get; set; }
  public bool IsVendor { get; set; }
  public int VendorCompanyId { get; set; }
}
