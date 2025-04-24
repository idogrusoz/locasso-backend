using System;

namespace Web.Domain.Entities
{
    public class User
    {
        public Guid Id { get; private set; }
        public string ExternalId { get; private set; } = string.Empty; // Apple ID or other auth provider's user ID
        public string Email { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public string PhotoUrl { get; private set; } = string.Empty;
        public UserRole Role { get; private set; }
        public string AuthProvider { get; private set; } = string.Empty; // "apple", "google", etc.
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastLoginAt { get; private set; }

        private User() { } // Required for EF Core

        public static User Create(
            string externalId,
            string email,
            string name,
            string photoUrl,
            string authProvider,
            UserRole role = UserRole.Traveler)
        {
            return new User
            {
                Id = Guid.NewGuid(),
                ExternalId = externalId,
                Email = email,
                Name = name,
                PhotoUrl = photoUrl,
                Role = role,
                AuthProvider = authProvider,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
        }

        public void UpdateLastLogin()
        {
            LastLoginAt = DateTime.UtcNow;
        }
    }

    public enum UserRole
    {
        Admin,
        Guide,
        Traveler
    }
} 