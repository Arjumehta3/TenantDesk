using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace TenantDesk.Api.Tenancy;

/// <summary>
/// Reads the tenant catalogue from configuration (appsettings.json).
/// In a real system this would be a catalogue database or a service.
/// </summary>
public sealed class ConfigurationTenantStore : ITenantStore
{
    private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9-]{0,49}$", RegexOptions.Compiled);

    private readonly Dictionary<string, Tenant> _tenants;

    public ConfigurationTenantStore(IOptions<TenantOptions> options)
    {
        var tenants = options.Value.Tenants;

        foreach (var tenant in tenants)
        {
            Validate(tenant);
        }

        _tenants = tenants.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<Tenant> All => _tenants.Values;

    public Tenant? Find(string tenantId) =>
        _tenants.TryGetValue(tenantId, out var tenant) ? tenant : null;

    private static void Validate(Tenant tenant)
    {
        if (!IdPattern.IsMatch(tenant.Id))
        {
            throw new InvalidOperationException(
                $"Invalid tenant id '{tenant.Id}'. Use lower-case letters, digits and '-' only (max 50 chars).");
        }

        if (string.IsNullOrWhiteSpace(tenant.ApiKey))
        {
            throw new InvalidOperationException($"Tenant '{tenant.Id}' has no ApiKey configured.");
        }
    }
}
