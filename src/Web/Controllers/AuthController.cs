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

        [HttpPost("signin")]
        public async Task<ActionResult<AuthenticateUserResult>> SignIn()
        {
            try
            {
                _logger.LogInformation("🔐 Processing authenticated SignIn");

                var userId = Request.Headers["x-ms-client-principal-id"];
                var Identity = Request.Headers["x-ms-client-principal"];
                var name = Request.Headers["x-ms-client-principal-name"];
                var email = Request.Headers["x-ms-client-principal-email"];
                var idp = Request.Headers["x-ms-client-principal-idp"];
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(Identity))
                {
                    _logger.LogWarning("❌ Missing essential user claims");
                    return BadRequest("Missing required user claims.");
                }
                _logger.LogInformation("🔐 User authenticated: UserId={UserId}, Identity={Identity}", userId, Identity);
                _logger.LogInformation("🔐 User claims: Name={Name}, Email={Email}, Idp={Idp}", name, email, idp);
                
                // var command = new AuthenticateUserCommand
                // {
                //     UserId = userId,
                //     Email = email,
                //     Name = name ?? string.Empty,
                //     PhotoUrl = photoUrl,
                //     AuthProvider = idp
                // };

                // var result = await _mediator.Send(command);
                // _logger.LogInformation("✅ User authenticated: IsNew={IsNew}, Role={Role}", result.IsNewUser, result.Role);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "⚠️ Error during SignIn");
                return StatusCode(500, "Authentication failed.");
            }
        }

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