using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Web.Application.Auth.Commands;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
                _logger.LogInformation("User authentication attempt in progress");
                
                // Get Azure Auth token from header
                string userId, email, name, photoUrl, authProvider;
                
                if (HttpContext.Request.Headers.TryGetValue("x-zumo-auth", out var authHeader))
                {
                    _logger.LogInformation("Using x-zumo-auth token for authentication");
                    
                    // Decode JWT token
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var token = tokenHandler.ReadJwtToken(authHeader);
                    
                    // Extract user info from token
                    userId = token.Subject ?? token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? string.Empty;
                    email = token.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? string.Empty;
                    name = token.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? string.Empty;
                    photoUrl = token.Claims.FirstOrDefault(c => c.Type == "picture")?.Value ?? string.Empty;
                    
                    // Determine provider from token claims
                    var issuer = token.Issuer?.ToLowerInvariant() ?? string.Empty;
                    if (issuer.Contains("apple"))
                    {
                        authProvider = "apple";
                    }
                    else if (issuer.Contains("google"))
                    {
                        authProvider = "google";
                    }
                    else
                    {
                        authProvider = "unknown";
                    }
                }
                else if (HttpContext.Request.Query.TryGetValue("_dev_mode", out var devMode) && devMode == "true")
                {
                    // Development mode - get user info from query parameters for testing
                    _logger.LogWarning("Using development mode authentication parameters");
                    userId = HttpContext.Request.Query["userId"].ToString() ?? Guid.NewGuid().ToString();
                    email = HttpContext.Request.Query["email"].ToString() ?? "test@example.com";
                    name = HttpContext.Request.Query["name"].ToString() ?? "Test User";
                    photoUrl = HttpContext.Request.Query["photoUrl"].ToString() ?? "";
                    authProvider = HttpContext.Request.Query["provider"].ToString() ?? "dev";
                }
                else
                {
                    // Fallback to standard claims from User object
                    _logger.LogInformation("Falling back to standard claims from User object");
                    userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
                    email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
                    name = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
                    photoUrl = User.FindFirst("picture")?.Value ?? string.Empty;
                    
                    // Determine the auth provider 
                    authProvider = "apple"; // Default
                    if (User.Identity?.AuthenticationType?.Contains("google", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        authProvider = "google";
                    }
                }

                // Log authentication details but mask sensitive data
                _logger.LogInformation("Authentication attempt details: UserId={UserId}, Email={EmailMasked}, Name={NameAvailable}, Provider={Provider}",
                    !string.IsNullOrEmpty(userId) ? userId.Substring(0, Math.Min(5, userId.Length)) + "..." : "[Missing]", 
                    !string.IsNullOrEmpty(email) ? "[Email Available]" : "[Email Missing]",
                    !string.IsNullOrEmpty(name) ? "[Name Available]" : "[Name Missing]",
                    authProvider);

                // If essential claims are missing, return bad request
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning("Authentication failed: missing essential claims. UserIdExists={UserIdExists}, EmailExists={EmailExists}",
                        !string.IsNullOrEmpty(userId), !string.IsNullOrEmpty(email));
                    return BadRequest("User claims are incomplete");
                }

                var command = new AuthenticateUserCommand
                {
                    UserId = userId,
                    Email = email,
                    Name = name ?? string.Empty,
                    PhotoUrl = photoUrl ?? string.Empty,
                    AuthProvider = authProvider
                };

                _logger.LogInformation("Sending authentication command to handler");
                var result = await _mediator.Send(command);
                
                _logger.LogInformation("Authentication successful. IsNewUser={IsNewUser}, Role={Role}", 
                    result.IsNewUser, result.Role);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user authentication");
                return StatusCode(500, "An error occurred during authentication. Please try again.");
            }
        }

        [HttpGet("me")]
        [Authorize]
        public ActionResult GetCurrentUser()
        {
            try
            {
                _logger.LogInformation("Retrieving current user info");
                
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                var name = User.FindFirst(ClaimTypes.Name)?.Value;
                
                _logger.LogInformation("Current user info retrieved successfully");
                
                return Ok(new 
                { 
                    Id = userId ?? "Unknown",
                    Email = email ?? "Unknown",
                    Name = name ?? "Unknown"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current user info");
                return StatusCode(500, "An error occurred while retrieving user information.");
            }
        }
        
        [HttpGet("diagnostic")]
        [ApiExplorerSettings(IgnoreApi = true)] // Hide from Swagger as this is for diagnostic only
        public ActionResult GetAuthDiagnostic()
        {
            try
            {
                _logger.LogInformation("Running authentication diagnostic");
                
                var headers = Request.Headers.ToDictionary(
                    h => h.Key, 
                    h => h.Key.Contains("Authorization", StringComparison.OrdinalIgnoreCase) || 
                         h.Key.Contains("x-zumo-auth", StringComparison.OrdinalIgnoreCase)
                        ? "[REDACTED]" 
                        : h.Value.ToString());
                
                var diagnostic = new Dictionary<string, object>
                {
                    ["IsAuthenticated"] = User.Identity?.IsAuthenticated ?? false,
                    ["Claims"] = User.Claims.Select(c => new { Type = c.Type, Value = MaskSensitiveValue(c.Type, c.Value ?? string.Empty) }).ToList(),
                    ["Headers"] = headers,
                    ["HasZumoAuthHeader"] = Request.Headers.ContainsKey("x-zumo-auth"),
                    ["RemoteIp"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    ["Timestamp"] = DateTime.UtcNow
                };
                
                _logger.LogInformation("Authentication diagnostic completed");
                return Ok(diagnostic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running authentication diagnostic");
                return StatusCode(500, "An error occurred while running authentication diagnostic.");
            }
        }
        
        // Helper method to mask sensitive information in claim values
        private string MaskSensitiveValue(string claimType, string value)
        {
            if (string.IsNullOrEmpty(value)) return "[NULL]";
            
            // Mask sensitive claims to prevent logging sensitive user information
            var sensitiveClaimTypes = new[]
            {
                ClaimTypes.Email,
                ClaimTypes.Name,
                ClaimTypes.NameIdentifier,
                "sub",
                "email",
                "name"
            };
            
            if (sensitiveClaimTypes.Contains(claimType, StringComparer.OrdinalIgnoreCase))
            {
                if (value.Length <= 5) return "****";
                return value.Substring(0, 3) + "..." + value.Substring(value.Length - 2);
            }
            
            return value;
        }
    }
} 