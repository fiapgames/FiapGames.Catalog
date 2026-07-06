using FiapGames.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FiapGames.Data;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<UserGameLibrary> UserGameLibraries => Set<UserGameLibrary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Title).HasMaxLength(200).IsRequired();
            entity.Property(g => g.Description).HasMaxLength(2000);
            entity.Property(g => g.Genre).HasMaxLength(100);
            entity.Property(g => g.Price).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Price).HasColumnType("decimal(18,2)");
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(o => o.RejectionReason).HasMaxLength(500);
            entity.HasIndex(o => o.UserId);

            entity.HasOne(o => o.Game)
                .WithMany()
                .HasForeignKey(o => o.GameId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserGameLibrary>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.HasIndex(l => new { l.UserId, l.GameId }).IsUnique();

            entity.HasOne(l => l.Game)
                .WithMany()
                .HasForeignKey(l => l.GameId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Order)
                .WithMany()
                .HasForeignKey(l => l.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
