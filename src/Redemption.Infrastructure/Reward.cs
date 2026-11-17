namespace Redemption.Infrastructure;

public sealed class Reward
{
    public string Sku { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public int Points { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Deterministic seed data: migrations require stable values.</summary>
public static class RewardSeed
{
    private static readonly string[] Categories = ["Merch", "Beverages", "Electronics", "Home", "Vouchers"];

    public static IEnumerable<Reward> Create()
    {
        yield return new Reward { Sku = "CAP-01", Name = "Branded cap", Category = "Merch", Points = 500 };
        yield return new Reward { Sku = "BOTTLE-01", Name = "Reusable bottle", Category = "Merch", Points = 1000 };
        yield return new Reward { Sku = "COOLER-01", Name = "Portable cooler", Category = "Home", Points = 5000 };

        for (var i = 1; i <= 200; i++)
        {
            yield return new Reward
            {
                Sku = $"RW-{i:000}",
                Name = $"Reward {i:000}",
                Category = Categories[i % Categories.Length],
                Points = 100 + (i * 37 % 4900)
            };
        }
    }
}