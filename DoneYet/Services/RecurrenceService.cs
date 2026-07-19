using DoneYet.Models;

namespace DoneYet.Services;

public static class RecurrenceService
{
    public static string Describe(TodoItem t) => t.Recurrence switch
    {
        RecurrenceKind.Daily => "daily",
        RecurrenceKind.Weekly => "weekly",
        RecurrenceKind.Monthly => "monthly",
        RecurrenceKind.Yearly => "yearly",
        RecurrenceKind.EveryNDays => $"every {t.RecurrenceN}d",
        _ => "",
    };

    /// <summary>
    /// Completes one cycle of a recurring item: rolls the deadline forward past 'now'
    /// (skipped cycles collapse into one) and records the completion.
    /// </summary>
    public static void CompleteCycle(TodoItem t, DateTime now)
    {
        t.TimesCompleted++;
        t.LastCompletedAt = now;
        t.SnoozedUntil = null;
        t.LastNotifiedAt = null;

        if (t.Deadline is not DateTime due) return;
        var next = due;
        do { next = NextOccurrence(t, next); }
        while (next <= now);
        t.Deadline = next;
    }

    public static DateTime NextOccurrence(TodoItem t, DateTime from) => t.Recurrence switch
    {
        RecurrenceKind.Daily => from.AddDays(1),
        RecurrenceKind.Weekly => from.AddDays(7),
        RecurrenceKind.Monthly => AddMonthAnchored(from, t.RecurrenceAnchorDay),
        RecurrenceKind.Yearly => from.AddYears(1),
        RecurrenceKind.EveryNDays => from.AddDays(Math.Max(1, t.RecurrenceN)),
        _ => from,
    };

    /// <summary>
    /// Adds one month keeping the original day-of-month anchor: a bill due the 31st
    /// goes 31 → Feb 28 → Mar 31, instead of drifting to the 28th forever.
    /// </summary>
    private static DateTime AddMonthAnchored(DateTime from, int anchorDay)
    {
        if (anchorDay < 1) anchorDay = from.Day;
        var firstOfNext = new DateTime(from.Year, from.Month, 1).AddMonths(1);
        int day = Math.Min(anchorDay, DateTime.DaysInMonth(firstOfNext.Year, firstOfNext.Month));
        return new DateTime(firstOfNext.Year, firstOfNext.Month, day, from.Hour, from.Minute, 0);
    }
}
