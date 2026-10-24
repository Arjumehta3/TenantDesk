using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TenantDesk.Api.Data;
using TenantDesk.Api.Tenancy;

namespace TenantDesk.Tests;

/// <summary>
/// Starts the real API in memory with two tenants (acme, globex).
/// Uses SQLite files in a temp folder, so the tests need neither Docker nor SQL Server.
/// Every instance gets its own empty databases.
/// </summary>
public sealed class TenantDeskFactory : WebApplicationFactory<Program>
{
    public const string AcmeKey = "acme-test-key";
    public const string GlobexKey = "globex-test-key";

    public string DataDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "tenantdesk-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<DatabaseOptions>(options =>
            {
                options.Provider = "Sqlite";
                options.SqliteDirectory = DataDirectory;
            });

            services.PostConfigure<TenantOptions>(options =>
            {
                options.Tenants = new List<Tenant>
                {
                    new() { Id = "acme", Name = "Acme Corp", ApiKey = AcmeKey },
                    new() { Id = "globex", Name = "Globex Ltd", ApiKey = GlobexKey }
                };
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        // SQLite keeps pooled connections (and file handles) open; release them before deleting.
        SqliteConnection.ClearAllPools();

        try
        {
            if (Directory.Exists(DataDirectory))
            {
                Directory.Delete(DataDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Temp files; the OS will clean them up eventually.
        }
    }
}
