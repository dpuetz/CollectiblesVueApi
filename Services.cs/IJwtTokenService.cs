using CollectiblesApi.Models;

namespace CollectiblesApi.Services;

public interface IJwtTokenService
{
  (string Token, DateTime ExpiresAtUtc) GenerateToken(User user, IEnumerable<string> groups);
}
