namespace TenantDesk.Api.Domain;

public enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}

public enum TicketPriority
{
    Low,
    Medium,
    High
}

public class Ticket
{
    // Used by EF Core when materialising rows from the database.
    private Ticket()
    {
    }

    public int Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public TicketPriority Priority { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static Ticket Create(string title, string? description, TicketPriority priority)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        var now = DateTime.UtcNow;

        return new Ticket
        {
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Priority = priority,
            Status = TicketStatus.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    /// <summary>
    /// Business rule: a closed ticket is final and cannot be reopened.
    /// </summary>
    public bool TryChangeStatus(TicketStatus newStatus, out string? error)
    {
        if (Status == TicketStatus.Closed && newStatus != TicketStatus.Closed)
        {
            error = "A closed ticket cannot be reopened.";
            return false;
        }

        error = null;

        if (newStatus != Status)
        {
            Status = newStatus;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        return true;
    }
}
