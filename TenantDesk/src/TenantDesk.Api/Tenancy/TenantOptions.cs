namespace TenantDesk.Api.Tenancy;

public class TenantOptions
{
    public const string SectionName = "Tenancy";

    public List<Tenant> Tenants { get; set; } = new();
}
