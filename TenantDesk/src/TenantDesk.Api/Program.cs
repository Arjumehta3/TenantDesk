using System.Text.Json.Serialization;
using TenantDesk.Api.Data;
using TenantDesk.Api.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<TenantOptions>(builder.Configuration.GetSection(TenantOptions.SectionName));

// Tenancy: who is calling (singleton catalogue + one TenantContext per request).
builder.Services.AddSingleton<ITenantStore, ConfigurationTenantStore>();
builder.Services.AddScoped<TenantContext>();

// Data: a DbContext per request, wired to the database of the current tenant.
builder.Services.AddSingleton<TenantDbContextFactory>();
builder.Services.AddScoped(sp =>
{
    var tenant = sp.GetRequiredService<TenantContext>().Current
        ?? throw new InvalidOperationException("No tenant has been resolved for this request.");

    return sp.GetRequiredService<TenantDbContextFactory>().Create(tenant);
});

// Creates the database of every tenant on start-up.
builder.Services.AddHostedService<TenantDatabaseInitializer>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

var app = builder.Build();

app.UseMiddleware<TenantResolutionMiddleware>();

app.MapGet("/", () => Results.Ok(new
{
    name = "TenantDesk Lite",
    description = "Multi-tenant helpdesk API (database per tenant)",
    usage = "Send X-Tenant and X-Api-Key headers. See README.md"
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapControllers();

app.Run();

// Makes the entry point visible to WebApplicationFactory in the test project.
public partial class Program
{
}
