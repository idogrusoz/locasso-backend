using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Application.Auth.Commands;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IMediator mediator, ILogger<AuthController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [Authorize]
        [HttpPost("signin")]
        public async Task<ActionResult<AuthenticateUserResult>> SignIn()
        {
            try
            {
                _logger.LogInformation("🔐 Processing authenticated SignIn");

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
                var name = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value;
                var photoUrl = User.FindFirst("picture")?.Value ?? string.Empty;
                var idp = User.FindFirst("idp")?.Value ?? "unknown";

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning("❌ Missing essential user claims");
                    return BadRequest("Missing required user claims.");
                }

                var command = new AuthenticateUserCommand
                {
                    UserId = userId,
                    Email = email,
                    Name = name ?? string.Empty,
                    PhotoUrl = photoUrl,
                    AuthProvider = idp
                };

                var result = await _mediator.Send(command);
                _logger.LogInformation("✅ User authenticated: IsNew={IsNew}, Role={Role}", result.IsNewUser, result.Role);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "⚠️ Error during SignIn");
                return StatusCode(500, "Authentication failed.");
            }
        }

        [Authorize]
        [HttpGet("me")]
        public ActionResult GetCurrentUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
            var name = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value;

            return Ok(new
            {
                Id = userId ?? "Unknown",
                Email = email ?? "Unknown",
                Name = name ?? "Unknown"
            });
        }

        [HttpGet("diagnostic")]
        public ActionResult GetAuthDiagnostic()
        {
            var claims = User.Claims.Select(c => new
            {
                Type = c.Type,
                Value = MaskSensitiveValue(c.Type, c.Value)
            }).ToList();

            return Ok(new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                Claims = claims,
                HasZumoAuthHeader = Request.Headers.ContainsKey("x-zumo-auth")
            });
        }

        private string MaskSensitiveValue(string claimType, string value)
        {
            if (string.IsNullOrEmpty(value)) return "[NULL]";
            if (new[] { "email", "sub", ClaimTypes.NameIdentifier, ClaimTypes.Email, "name" }
                .Contains(claimType, StringComparer.OrdinalIgnoreCase))
            {
                return value.Length <= 5 ? "*****" : $"{value[..3]}...{value[^2..]}";
            }

            return value;
        }
    }
}