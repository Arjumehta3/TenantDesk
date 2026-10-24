namespace TenantDesk.Api.Tenancy;

public interface ITenantStore
{
    IReadOnlyCollection<Tenant> All { get; }

    Tenant? Find(string tenantId);
}
