using eTicketing.Contracts.Messaging;
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
    public DbSet<GateDevice> GateDevices => Set<GateDevice>();
    public DbSet<GateDeviceSector> GateDeviceSectors => Set<GateDeviceSector>();

    /// <summary>A read model of eTicketing.Catalog's Product, not a domain table — this service
    /// never writes it except by projecting events. See ProductSnapshot.</summary>
    public DbSet<ProductSnapshot> ProductSnapshots => Set<ProductSnapshot>();

    /// <summary>Likewise a read model, of eTicketing.Identity's Organization. See
    /// OrganizationSnapshot.</summary>
    public DbSet<OrganizationSnapshot> OrganizationSnapshots => Set<OrganizationSnapshot>();

    /// <summary>Events waiting to reach the broker, written in the same transaction as the data
    /// that produced them. Not a domain table — see eTicketing.Contracts.Messaging.OutboxMessage.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketingDbContext).Assembly);

        // Lives in eTicketing.Contracts, so the assembly scan above does not reach it. Applied
        // explicitly here rather than copied, so all three services' outbox tables stay identical.
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
