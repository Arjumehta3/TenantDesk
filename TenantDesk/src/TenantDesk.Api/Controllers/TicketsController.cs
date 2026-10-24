using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TenantDesk.Api.Contracts;
using TenantDesk.Api.Data;
using TenantDesk.Api.Domain;

namespace TenantDesk.Api.Controllers;

/// <summary>
/// The controller never mentions a tenant. The injected <see cref="TenantDbContext"/> is already
/// connected to the current tenant's database, so cross-tenant access is impossible by construction.
/// </summary>
[ApiController]
[Route("api/tickets")]
public sealed class TicketsController : ControllerBase
{
    private readonly TenantDbContext _db;

    public TicketsController(TenantDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketResponse>>> List(
        [FromQuery] TicketStatus? status,
        CancellationToken cancellationToken)
    {
        IQueryable<Ticket> query = _db.Tickets.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(t => t.Status == status);
        }

        var tickets = await query
            .OrderByDescending(t => t.Id)
            .ToListAsync(cancellationToken);

        return Ok(tickets.Select(TicketResponse.From).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var ticket = await _db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (ticket is null)
        {
            return NotFound();
        }

        return Ok(TicketResponse.From(ticket));
    }

    [HttpPost]
    public async Task<ActionResult<TicketResponse>> Create(
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = Ticket.Create(request.Title, request.Description, request.Priority);

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, TicketResponse.From(ticket));
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<TicketResponse>> ChangeStatus(
        int id,
        ChangeStatusRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (ticket is null)
        {
            return NotFound();
        }

        if (!ticket.TryChangeStatus(request.Status!.Value, out var error))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Invalid status change",
                Detail = error
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(TicketResponse.From(ticket));
    }
}
