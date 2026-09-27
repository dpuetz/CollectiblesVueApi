using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace CollectiblesApi.Controllers;

[ApiController]
[Route("api/sentry-tunnel")]
[AllowAnonymous]
public class SentryTunnelController : ControllerBase
{
  private readonly IHttpClientFactory _httpClientFactory;

  public SentryTunnelController(IHttpClientFactory httpClientFactory)
  {
    _httpClientFactory = httpClientFactory;
  }

  [HttpPost]
  public async Task<IActionResult> Forward()
  {
    int statusCode = 400;
    try
    {
      using var reader = new StreamReader(Request.Body);
      var envelope = await reader.ReadToEndAsync();
      var firstLine = envelope.Split('\n')[0];
      using var header = JsonDocument.Parse(firstLine);
      var dsn = header.RootElement.GetProperty("dsn").GetString();
      var dsnUri = new Uri(dsn!);
      var projectId = dsnUri.AbsolutePath.Trim('/');
      var host = dsnUri.Host;
      var sentryIngestUrl = $"https://{host}/api/{projectId}/envelope/";
      var client = _httpClientFactory.CreateClient();
      var content = new StringContent(envelope, Encoding.UTF8, "application/x-sentry-envelope");
      var response = await client.PostAsync(sentryIngestUrl, content);
      var responseBody = await response.Content.ReadAsStringAsync();
      return StatusCode((int)response.StatusCode);
    }
    catch (Exception ex)
    {
      Console.WriteLine(ex);
      return StatusCode(statusCode, ex.Message);
    }
  }
}