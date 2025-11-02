using Microsoft.EntityFrameworkCore;
using Gov2Biz.LicenseService.Models;

namespace Gov2Biz.LicenseService.Data;

/// <summary>
/// EF Core DbContext for License Service.
/// Used primarily for calling stored procedures via FromSqlRaw.
/// </summary>
public class LicenseDbContext : DbContext
{
    public LicenseDbContext(DbContextOptions<LicenseDbContext> options) 
        : base(options)
    {
    }

    public DbSet<License> Licenses => Set<License>();
    public DbSet<LicenseHistory> LicenseHistory => Set<LicenseHistory>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tenant entity configuration
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(e => e.TenantId);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        });

        // License entity configuration
        modelBuilder.Entity<License>(entity =>
        {
            entity.ToTable("Licenses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LicenseNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ApplicantName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ApplicantEmail).IsRequired().HasMaxLength(256);
            entity.Property(e => e.LicenseType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.LicenseNumber).IsUnique();
        });

        // LicenseHistory entity configuration
        modelBuilder.Entity<LicenseHistory>(entity =>
        {
            entity.ToTable("LicenseHistory");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PerformedBy).HasMaxLength(256);
            entity.HasIndex(e => e.LicenseId);
        });

        // User entity configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Roles).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => new { e.Email, e.TenantId }).IsUnique();
        });
    }
}
