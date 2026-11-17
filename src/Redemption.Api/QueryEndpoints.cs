using System.Buffers.Text;
using Microsoft.EntityFrameworkCore;
using Redemption.Domain;
using Redemption.Infrastructure;

namespace Redemption.Api;

public static class QueryEndpoints
{
    public static void MapQueryEndpoints(this IEndpointRouteBuilder app)
    {
        // Offset pagination: small, stable catalog where users jump between pages.
        app.MapGet("/rewards", async (
            string? category, string? search, int? page, int? pageSize, RedemptionDbContext db, CancellationToken ct) =>
        {
            var size = Math.Clamp(pageSize ?? 20, 1, 100);
            var number = Math.Max(page ?? 1, 1);

            var query = db.Rewards.AsNoTracking().Where(r => r.IsActive);
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(r => r.Category == category);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(r => r.Name.Contains(search));

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(r => r.Points).ThenBy(r => r.Sku)
                .Skip((number - 1) * size).Take(size)
                .Select(r => new RewardDto(r.Sku, r.Name, r.Category, r.Points))
                .ToListAsync(ct);

            return Results.Ok(new PagedResult<RewardDto>(items, number, size, total));
        });

        // Keyset pagination: an ever-growing list. The cost doesn't grow with page depth,
        // and new inserts never shift rows between pages.
        app.MapGet("/customers/{customerId}/redemptions", async (
            string customerId, string? status, string? cursor, int? limit, RedemptionDbContext db, CancellationToken ct) =>
        {
            var size = Math.Clamp(limit ?? 20, 1, 100);
            var query = db.Redemptions.AsNoTracking().Where(o => o.CustomerId == customerId);

            if (status is not null)
            {
                if (!Enum.TryParse<RedemptionStatus>(status, ignoreCase: true, out var parsed))
                    return Results.BadRequest(new { error = $"Unknown status '{status}'." });
                query = query.Where(o => o.Status == parsed);
            }

            if (cursor is not null)
            {
                if (!TryDecodeCursor(cursor, out var after))
                    return Results.BadRequest(new { error = "Invalid cursor." });
                query = query.Where(o => o.CreatedAt < after);
            }

            // Projection: the JSON columns (lines, history) are never read for a list.
            var page = await query
                .OrderByDescending(o => o.CreatedAt)
                .Take(size + 1)
                .Select(o => new RedemptionSummary(o.Id, o.Status, o.TotalPoints, o.CreatedAt, o.UpdatedAt))
                .ToListAsync(ct);

            var items = page.Take(size).ToList();
            var nextCursor = page.Count > size ? EncodeCursor(items[^1].CreatedAt) : null;
            return Results.Ok(new { items, nextCursor });
        });
    }

    // Lab simplification: production cursors also carry a unique tie-breaker (e.g. the id).
    private static string EncodeCursor(DateTimeOffset createdAt) =>
        Base64Url.EncodeToString(BitConverter.GetBytes(createdAt.UtcTicks));

    private static bool TryDecodeCursor(string cursor, out DateTimeOffset createdAt)
    {
        createdAt = default;
        try
        {
            var bytes = Base64Url.DecodeFromChars(cursor);
            if (bytes.Length != sizeof(long))
                return false;
            createdAt = new DateTimeOffset(BitConverter.ToInt64(bytes), TimeSpan.Zero);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}