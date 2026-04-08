using BalanzaQ.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BalanzaQ.Web.Data;

public class BalanzaDbContext : DbContext
{
    public DbSet<PluItem> PluItems { get; set; } = null!;
    public DbSet<Balanza> Balanzas { get; set; } = null!;
    public DbSet<SyncLog> SyncLogs { get; set; } = null!;
    public DbSet<ImportLog> ImportLogs { get; set; } = null!;
    public DbSet<AppSetting> AppSettings { get; set; } = null!;

    public BalanzaDbContext(DbContextOptions<BalanzaDbContext> options)
        : base(options)
    {
    }
}
