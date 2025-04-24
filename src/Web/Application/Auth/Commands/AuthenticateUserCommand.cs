using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Web.Domain.Entities;
using Web.Infrastructure.Data;

namespace Web.Application.Auth.Commands
{
    public class AuthenticateUserCommand : IRequest<AuthenticateUserResult>
    {
        public required string UserId { get; set; }
        public required string Email { get; set; }
        public string? Name { get; set; }
        public string? PhotoUrl { get; set; }
        public required string AuthProvider { get; set; }
    }

    public class AuthenticateUserResult
    {
        public bool IsNewUser { get; set; }
        public Guid UserId { get; set; }
        public required string Email { get; set; }
        public string? Name { get; set; }
        public string? PhotoUrl { get; set; }
        public UserRole Role { get; set; }
    }

    public class AuthenticateUserCommandHandler : IRequestHandler<AuthenticateUserCommand, AuthenticateUserResult>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<AuthenticateUserCommandHandler> _logger;

        public AuthenticateUserCommandHandler(
            ApplicationDbContext dbContext,
            ILogger<AuthenticateUserCommandHandler> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<AuthenticateUserResult> Handle(AuthenticateUserCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Processing authentication for user: Provider={Provider}, ExternalId={ExternalIdMasked}",
                    request.AuthProvider,
                    request.UserId?.Substring(0, Math.Min(5, request.UserId?.Length ?? 0)) + "...");

                // Check if user exists
                _logger.LogDebug("Checking if user exists in database");
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => 
                        u.ExternalId == request.UserId && 
                        u.AuthProvider == request.AuthProvider, 
                        cancellationToken);

                bool isNewUser = false;

                if (user == null)
                {
                    _logger.LogInformation("User not found in database. Creating new user record");
                    
                    // Ensure required values are not null
                    if (string.IsNullOrEmpty(request.UserId))
                    {
                        throw new ArgumentNullException(nameof(request.UserId), "User ID cannot be null when creating a new user");
                    }
                    
                    // Create new user
                    user = User.Create(
                        request.UserId,
                        request.Email,
                        request.Name ?? "",
                        request.PhotoUrl ?? "",
                        request.AuthProvider
                    );

                    await _dbContext.Users.AddAsync(user, cancellationToken);
                    isNewUser = true;
                    _logger.LogInformation("New user created with ID: {UserId}, Role: {Role}", user.Id, user.Role);
                }
                else
                {
                    _logger.LogInformation("User found in database. Updating last login time. UserId={UserId}, Role={Role}",
                        user.Id, user.Role);
                    // Update last login time
                    user.UpdateLastLogin();
                }

                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogDebug("Database changes saved successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save user data to database");
                    throw;
                }

                _logger.LogInformation("Authentication process completed successfully. IsNewUser={IsNewUser}", isNewUser);
                return new AuthenticateUserResult
                {
                    IsNewUser = isNewUser,
                    UserId = user.Id,
                    Email = user.Email,
                    Name = user.Name,
                    PhotoUrl = user.PhotoUrl,
                    Role = user.Role
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user authentication process");
                throw;
            }
        }
    }
} 