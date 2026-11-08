using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderReview.Api.Models;
using OrderReview.Api.Security;
using OrderReview.Tests.Support;

namespace OrderReview.Tests.Controllers;

/// <summary>HTTP-level tests of OrdersController: real pipeline, auth, EF Core and SQLite.</summary>
[TestFixture]
public class OrdersApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private OrderReviewApiFactory factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => factory = new OrderReviewApiFactory();

    [OneTimeTearDown]
    public void OneTimeTearDown() => factory.Dispose();

    private HttpClient ReaderClient() => factory.CreateClientWithScopes(AuthorizationPolicies.ReadScope);
    private HttpClient CreatorClient() => factory.CreateClientWithScopes(AuthorizationPolicies.CreateScope);

    private sealed record OrderDto(Guid Id, Guid CustomerId, string Description, decimal Total, OrderStatus Status,
        DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    // ---- authentication / authorization ----

    [Test]
    public async Task GetOrders_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/orders");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PostOrders_WithoutToken_Returns401AndPersistsNothing()
    {
        var before = await factory.CountOrdersAsync();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/orders",
            new { customerId = Guid.NewGuid(), description = "x", total = 10 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(await factory.CountOrdersAsync(), Is.EqualTo(before));
    }

    [Test]
    public async Task GetOrders_WithTokenLackingReadScope_Returns403()
    {
        var response = await CreatorClient().GetAsync("/api/orders");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task PostOrders_WithTokenLackingCreateScope_Returns403AndPersistsNothing()
    {
        var before = await factory.CountOrdersAsync();

        var response = await ReaderClient().PostAsJsonAsync("/api/orders",
            new { customerId = Guid.NewGuid(), description = "x", total = 10 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await factory.CountOrdersAsync(), Is.EqualTo(before));
    }

    [Test]
    public async Task GetOrders_WithTokenSignedByAnotherKey_Returns401()
    {
        using var otherFactory = new OrderReviewApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", otherFactory.CreateToken(AuthorizationPolicies.ReadScope));

        var response = await client.GetAsync("/api/orders");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private async Task<HttpStatusCode> GetOrdersWithTokenAsync(string token)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return (await client.GetAsync("/api/orders")).StatusCode;
    }

    [Test]
    public async Task GetOrders_TokenWithWrongIssuer_Returns401()
    {
        var token = factory.CreateToken([AuthorizationPolicies.ReadScope], "evil-issuer", OrderReviewApiFactory.ValidAudience, DateTime.UtcNow.AddMinutes(10));

        Assert.That(await GetOrdersWithTokenAsync(token), Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetOrders_TokenWithWrongAudience_Returns401()
    {
        var token = factory.CreateToken([AuthorizationPolicies.ReadScope], OrderReviewApiFactory.ValidIssuer, "another-api", DateTime.UtcNow.AddMinutes(10));

        Assert.That(await GetOrdersWithTokenAsync(token), Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetOrders_ExpiredToken_Returns401()
    {
        var token = factory.CreateToken([AuthorizationPolicies.ReadScope], OrderReviewApiFactory.ValidIssuer, OrderReviewApiFactory.ValidAudience, DateTime.UtcNow.AddHours(-1));

        Assert.That(await GetOrdersWithTokenAsync(token), Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Orders_TokenWithLookalikeScopes_Returns403()
    {
        using var client = factory.CreateClientWithScopes("orders:readx", "orders:createx");

        var list = await client.GetAsync("/api/orders");
        var post = await client.PostAsJsonAsync("/api/orders", new { customerId = Guid.NewGuid(), description = "x", total = 10 });

        Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(post.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    // ---- GET ----

    [Test]
    public async Task GetOrders_WithReadScope_Returns200WithOrdersNewestFirst()
    {
        var older = await factory.SeedOrderAsync("older order", 10.50m, OrderStatus.Pending, new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var newer = await factory.SeedOrderAsync("newer order", 99.99m, OrderStatus.Shipped, new DateTimeOffset(2030, 6, 1, 0, 0, 0, TimeSpan.Zero));

        var response = await ReaderClient().GetAsync("/api/orders");
        var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>(Json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ids = orders!.Select(o => o.Id).ToList();
        Assert.That(ids, Does.Contain(older.Id).And.Contain(newer.Id));
        Assert.That(ids.IndexOf(newer.Id), Is.LessThan(ids.IndexOf(older.Id)), "newest first");
        var returned = orders!.Single(o => o.Id == newer.Id);
        Assert.Multiple(() =>
        {
            Assert.That(returned.Description, Is.EqualTo("newer order"));
            Assert.That(returned.Total, Is.EqualTo(99.99m));
            Assert.That(returned.Status, Is.EqualTo(OrderStatus.Shipped));
        });
    }

    [Test]
    public async Task GetOrders_SerializesStatusAsString()
    {
        await factory.SeedOrderAsync("status check", 5m, OrderStatus.Processing, DateTimeOffset.UtcNow);

        var body = await ReaderClient().GetStringAsync("/api/orders");

        Assert.That(body, Does.Contain("\"status\":\"Processing\""));
    }

    [Test]
    public async Task GetOrderById_ExistingOrder_Returns200WithOrder()
    {
        var seeded = await factory.SeedOrderAsync("find me", 42.10m, OrderStatus.Delivered, new DateTimeOffset(2031, 2, 3, 4, 5, 6, TimeSpan.Zero));

        var response = await ReaderClient().GetAsync($"/api/orders/{seeded.Id}");
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(Json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.Multiple(() =>
        {
            Assert.That(order!.Id, Is.EqualTo(seeded.Id));
            Assert.That(order.CustomerId, Is.EqualTo(seeded.CustomerId));
            Assert.That(order.Description, Is.EqualTo("find me"));
            Assert.That(order.Total, Is.EqualTo(42.10m));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Delivered));
            Assert.That(order.CreatedAt, Is.EqualTo(seeded.CreatedAt));
        });
    }

    [Test]
    public async Task GetOrderById_UnknownId_Returns404ProblemDetails()
    {
        var response = await ReaderClient().GetAsync($"/api/orders/{Guid.NewGuid()}");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        Assert.That(problem.GetProperty("status").GetInt32(), Is.EqualTo(404));
    }

    [Test]
    public async Task GetOrderById_NonGuidId_Returns404()
    {
        var response = await ReaderClient().GetAsync("/api/orders/not-a-guid");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ---- POST ----

    [Test]
    public async Task PostOrders_ValidRequest_Returns201WithLocationAndPersistsPendingOrder()
    {
        var customerId = Guid.NewGuid();

        var response = await CreatorClient().PostAsJsonAsync("/api/orders",
            new { customerId, description = "  New keyboard ", total = 59.90m });
        var created = await response.Content.ReadFromJsonAsync<OrderDto>(Json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Not.Null);
        Assert.That(response.Headers.Location!.AbsolutePath, Is.EqualTo($"/api/orders/{created!.Id}"));
        Assert.Multiple(() =>
        {
            Assert.That(created.Status, Is.EqualTo(OrderStatus.Pending));
            Assert.That(created.Description, Is.EqualTo("New keyboard"));
            Assert.That(created.CustomerId, Is.EqualTo(customerId));
        });

        var persisted = await factory.FindOrderAsync(created.Id);
        Assert.That(persisted, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(persisted!.Status, Is.EqualTo(OrderStatus.Pending));
            Assert.That(persisted.Total, Is.EqualTo(59.90m));
            Assert.That(persisted.Description, Is.EqualTo("New keyboard"));
            Assert.That(persisted.CustomerId, Is.EqualTo(customerId));
            Assert.That(persisted.CancellationReason, Is.Null);
        });
    }

    [Test]
    public async Task PostOrders_LocationHeader_PointsToReadableOrder()
    {
        var create = await CreatorClient().PostAsJsonAsync("/api/orders",
            new { customerId = Guid.NewGuid(), description = "follow location", total = 12m });

        var follow = await ReaderClient().GetAsync(create.Headers.Location);

        Assert.That(follow.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task PostOrders_InvalidRequest_Returns400ValidationProblemAndPersistsNothing()
    {
        var before = await factory.CountOrdersAsync();

        var response = await CreatorClient().PostAsJsonAsync("/api/orders",
            new { customerId = Guid.Empty, description = "", total = 0 });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var errors = problem.GetProperty("errors");
        Assert.Multiple(() =>
        {
            Assert.That(errors.TryGetProperty("customerId", out _), Is.True);
            Assert.That(errors.TryGetProperty("description", out _), Is.True);
            Assert.That(errors.TryGetProperty("total", out _), Is.True);
        });
        Assert.That(await factory.CountOrdersAsync(), Is.EqualTo(before));
    }

    [Test]
    public async Task PostOrders_MissingDescription_Returns400FromServiceRules()
    {
        var response = await CreatorClient().PostAsJsonAsync("/api/orders",
            new { customerId = Guid.NewGuid(), total = 10 });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(problem.GetProperty("errors").TryGetProperty("description", out _), Is.True);
    }

    [Test]
    public async Task PostOrders_DescriptionOver250_Returns400()
    {
        var response = await CreatorClient().PostAsJsonAsync("/api/orders",
            new { customerId = Guid.NewGuid(), description = new string('x', 251), total = 10 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostOrders_MalformedJson_Returns400()
    {
        var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await CreatorClient().PostAsync("/api/orders", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostOrders_WrongTypeForCustomerId_Returns400WithoutInternalTypeNames()
    {
        var content = new StringContent("{\"customerId\":\"zz\",\"description\":\"x\",\"total\":10}", System.Text.Encoding.UTF8, "application/json");

        var response = await CreatorClient().PostAsync("/api/orders", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(body, Does.Not.Contain("OrderReview.Api"));
    }

    [Test]
    public async Task PostOrders_TotalWithThreeDecimals_Returns400OnTotal()
    {
        var response = await CreatorClient().PostAsJsonAsync("/api/orders",
            new { customerId = Guid.NewGuid(), description = "x", total = 1.001m });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(problem.GetProperty("errors").TryGetProperty("total", out _), Is.True);
    }

    // ---- no cancellation / modification surface in the baseline ----

    [TestCase("POST", "cancel")]
    [TestCase("PUT", "")]
    [TestCase("DELETE", "")]
    [TestCase("PATCH", "")]
    public async Task OrderModificationRoutes_DoNotExist(string method, string suffix)
    {
        var order = await factory.SeedOrderAsync("untouchable", 20m, OrderStatus.Pending, DateTimeOffset.UtcNow);
        using var client = factory.CreateClientWithScopes(AuthorizationPolicies.ReadScope, AuthorizationPolicies.CreateScope);
        var path = string.IsNullOrEmpty(suffix) ? $"/api/orders/{order.Id}" : $"/api/orders/{order.Id}/{suffix}";

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { reason = "changed my mind" })
        });

        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed));
        var persisted = await factory.FindOrderAsync(order.Id);
        Assert.That(persisted!.Status, Is.EqualTo(OrderStatus.Pending));
    }

    // ---- error handling ----

    [Test]
    public async Task UnhandledException_Returns500WithoutInternalDetails()
    {
        // Break persistence by using a factory whose database has no schema.
        using var broken = new BrokenDatabaseFactory();
        using var client = broken.CreateClientWithScopes(AuthorizationPolicies.ReadScope);

        var response = await client.GetAsync("/api/orders");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(body, Does.Contain("An unexpected error occurred."));
        Assert.That(body, Does.Not.Contain("no such table").And.Not.Contain("SqliteException").And.Not.Contain("Orders"));
    }

    private sealed class BrokenDatabaseFactory : IDisposable
    {
        private readonly OrderReviewApiFactory inner = new();
        public BrokenDatabaseFactory()
        {
            // Drop the table so the next query fails with a provider exception.
            using var scope = inner.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Api.Data.AppDbContext>();
            db.Database.ExecuteSqlRaw("DROP TABLE \"Orders\"");
        }
        public HttpClient CreateClientWithScopes(params string[] scopes) => inner.CreateClientWithScopes(scopes);
        public void Dispose() => inner.Dispose();
    }
}
