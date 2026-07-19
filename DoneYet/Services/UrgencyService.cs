using DoneYet.Models;

namespace DoneYet.Services;

public static class UrgencyService
{
    public static UrgencyTier GetTier(TodoItem t, DateTime now)
    {
        if (t.Deadline is not DateTime d) return UrgencyTier.None;
        var delta = d - now;
        if (delta < TimeSpan.Zero) return UrgencyTier.Overdue;
        if (delta <= TimeSpan.FromHours(24)) return UrgencyTier.Urgent;
        if (delta <= TimeSpan.FromDays(3)) return UrgencyTier.Soon;
        if (delta <= TimeSpan.FromDays(7)) return UrgencyTier.Week;
        return UrgencyTier.Later;
    }

    public static Color TierColor(UrgencyTier tier) => tier switch
    {
        UrgencyTier.Overdue => Color.FromArgb(226, 84, 84),    // red
        UrgencyTier.Urgent => Color.FromArgb(233, 138, 62),    // orange
        UrgencyTier.Soon => Color.FromArgb(226, 184, 76),      // yellow
        UrgencyTier.Week => Color.FromArgb(86, 148, 217),      // blue
        UrgencyTier.Later => Color.FromArgb(96, 176, 128),     // green
        _ => Color.FromArgb(128, 133, 140),                    // gray — no deadline
    };

    /// <summary>Short human text like "due in 3d (Jul 22)" or "OVERDUE 5d".</summary>
    public static string DueText(TodoItem t, DateTime now)
    {
        if (t.Deadline is not DateTime d) return "no deadline";
        var delta = d - now;

        if (delta < TimeSpan.Zero)
        {
            var over = now - d;
            if (over.TotalHours < 1) return "OVERDUE " + Math.Max(1, (int)over.TotalMinutes) + "m";
            if (over.TotalHours < 24) return "OVERDUE " + (int)over.TotalHours + "h";
            return "OVERDUE " + (int)over.TotalDays + "d";
        }

        if (delta.TotalMinutes < 60) return "due in " + Math.Max(1, (int)delta.TotalMinutes) + "m";
        if (delta.TotalHours < 24) return "due in " + (int)Math.Ceiling(delta.TotalHours) + "h";
        if (d.Date == now.Date.AddDays(1)) return "due tomorrow " + d.ToString("h:mm tt");
        return $"due in {(int)Math.Ceiling(delta.TotalDays)}d ({d:MMM d})";
    }

    /// <summary>Worst tier among open, non-snoozed items.</summary>
    public static UrgencyTier WorstTier(IEnumerable<TodoItem> items, DateTime now)
    {
        var worst = UrgencyTier.None;
        foreach (var t in items)
        {
            var tier = GetTier(t, now);
            if (tier > worst) worst = tier;
        }
        return worst;
    }

    /// <summary>Sort for display: overdue first, then by deadline, no-deadline items last (newest first).</summary>
    public static List<TodoItem> SortForDisplay(IEnumerable<TodoItem> items, DateTime now)
    {
        return items
            .OrderByDescending(t => GetTier(t, now) == UrgencyTier.Overdue)
            .ThenBy(t => t.Deadline ?? DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedAt)
            .ToList();
    }

    public static string SummaryLine(IReadOnlyList<TodoItem> open, DateTime now)
    {
        int overdue = open.Count(t => GetTier(t, now) == UrgencyTier.Overdue);
        int today = open.Count(t => GetTier(t, now) == UrgencyTier.Urgent);
        int soon = open.Count(t => GetTier(t, now) == UrgencyTier.Soon);

        var parts = new List<string>();
        if (overdue > 0) parts.Add($"{overdue} overdue");
        if (today > 0) parts.Add($"{today} due today");
        if (soon > 0) parts.Add($"{soon} due soon");
        parts.Add($"{open.Count} open");
        return string.Join(" · ", parts);
    }
}
