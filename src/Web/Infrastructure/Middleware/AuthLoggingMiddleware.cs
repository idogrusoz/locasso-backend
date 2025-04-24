using System.Net;
using Microsoft.Extensions.Logging;

namespace Web.Infrastructure.Middleware
{
    public class AuthLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthLoggingMiddleware> _logger;

        public AuthLoggingMiddleware(RequestDelegate next, ILogger<AuthLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Continue the pipeline
                await _next(context);

                // Log authentication failures
                if (context.Response.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    var endpoint = context.GetEndpoint()?.DisplayName;
                    var path = context.Request.Path;
                    var method = context.Request.Method;
                    var headers = string.Join(", ", context.Request.Headers
                        .Where(h => !h.Key.Contains("Authorization", StringComparison.OrdinalIgnoreCase))
                        .Select(h => h.Key));

                    _logger.LogWarning(
                        "Authentication failed: StatusCode={StatusCode}, Path={Path}, Method={Method}, Endpoint={Endpoint}, Headers={Headers}",
                        context.Response.StatusCode, path, method, endpoint, headers);
                }
                else if (context.Response.StatusCode == (int)HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning(
                        "Authorization failed: StatusCode={StatusCode}, Path={Path}, Method={Method}, User={User}",
                        context.Response.StatusCode, context.Request.Path, context.Request.Method, 
                        context.User?.Identity?.Name ?? "Unknown");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in auth pipeline");
                throw;
            }
        }
    }

    // Extension methods for adding this middleware to the pipeline
    public static class AuthLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuthLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthLoggingMiddleware>();
        }
    }
} 