using Microsoft.EntityFrameworkCore;
using OrderReview.Api.Data;
using OrderReview.Api.Models;

namespace OrderReview.Api.Repositories;

public sealed class OrderRepository(AppDbContext dbContext) : IOrderRepository
{
    public async Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken)
    {
        // CreatedAt is persisted as UTC ticks (see AppDbContext), so SQLite can order by it.
        // Total is TEXT in SQLite, so it must never be used in OrderBy/Where.
        return await dbContext.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
