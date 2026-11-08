using Microsoft.AspNetCore.Authorization;

namespace OrderReview.Api.Security;

/// <summary>
/// Policies are satisfied by the space-delimited OAuth2 "scope" claim
/// (what `dotnet user-jwts create --scope ...` emits).
/// </summary>
public static class AuthorizationPolicies
{
    public const string OrdersRead = "Orders.Read";
    public const string OrdersCreate = "Orders.Create";

    public const string ReadScope = "orders:read";
    public const string CreateScope = "orders:create";

    public static AuthorizationBuilder AddOrderPolicies(this AuthorizationBuilder builder) => builder
        .AddPolicy(OrdersRead, policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => HasScope(ctx, ReadScope)))
        .AddPolicy(OrdersCreate, policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => HasScope(ctx, CreateScope)));

    private static bool HasScope(AuthorizationHandlerContext context, string scope) =>
        context.User.FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
}
