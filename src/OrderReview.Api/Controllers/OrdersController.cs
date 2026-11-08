using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderReview.Api.DTOs;
using OrderReview.Api.Security;
using OrderReview.Api.Services;

namespace OrderReview.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.OrdersRead)]
    [ProducesResponseType<IReadOnlyList<OrderResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.OrdersRead)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.OrdersCreate)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var created = await orderService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
