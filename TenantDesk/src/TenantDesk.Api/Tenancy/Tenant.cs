namespace TenantDesk.Api.Tenancy;

public class Tenant
{
    /// <summary>Lower-case slug, e.g. "acme". Also used in SQLite file names, so it is validated strictly.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Connection string of this tenant's own database (SQL Server mode).</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Secret the tenant must send in the X-Api-Key header.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
