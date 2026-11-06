using Microsoft.EntityFrameworkCore;
using Redemption.Domain;
using System.Text.Json;

namespace Redemption.Infrastructure;

public sealed class RedemptionDbContext(DbContextOptions<RedemptionDbContext> options) : DbContext(options)
{
    public DbSet<RedemptionOrder> Redemptions => Set<RedemptionOrder>();
    public DbSet<PointsReservation> PointsReservations => Set<PointsReservation>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RedemptionOrder>(order =>
        {
            order.ToTable("Redemptions");
            order.HasKey(o => o.Id);
            order.Property(o => o.Id).ValueGeneratedNever();
            order.Property(o => o.CustomerId).HasMaxLength(64);
            order.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);
            order.Property(o => o.LastDeliveryStatus).HasConversion<string>().HasMaxLength(32);
            order.Property(o => o.StatusAfterReversal).HasConversion<string>().HasMaxLength(32);
            order.Property(o => o.RejectionReason).HasMaxLength(256);
            order.HasIndex(o => new { o.CustomerId, o.Status });

            // Optimistic concurrency without leaking persistence concerns into the domain.
            order.Property<byte[]>("RowVersion").IsRowVersion();

            // Lines and history are always loaded with the order, so they live in JSON columns.
            order.OwnsMany(o => o.Lines, lines =>
            {
                lines.ToJson();
                lines.Ignore(l => l.TotalPoints);
            });
            order.OwnsMany(o => o.History, history =>
            {
                history.ToJson();
                history.Property(h => h.From).HasConversion<string>();
                history.Property(h => h.To).HasConversion<string>();
            });

            order.Ignore(o => o.DomainEvents);
        });

        modelBuilder.Entity<PointsReservation>(reservation =>
        {
            reservation.ToTable("PointsReservations");
            reservation.HasKey(r => r.RedemptionId);
            reservation.Property(r => r.CustomerId).HasMaxLength(64);
            reservation.Property(r => r.State).HasConversion<string>().HasMaxLength(16);
            reservation.HasIndex(r => new { r.CustomerId, r.State });
        });

        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.ToTable("OutboxMessages");
            outbox.HasKey(m => m.Id);
            outbox.Property(m => m.Sequence).UseIdentityColumn();
            outbox.Property(m => m.Type).HasMaxLength(128);
            outbox.Property(m => m.LastError).HasMaxLength(2048);
            // Filtered index: the relay only ever looks for pending messages, in order.
            outbox.HasIndex(m => m.Sequence).HasFilter("[ProcessedAt] IS NULL");
        });

        modelBuilder.Entity<InboxMessage>(inbox =>
        {
            inbox.ToTable("InboxMessages");
            // Composite key: the same message can be handled once per consumer.
            inbox.HasKey(i => new { i.MessageId, i.Consumer });
            inbox.Property(i => i.MessageId).HasMaxLength(128);
            inbox.Property(i => i.Consumer).HasMaxLength(64);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await SyncPointsReservationsAsync(cancellationToken);

        // Outbox rows join the same transaction as the order and the ledger.
        var ordersWithEvents = CaptureDomainEvents();
        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var order in ordersWithEvents)
            order.ClearDomainEvents();

        return result;
    }

    private List<RedemptionOrder> CaptureDomainEvents()
    {
        var orders = ChangeTracker.Entries<RedemptionOrder>()
            .Select(e => e.Entity)
            .Where(o => o.DomainEvents.Count > 0)
            .ToList();

        foreach (var order in orders)
        {
            foreach (var domainEvent in order.DomainEvents)
            {
                OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.CreateVersion7(),
                    AggregateId = domainEvent.RedemptionId,
                    Type = domainEvent.GetType().Name,
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), OutboxJson.Options),
                    OccurredAt = domainEvent.OccurredAt
                });
            }
        }

        return orders;
    }
    // Keeps the local points ledger consistent with each order, in the same SQL transaction.
    private async Task SyncPointsReservationsAsync(CancellationToken ct)
    {
        var orders = ChangeTracker.Entries<RedemptionOrder>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .Select(e => e.Entity)
            .ToList();

        foreach (var order in orders)
        {
            var desired = PointsLedger.DesiredState(order);
            var reservation = await PointsReservations.FindAsync([order.Id], ct);

            if (reservation is null)
            {
                if (desired == ReservationState.Active)
                {
                    PointsReservations.Add(new PointsReservation
                    {
                        RedemptionId = order.Id,
                        CustomerId = order.CustomerId,
                        Points = order.TotalPoints,
                        State = ReservationState.Active,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }
                continue;
            }

            if (desired is not null && reservation.State != desired)
            {
                reservation.State = desired.Value;
                reservation.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
    }
}