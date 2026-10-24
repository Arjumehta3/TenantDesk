using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TenantDesk.Api.Tenancy;

/// <summary>
/// Resolves the tenant for every request (X-Tenant header, or the first label of the host name),
/// verifies the tenant's API key and stores the tenant in <see cref="TenantContext"/>.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    public const string TenantHeader = "X-Tenant";
    public const string ApiKeyHeader = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantStore store, TenantContext tenantContext)
    {
        if (IsPublicPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var tenantId = ResolveTenantId(context.Request);

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Tenant not specified",
                $"Send the '{TenantHeader}' header or use a tenant sub-domain such as acme.localhost.");
            return;
        }

        var tenant = store.Find(tenantId);
        var suppliedKey = context.Request.Headers[ApiKeyHeader].ToString();

        // Same answer for "unknown tenant" and "wrong key" so tenant names cannot be enumerated.
        if (tenant is null || !ApiKeyMatches(tenant.ApiKey, suppliedKey))
        {
            _logger.LogWarning("Rejected request for tenant {TenantId}: unknown tenant or bad API key", tenantId);

            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Unknown tenant or invalid API key.");
            return;
        }

        tenantContext.Current = tenant;

        // Every log line written while handling this request carries the tenant id.
        using (_logger.BeginScope(new Dictionary<string, object> { ["TenantId"] = tenant.Id }))
        {
            await _next(context);
        }
    }

    private static bool IsPublicPath(PathString path) =>
        path == "/" || path.StartsWithSegments("/health");

    private static string? ResolveTenantId(HttpRequest request)
    {
        var header = request.Headers[TenantHeader].ToString();

        if (!string.IsNullOrWhiteSpace(header))
        {
            return header.Trim();
        }

        var host = request.Host.Host;

        if (string.IsNullOrEmpty(host) || IPAddress.TryParse(host, out _))
        {
            return null;
        }

        var firstDot = host.IndexOf('.');

        return firstDot > 0 ? host[..firstDot] : null;
    }

    private static bool ApiKeyMatches(string expected, string supplied) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(supplied));

    private static Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var body = JsonSerializer.Serialize(new { title, status, detail });

        return context.Response.WriteAsync(body);
    }
}
