using Microsoft.EntityFrameworkCore;
using OrderReview.Api.Models;

namespace OrderReview.Api.Data;

/// <summary>Deterministic development data: fixed ids and dates, safe to run many times.</summary>
public static class DbSeeder
{
    private static readonly Guid CustomerA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CustomerB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static IReadOnlyList<Order> SeedOrders { get; } =
    [
        Build("a0000000-0000-0000-0000-000000000001", CustomerA, "Mechanical keyboard", 129.99m, OrderStatus.Pending, "2025-01-10T09:00:00Z", null),
        Build("a0000000-0000-0000-0000-000000000002", CustomerB, "USB-C docking station", 249.50m, OrderStatus.Pending, "2025-01-11T10:30:00Z", null),
        Build("a0000000-0000-0000-0000-000000000003", CustomerA, "27-inch monitor", 389.00m, OrderStatus.Processing, "2025-01-08T14:15:00Z", "2025-01-09T08:00:00Z"),
        Build("a0000000-0000-0000-0000-000000000004", CustomerB, "Noise-cancelling headphones", 199.00m, OrderStatus.Shipped, "2025-01-05T16:45:00Z", "2025-01-07T12:00:00Z"),
        Build("a0000000-0000-0000-0000-000000000005", CustomerA, "Laptop stand", 45.25m, OrderStatus.Delivered, "2025-01-02T11:00:00Z", "2025-01-06T17:30:00Z")
    ];

    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var seedIds = SeedOrders.Select(o => o.Id).ToList();
        var existingIds = await dbContext.Orders
            .Where(o => seedIds.Contains(o.Id))
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var missing = SeedOrders.Where(o => !existingIds.Contains(o.Id)).Select(Clone).ToList();
        if (missing.Count == 0)
            return;

        dbContext.Orders.AddRange(missing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Order Build(string id, Guid customerId, string description, decimal total,
        OrderStatus status, string createdAt, string? updatedAt) => new()
    {
        Id = Guid.Parse(id),
        CustomerId = customerId,
        Description = description,
        Total = total,
        Status = status,
        CreatedAt = DateTimeOffset.Parse(createdAt, null, System.Globalization.DateTimeStyles.AssumeUniversal),
        UpdatedAt = updatedAt is null ? null : DateTimeOffset.Parse(updatedAt, null, System.Globalization.DateTimeStyles.AssumeUniversal)
    };

    private static Order Clone(Order o) => new()
    {
        Id = o.Id, CustomerId = o.CustomerId, Description = o.Description, Total = o.Total,
        Status = o.Status, CreatedAt = o.CreatedAt, UpdatedAt = o.UpdatedAt
    };
}
