using Microsoft.EntityFrameworkCore;
using Redemption.Domain;

namespace Redemption.Infrastructure;

public sealed class RedemptionDbContext(DbContextOptions<RedemptionDbContext> options) : DbContext(options)
{
    public DbSet<RedemptionOrder> Redemptions => Set<RedemptionOrder>();
    public DbSet<PointsReservation> PointsReservations => Set<PointsReservation>();

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
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await SyncPointsReservationsAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
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