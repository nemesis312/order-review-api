using OrderReview.Api.Models;

namespace OrderReview.Api.DTOs;

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string Description,
    decimal Total,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public static OrderResponse From(Order order) => new(
        order.Id,
        order.CustomerId,
        order.Description,
        order.Total,
        order.Status,
        order.CreatedAt,
        order.UpdatedAt);
}
