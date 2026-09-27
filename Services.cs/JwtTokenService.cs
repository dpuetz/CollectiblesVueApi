using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using CollectiblesApi.Models;

namespace CollectiblesApi.Services;

public class JwtTokenService : IJwtTokenService
{
  private readonly IConfiguration _configuration;

  public JwtTokenService(IConfiguration configuration)
  {
    _configuration = configuration;
  }

  public (string Token, DateTime ExpiresAtUtc) GenerateToken(User user, IEnumerable<string> groups)
  {
    var jwtSection = _configuration.GetSection("Jwt");
    var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key not configured.");
    var issuer = jwtSection["Issuer"];
    var audience = jwtSection["Audience"];
    var expiryMinutes = jwtSection.GetValue("ExpiryMinutes", 60);

    var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

    claims.AddRange(groups.Select(g => new Claim(ClaimTypes.Role, g)));

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
    var expires = DateTime.UtcNow.AddMinutes(expiryMinutes);

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: expires,
        signingCredentials: credentials);

    return (new JwtSecurityTokenHandler().WriteToken(token), expires);
  }
}
