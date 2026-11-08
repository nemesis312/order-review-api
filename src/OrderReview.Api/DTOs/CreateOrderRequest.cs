namespace OrderReview.Api.DTOs;

// No validation attributes on purpose: every business rule lives in OrderService.
// Description is nullable so a missing value reaches the service instead of being
// rejected by MVC's implicit [Required] for non-nullable reference types.
public sealed record CreateOrderRequest(Guid CustomerId, string? Description, decimal Total);
