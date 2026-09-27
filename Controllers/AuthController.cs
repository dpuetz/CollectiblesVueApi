using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CollectiblesApi.Models;
using CollectiblesApi.Repositories;
using CollectiblesApi.Services;

namespace CollectiblesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
  private readonly IUserRepository _userRepository;
  private readonly IJwtTokenService _tokenService;
  private readonly PasswordHasher<User> _passwordHasher = new();

  public AuthController(IUserRepository userRepository, IJwtTokenService tokenService)
  {
    _userRepository = userRepository;
    _tokenService = tokenService;
  }

  [HttpPost("login")]
  [AllowAnonymous]
  public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
  {
    var user = await _userRepository.GetByEmailAsync(request.Email);
    var hasher = new PasswordHasher<User>();

    if (user is null || !user.IsActive)
    {
      return Unauthorized(new { error = "Invalid email or password." });
    }

    var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

    if (verifyResult == PasswordVerificationResult.Failed)
    {
      return Unauthorized(new { error = "Invalid email or password." });
    }

    var groups = await _userRepository.GetGroupNamesForUserAsync(user.Id);
    var (token, expiresAtUtc) = _tokenService.GenerateToken(user, groups);

    return Ok(new LoginResponse
    {
      Token = token,
      Id = user.Id,
      ExpiresAtUtc = expiresAtUtc,
      Email = user.Email,
      FirstName = user.FirstName,
      LastName = user.LastName,
      Groups = groups,
      VendorCompanyId = user.VendorCompanyId,
      IsVendor = user.IsVendor
    });
  }
}