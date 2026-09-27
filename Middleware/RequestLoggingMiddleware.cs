using CollectiblesApi.Repositories;

namespace CollectiblesApi.Middleware
{
  public class RequestLoggingMiddleware
  {
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
      _next = next;
      _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IVisitStatsRepository visitStats, IWebHostEnvironment env)
    {
      if (env.IsDevelopment())
      {
        await _next(context);
        return;
      }
      try
      {
        var ip = context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded)
            ? forwarded.ToString().Split(',')[0].Trim()
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await visitStats.LogRequestAsync(ip, context.Request.Method, context.Request.Path.Value ?? "");
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Failed to write request log");
      }

      await _next(context);
    }
  }
}