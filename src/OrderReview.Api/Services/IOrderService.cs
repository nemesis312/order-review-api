using OrderReview.Api.DTOs;

namespace OrderReview.Api.Services;

public interface IOrderService
{
    Task<IReadOnlyList<OrderResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<OrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken);
}
