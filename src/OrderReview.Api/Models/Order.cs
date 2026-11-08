namespace OrderReview.Api.Models;

public class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}
