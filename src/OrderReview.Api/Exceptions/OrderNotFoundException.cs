namespace OrderReview.Api.Exceptions;

public sealed class OrderNotFoundException(Guid orderId)
    : Exception($"Order '{orderId}' was not found.")
{
    public Guid OrderId { get; } = orderId;
}
