using System.Text.Json.Serialization;

namespace DoneYet.Models;

public class TodoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";

    /// <summary>Local time. Null = general todo with no deadline.</summary>
    public DateTime? Deadline { get; set; }

    public RecurrenceKind Recurrence { get; set; } = RecurrenceKind.None;

    /// <summary>Interval for EveryNDays.</summary>
    public int RecurrenceN { get; set; } = 3;

    /// <summary>Day-of-month anchor for Monthly recurrence (so "due the 31st" doesn't drift after February).</summary>
    public int RecurrenceAnchorDay { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Set only when the item is confirmed complete and archived. Recurring items never set this; they roll forward.</summary>
    public DateTime? CompletedAt { get; set; }

    public DateTime? SnoozedUntil { get; set; }
    public DateTime? LastNotifiedAt { get; set; }

    /// <summary>How many cycles a recurring item has been completed.</summary>
    public int TimesCompleted { get; set; }
    public DateTime? LastCompletedAt { get; set; }

    [JsonIgnore]
    public bool IsDone => CompletedAt != null;

    [JsonIgnore]
    public bool IsSnoozed => SnoozedUntil.HasValue && SnoozedUntil.Value > DateTime.Now;

    [JsonIgnore]
    public bool IsRecurring => Recurrence != RecurrenceKind.None;
}
