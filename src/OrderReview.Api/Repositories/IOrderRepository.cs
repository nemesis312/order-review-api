using OrderReview.Api.Models;

namespace OrderReview.Api.Repositories;

public interface IOrderRepository
{
    Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Order order, CancellationToken cancellationToken);
}
