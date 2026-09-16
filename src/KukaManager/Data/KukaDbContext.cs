using Microsoft.EntityFrameworkCore;
using KukaManager.Utils;

namespace KukaManager.Data;

// EF Core is the long-term persistence boundary. The migrated 0.7 data model is
// intentionally kept table-compatible; high-volume reporting still uses optimized SQL.
public sealed class KukaDbContext(DbContextOptions<KukaDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SettingEntity>().ToTable("settings").HasKey(x => x.Key);
        modelBuilder.Entity<SettingEntity>().Property(x => x.Key).HasColumnName("key");
        modelBuilder.Entity<SettingEntity>().Property(x => x.Value).HasColumnName("value");
    }
}

public sealed class SettingEntity
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
