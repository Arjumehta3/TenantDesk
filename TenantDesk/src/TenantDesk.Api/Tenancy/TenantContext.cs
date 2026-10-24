namespace TenantDesk.Api.Tenancy;

/// <summary>
/// Holds the tenant of the current HTTP request. Registered as a scoped service,
/// so every request gets its own instance.
/// </summary>
public sealed class TenantContext
{
    public Tenant? Current { get; set; }
}
