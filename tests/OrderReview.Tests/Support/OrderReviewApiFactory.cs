using Microsoft.Extensions.Hosting;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrderReview.Api.Data;
using OrderReview.Api.Models;

namespace OrderReview.Tests.Support;

/// <summary>
/// Boots the real API against an isolated, named in-memory SQLite database and a
/// test-only JWT signing key. Nothing here depends on user-secrets or on the dev .db file.
/// </summary>
public sealed class OrderReviewApiFactory : WebApplicationFactory<Program>
{
    public const string ValidIssuer = Issuer;
    public const string ValidAudience = Audience;
    private const string Issuer = "order-review-tests";
    private const string Audience = "order-review-api-tests";

    // Test-only key, generated per factory instance; never a real secret.
    private readonly byte[] signingKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    private readonly string connectionString = $"Data Source=file:orderreview_{Guid.NewGuid():N}?mode=memory&cache=shared";
    private SqliteConnection? keepAliveConnection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" (not Development): no auto-migration or demo seeding, tests own their data.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:OrderReview", connectionString);
        builder.UseSetting("Authentication:Schemes:Bearer:ValidIssuer", Issuer);
        builder.UseSetting("Authentication:Schemes:Bearer:ValidAudiences:0", Audience);
        builder.UseSetting("Authentication:Schemes:Bearer:SigningKeys:0:Issuer", Issuer);
        builder.UseSetting("Authentication:Schemes:Bearer:SigningKeys:0:Value", Convert.ToBase64String(signingKey));
        builder.UseSetting("Authentication:Schemes:Bearer:SigningKeys:0:Length", signingKey.Length.ToString());
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // The shared in-memory database lives only while at least one connection is open.
        keepAliveConnection = new SqliteConnection(connectionString);
        keepAliveConnection.Open();

        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
        return host;
    }

    public HttpClient CreateClientWithScopes(params string[] scopes)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(scopes));
        return client;
    }

    public string CreateToken(params string[] scopes) => CreateToken(scopes, Issuer, Audience, DateTime.UtcNow.AddMinutes(10));

    public string CreateToken(string[] scopes, string issuer, string audience, DateTime expires)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            NotBefore = expires.AddHours(-2),
            Expires = expires,
            Subject = new ClaimsIdentity([new Claim("sub", "test-user"), new Claim("scope", string.Join(' ', scopes))]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(signingKey), SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public async Task<Order> SeedOrderAsync(string description, decimal total, OrderStatus status, DateTimeOffset createdAt)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Description = description,
            Total = total,
            Status = status,
            CreatedAt = createdAt
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    public async Task<Order?> FindOrderAsync(Guid id)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<int> CountOrdersAsync()
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.CountAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            keepAliveConnection?.Dispose();
    }
}
