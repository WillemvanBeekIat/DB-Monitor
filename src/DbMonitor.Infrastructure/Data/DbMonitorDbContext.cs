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
            e.ToTable("audit_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Timestamp).HasColumnName("timestamp");
            e.Property(x => x.ActionType).HasColumnName("action_type").HasConversion<string>();
            e.Property(x => x.Trigger).HasColumnName("trigger").HasConversion<string>();
            e.Property(x => x.Target).HasColumnName("target");
            e.Property(x => x.Reason).HasColumnName("reason");
            e.Property(x => x.IsDryRun).HasColumnName("is_dry_run");
            e.Property(x => x.Outcome).HasColumnName("outcome").HasConversion<string>();
            e.Property(x => x.IsManualOverride).HasColumnName("is_manual_override");
            e.Property(x => x.ErrorMessage).HasColumnName("error_message");
            e.Property(x => x.TriggeredBy).HasColumnName("triggered_by");
        });

        modelBuilder.Entity<InstanceHealthEntity>(e =>
        {
            e.ToTable("instance_health");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Timestamp).HasColumnName("timestamp");
            e.Property(x => x.IsReachable).HasColumnName("is_reachable");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.ConsecutiveFailures).HasColumnName("consecutive_failures");
            e.Property(x => x.ConnectionOpenMs).HasColumnName("connection_open_ms");
            e.Property(x => x.ProbeQueryMs).HasColumnName("probe_query_ms");
            e.Property(x => x.ErrorMessage).HasColumnName("error_message");
            e.Property(x => x.IsCurrent).HasColumnName("is_current");
            e.HasMany(x => x.Databases).WithOne().HasForeignKey(x => x.InstanceHealthEntityId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DatabaseHealthEntity>(e =>
        {
            e.ToTable("database_health");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.InstanceHealthEntityId).HasColumnName("instance_health_id");
            e.Property(x => x.DatabaseName).HasColumnName("database_name");
            e.Property(x => x.IsOnline).HasColumnName("is_online");
            e.Property(x => x.IsReachable).HasColumnName("is_reachable");
            e.Property(x => x.State).HasColumnName("state");
            e.Property(x => x.ErrorMessage).HasColumnName("error_message");
        });

        modelBuilder.Entity<CooldownEntity>(e =>
        {
            e.ToTable("cooldowns");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasColumnName("key");
            e.Property(x => x.Until).HasColumnName("until");
        });

        modelBuilder.Entity<BookmarkEntity>(e =>
        {
            e.ToTable("bookmarks");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasColumnName("key");
            e.Property(x => x.Value).HasColumnName("value");
        });
    }
}
