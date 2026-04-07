using Microsoft.EntityFrameworkCore;

namespace DbMonitor.Infrastructure.Data;

public class DbMonitorDbContext : DbContext
{
    public DbMonitorDbContext(DbContextOptions<DbMonitorDbContext> options) : base(options) { }

    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();
    public DbSet<InstanceHealthEntity> InstanceHealthSnapshots => Set<InstanceHealthEntity>();
    public DbSet<DatabaseHealthEntity> DatabaseHealthSnapshots => Set<DatabaseHealthEntity>();
    public DbSet<CooldownEntity> Cooldowns => Set<CooldownEntity>();
    public DbSet<BookmarkEntity> Bookmarks => Set<BookmarkEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEntryEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ActionType).HasConversion<string>();
            e.Property(x => x.Trigger).HasConversion<string>();
            e.Property(x => x.Outcome).HasConversion<string>();
        });

        modelBuilder.Entity<InstanceHealthEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasMany(x => x.Databases).WithOne().HasForeignKey(x => x.InstanceHealthEntityId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DatabaseHealthEntity>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<CooldownEntity>(e =>
        {
            e.HasKey(x => x.Key);
        });

        modelBuilder.Entity<BookmarkEntity>(e =>
        {
            e.HasKey(x => x.Key);
        });
    }
}
