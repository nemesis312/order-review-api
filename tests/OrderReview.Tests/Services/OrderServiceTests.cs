using Microsoft.Extensions.Time.Testing;
using Moq;
using OrderReview.Api.DTOs;
using OrderReview.Api.Exceptions;
using OrderReview.Api.Models;
using OrderReview.Api.Repositories;
using OrderReview.Api.Services;

namespace OrderReview.Tests.Services;

[TestFixture]
public class OrderServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ValidCustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private Mock<IOrderRepository> repository = null!;
    private OrderService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new Mock<IOrderRepository>(MockBehavior.Strict);
        service = new OrderService(repository.Object, new FakeTimeProvider(Now));
    }

    [Test]
    public async Task CreateAsync_ValidRequest_ReturnsPendingOrderWithTrimmedDescriptionAndClockTime()
    {
        repository.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await service.CreateAsync(new CreateOrderRequest(ValidCustomerId, "  Keyboard  ", 49.90m), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(result.CustomerId, Is.EqualTo(ValidCustomerId));
            Assert.That(result.Description, Is.EqualTo("Keyboard"));
            Assert.That(result.Total, Is.EqualTo(49.90m));
            Assert.That(result.Status, Is.EqualTo(OrderStatus.Pending));
            Assert.That(result.CreatedAt, Is.EqualTo(Now));
            Assert.That(result.UpdatedAt, Is.Null);
        });
    }

    [TestCase(0)]
    [TestCase(-5.25)]
    public void CreateAsync_NonPositiveTotal_ThrowsValidationErrorOnTotalAndDoesNotPersist(double total)
    {
        var request = new CreateOrderRequest(ValidCustomerId, "Keyboard", (decimal)total);

        var ex = Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request, CancellationToken.None));

        Assert.That(ex!.Errors.Keys, Is.EquivalentTo(new[] { "total" }));
        repository.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(0.001)]
    [TestCase(10.005)]
    [TestCase(1_000_000.01)]
    [TestCase(2_000_000)]
    public void CreateAsync_TotalWithMoreThanTwoDecimalsOrAboveMax_ThrowsValidationErrorOnTotal(double total)
    {
        var request = new CreateOrderRequest(ValidCustomerId, "Keyboard", (decimal)total);

        var ex = Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request, CancellationToken.None));

        Assert.That(ex!.Errors.Keys, Is.EquivalentTo(new[] { "total" }));
        repository.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task CreateAsync_TotalEqualToMax_IsAccepted()
    {
        repository.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await service.CreateAsync(new CreateOrderRequest(ValidCustomerId, "Big order", OrderService.MaxTotal), CancellationToken.None);

        Assert.That(result.Total, Is.EqualTo(1_000_000m));
    }

    [Test]
    public async Task CreateAsync_TotalWithTwoDecimals_IsAccepted()
    {
        repository.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await service.CreateAsync(new CreateOrderRequest(ValidCustomerId, "Cents", 0.01m), CancellationToken.None);

        Assert.That(result.Total, Is.EqualTo(0.01m));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void CreateAsync_EmptyDescription_ThrowsValidationErrorOnDescriptionAndDoesNotPersist(string? description)
    {
        var request = new CreateOrderRequest(ValidCustomerId, description, 10m);

        var ex = Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request, CancellationToken.None));

        Assert.That(ex!.Errors.Keys, Is.EquivalentTo(new[] { "description" }));
        repository.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void CreateAsync_DescriptionLongerThan250_ThrowsValidationError()
    {
        var request = new CreateOrderRequest(ValidCustomerId, new string('x', 251), 10m);

        var ex = Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request, CancellationToken.None));

        Assert.That(ex!.Errors.Keys, Is.EquivalentTo(new[] { "description" }));
    }

    [Test]
    public async Task CreateAsync_DescriptionOfExactly250_IsAccepted()
    {
        repository.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await service.CreateAsync(new CreateOrderRequest(ValidCustomerId, new string('x', 250), 10m), CancellationToken.None);

        Assert.That(result.Description, Has.Length.EqualTo(250));
    }

    [Test]
    public void CreateAsync_EmptyCustomerId_ThrowsValidationErrorOnCustomerIdAndDoesNotPersist()
    {
        var request = new CreateOrderRequest(Guid.Empty, "Keyboard", 10m);

        var ex = Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request, CancellationToken.None));

        Assert.That(ex!.Errors.Keys, Is.EquivalentTo(new[] { "customerId" }));
        repository.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void CreateAsync_SeveralInvalidFields_ReportsAllOfThem()
    {
        var request = new CreateOrderRequest(Guid.Empty, "", 0m);

        var ex = Assert.ThrowsAsync<OrderValidationException>(() => service.CreateAsync(request, CancellationToken.None));

        Assert.That(ex!.Errors.Keys, Is.EquivalentTo(new[] { "customerId", "description", "total" }));
    }

    [Test]
    public async Task GetByIdAsync_ExistingOrder_ReturnsMappedOrder()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = ValidCustomerId,
            Description = "Monitor",
            Total = 300m,
            Status = OrderStatus.Shipped,
            CreatedAt = Now,
            UpdatedAt = Now.AddDays(1)
        };
        repository.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await service.GetByIdAsync(order.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(order.Id));
            Assert.That(result.Description, Is.EqualTo("Monitor"));
            Assert.That(result.Total, Is.EqualTo(300m));
            Assert.That(result.Status, Is.EqualTo(OrderStatus.Shipped));
            Assert.That(result.UpdatedAt, Is.EqualTo(Now.AddDays(1)));
        });
    }

    [Test]
    public void GetByIdAsync_UnknownOrder_ThrowsOrderNotFoundWithRequestedId()
    {
        var id = Guid.NewGuid();
        repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Order?)null);

        var ex = Assert.ThrowsAsync<OrderNotFoundException>(() => service.GetByIdAsync(id, CancellationToken.None));

        Assert.That(ex!.OrderId, Is.EqualTo(id));
    }

    [Test]
    public async Task CreateAsync_ValidRequest_PersistsExactlyOnePendingOrderThroughRepositoryWithCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        Order? persisted = null;
        repository.Setup(r => r.AddAsync(It.IsAny<Order>(), cts.Token))
            .Callback<Order, CancellationToken>((o, _) => persisted = o)
            .Returns(Task.CompletedTask);

        var result = await service.CreateAsync(new CreateOrderRequest(ValidCustomerId, "Mouse", 25m), cts.Token);

        repository.Verify(r => r.AddAsync(It.IsAny<Order>(), cts.Token), Times.Once);
        Assert.That(persisted, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(persisted!.Id, Is.EqualTo(result.Id));
            Assert.That(persisted.Status, Is.EqualTo(OrderStatus.Pending));
            Assert.That(persisted.CustomerId, Is.EqualTo(ValidCustomerId));
            Assert.That(persisted.CancellationReason, Is.Null);
            Assert.That(persisted.CancelledAt, Is.Null);
        });
    }

    [Test]
    public async Task GetAllAsync_ReturnsAllOrdersFromRepositoryInOrder()
    {
        var first = new Order { Id = Guid.NewGuid(), CustomerId = ValidCustomerId, Description = "A", Total = 1m, CreatedAt = Now };
        var second = new Order { Id = Guid.NewGuid(), CustomerId = ValidCustomerId, Description = "B", Total = 2m, CreatedAt = Now };
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);

        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.That(result.Select(o => o.Id), Is.EqualTo(new[] { first.Id, second.Id }));
    }
}
