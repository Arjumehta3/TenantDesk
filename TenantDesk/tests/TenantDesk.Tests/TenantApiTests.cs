using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace TenantDesk.Tests;

internal sealed record TicketDto(int Id, string Title, string Status, string Priority);

/// <summary>
/// End-to-end tests against the real API pipeline (middleware, controllers, EF Core, SQLite).
/// xUnit creates a new instance of this class for every test, so each test starts with fresh databases.
/// </summary>
public sealed class TenantApiTests : IDisposable
{
    private readonly TenantDeskFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient ClientFor(string tenant, string apiKey)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", tenant);
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    private static async Task<TicketDto> CreateTicketAsync(HttpClient client, string title, string priority = "Medium")
    {
        var response = await client.PostAsJsonAsync("/api/tickets", new { title, description = "details", priority });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var ticket = await response.Content.ReadFromJsonAsync<TicketDto>();
        Assert.NotNull(ticket);
        return ticket!;
    }

    private static Task<HttpResponseMessage> ChangeStatusAsync(HttpClient client, int id, string status)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/tickets/{id}/status")
        {
            Content = JsonContent.Create(new { status })
        };

        return client.SendAsync(request);
    }

    [Fact]
    public async Task Health_endpoint_does_not_require_a_tenant()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Request_without_a_tenant_is_rejected_with_400()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_tenant_is_rejected_with_401()
    {
        using var client = ClientFor("initech", "whatever");

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_api_key_is_rejected_with_401()
    {
        using var client = ClientFor("acme", "not-the-right-key");

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task One_tenants_key_does_not_open_another_tenants_data()
    {
        using var client = ClientFor("globex", TenantDeskFactory.AcmeKey);

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_can_be_resolved_from_the_sub_domain()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Host = "acme.localhost";
        client.DefaultRequestHeaders.Add("X-Api-Key", TenantDeskFactory.AcmeKey);

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_ticket_created_by_one_tenant_is_invisible_to_another_tenant()
    {
        using var acme = ClientFor("acme", TenantDeskFactory.AcmeKey);
        using var globex = ClientFor("globex", TenantDeskFactory.GlobexKey);

        var ticket = await CreateTicketAsync(acme, "Acme confidential ticket");

        var acmeTickets = await acme.GetFromJsonAsync<List<TicketDto>>("/api/tickets");
        Assert.Contains(acmeTickets!, t => t.Id == ticket.Id);

        var globexTickets = await globex.GetFromJsonAsync<List<TicketDto>>("/api/tickets");
        Assert.Empty(globexTickets!);

        var globexLookup = await globex.GetAsync($"/api/tickets/{ticket.Id}");
        Assert.Equal(HttpStatusCode.NotFound, globexLookup.StatusCode);
    }

    [Fact]
    public async Task Each_tenant_gets_its_own_database()
    {
        using var acme = ClientFor("acme", TenantDeskFactory.AcmeKey);
        await CreateTicketAsync(acme, "Triggers database use");

        var files = Directory.GetFiles(_factory.DataDirectory, "*.db")
                             .Select(f => new FileInfo(f).Name)
                             .ToList();

        Assert.Contains("tenantdesk_acme.db", files);
        Assert.Contains("tenantdesk_globex.db", files);
    }

    [Fact]
    public async Task Creating_a_ticket_with_an_empty_title_returns_400()
    {
        using var acme = ClientFor("acme", TenantDeskFactory.AcmeKey);

        var response = await acme.PostAsJsonAsync("/api/tickets", new { title = "", priority = "Low" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_ticket_returns_201_with_a_location_header()
    {
        using var acme = ClientFor("acme", TenantDeskFactory.AcmeKey);

        var response = await acme.PostAsJsonAsync("/api/tickets", new { title = "Reset my password", priority = "High" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Tickets_can_be_filtered_by_status()
    {
        using var acme = ClientFor("acme", TenantDeskFactory.AcmeKey);

        var first = await CreateTicketAsync(acme, "Stays open");
        var second = await CreateTicketAsync(acme, "Gets resolved");

        var change = await ChangeStatusAsync(acme, second.Id, "Resolved");
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        var open = await acme.GetFromJsonAsync<List<TicketDto>>("/api/tickets?status=Open");

        Assert.Single(open!);
        Assert.Equal(first.Id, open![0].Id);
    }

    [Fact]
    public async Task A_closed_ticket_cannot_be_reopened_and_returns_409()
    {
        using var acme = ClientFor("acme", TenantDeskFactory.AcmeKey);
        var ticket = await CreateTicketAsync(acme, "Will be closed");

        var close = await ChangeStatusAsync(acme, ticket.Id, "Closed");
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);

        var reopen = await ChangeStatusAsync(acme, ticket.Id, "Open");

        Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);
    }

    [Fact]
    public async Task Changing_the_status_of_a_missing_ticket_returns_404()
    {
        using var acme = ClientFor("acme", TenantDeskFactory.AcmeKey);

        var response = await ChangeStatusAsync(acme, 9999, "Closed");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
