using System.ComponentModel.DataAnnotations;
using TenantDesk.Api.Domain;

namespace TenantDesk.Api.Contracts;

public sealed class CreateTicketRequest
{
    [Required]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
}

public sealed class ChangeStatusRequest
{
    [Required]
    public TicketStatus? Status { get; set; }
}

public sealed record TicketResponse(
    int Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static TicketResponse From(Ticket ticket) => new(
        ticket.Id,
        ticket.Title,
        ticket.Description,
        ticket.Status,
        ticket.Priority,
        ticket.CreatedAtUtc,
        ticket.UpdatedAtUtc);
}
