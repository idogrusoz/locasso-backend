using Microsoft.EntityFrameworkCore;
using Web.Domain.Entities;

namespace Web.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired();
                entity.Property(e => e.ExternalId).IsRequired();
                entity.Property(e => e.AuthProvider).IsRequired();
                entity.HasIndex(e => new { e.ExternalId, e.AuthProvider }).IsUnique();
            });
        }
    }
} 