using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Data;

public class TicketingDbContext : DbContext, IUnitOfWork
{
    public TicketingDbContext(DbContextOptions<TicketingDbContext> options) : base(options) { }

    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketPrintBatch> TicketPrintBatches => Set<TicketPrintBatch>();
    public DbSet<TicketPrintBatchFile> TicketPrintBatchFiles => Set<TicketPrintBatchFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketingDbContext).Assembly);
    }
}
