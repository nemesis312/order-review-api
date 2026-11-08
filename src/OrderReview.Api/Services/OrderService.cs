using OrderReview.Api.DTOs;
using OrderReview.Api.Exceptions;
using OrderReview.Api.Models;
using OrderReview.Api.Repositories;

namespace OrderReview.Api.Services;

public sealed class OrderService(IOrderRepository repository, TimeProvider timeProvider) : IOrderService
{
    public const int MaxDescriptionLength = 250;
    public const decimal MaxTotal = 1_000_000m;
    public const int MaxTotalDecimalPlaces = 2;

    public async Task<IReadOnlyList<OrderResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var orders = await repository.GetAllAsync(cancellationToken);
        return orders.Select(OrderResponse.From).ToList();
    }

    public async Task<OrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new OrderNotFoundException(id);

        return OrderResponse.From(order);
    }

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Description = request.Description!.Trim(),
            Total = request.Total,
            Status = OrderStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow()
        };

        await repository.AddAsync(order, cancellationToken);
        return OrderResponse.From(order);
    }

    private static void Validate(CreateOrderRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.CustomerId == Guid.Empty)
            errors["customerId"] = ["CustomerId must not be empty."];

        if (request.Total <= 0)
            errors["total"] = ["Total must be greater than zero."];
        else if (request.Total > MaxTotal)
            errors["total"] = [$"Total must be at most {MaxTotal:0}."];
        else if (decimal.Round(request.Total, MaxTotalDecimalPlaces) != request.Total)
            errors["total"] = [$"Total must have at most {MaxTotalDecimalPlaces} decimal places."];

        var description = request.Description?.Trim();
        if (string.IsNullOrEmpty(description))
            errors["description"] = ["Description is required."];
        else if (description.Length > MaxDescriptionLength)
            errors["description"] = [$"Description must be at most {MaxDescriptionLength} characters."];

        if (errors.Count > 0)
            throw new OrderValidationException(errors);
    }
}
