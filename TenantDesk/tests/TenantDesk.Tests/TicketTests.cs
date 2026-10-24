using TenantDesk.Api.Domain;
using Xunit;

namespace TenantDesk.Tests;

public class TicketTests
{
    [Fact]
    public void New_ticket_starts_open_and_trims_input()
    {
        var ticket = Ticket.Create("  Printer on fire  ", "  3rd floor  ", TicketPriority.High);

        Assert.Equal("Printer on fire", ticket.Title);
        Assert.Equal("3rd floor", ticket.Description);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(ticket.CreatedAtUtc, ticket.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Creating_a_ticket_with_a_blank_title_is_rejected(string title)
    {
        Assert.Throws<ArgumentException>(() => Ticket.Create(title, null, TicketPriority.Low));
    }

    [Theory]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void An_open_ticket_can_move_to_any_later_status(TicketStatus next)
    {
        var ticket = Ticket.Create("Login broken", null, TicketPriority.Medium);

        var changed = ticket.TryChangeStatus(next, out var error);

        Assert.True(changed);
        Assert.Null(error);
        Assert.Equal(next, ticket.Status);
        Assert.True(ticket.UpdatedAtUtc >= ticket.CreatedAtUtc);
    }

    [Fact]
    public void A_closed_ticket_cannot_be_reopened()
    {
        var ticket = Ticket.Create("Login broken", null, TicketPriority.Medium);
        ticket.TryChangeStatus(TicketStatus.Closed, out _);

        var changed = ticket.TryChangeStatus(TicketStatus.Open, out var error);

        Assert.False(changed);
        Assert.NotNull(error);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }
}
