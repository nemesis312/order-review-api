using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OrderReview.Api.Data;
using OrderReview.Api.Errors;
using OrderReview.Api.Repositories;
using OrderReview.Api.Security;
using OrderReview.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Malformed JSON 400s must not leak internal type names (e.g. OrderReview.Api.DTOs...).
        o.AllowInputFormatterExceptionMessages = false;
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(ResolveConnectionString(builder.Configuration, builder.Environment.ContentRootPath)));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

// Reads the standard "Authentication:Schemes:Bearer" section (ValidIssuer, ValidAudiences,
// SigningKeys). `dotnet user-jwts` writes the signing key to user-secrets, not to the repo.
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorizationBuilder().AddOrderPolicies();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "OrderReview API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT from `dotnet user-jwts create`. Paste the token only (no 'Bearer ' prefix)."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Development only: create/upgrade the database, then load deterministic demo data.
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);
    }
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// SQLite relative paths are resolved against the project folder so that `dotnet run`,
// `dotnet ef` and the IDE all use the same .db file regardless of the working directory.
static string ResolveConnectionString(IConfiguration configuration, string contentRoot)
{
    var raw = configuration.GetConnectionString("OrderReview")
        ?? throw new InvalidOperationException("Connection string 'OrderReview' is not configured.");

    var csb = new SqliteConnectionStringBuilder(raw);
    var isFileDb = csb.Mode != SqliteOpenMode.Memory
        && !csb.DataSource.Contains(":memory:", StringComparison.OrdinalIgnoreCase)
        && !csb.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase);

    if (isFileDb && !Path.IsPathRooted(csb.DataSource))
        csb.DataSource = Path.Combine(contentRoot, csb.DataSource);

    return csb.ToString();
}

public partial class Program;
