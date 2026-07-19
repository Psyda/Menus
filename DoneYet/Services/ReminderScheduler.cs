using DoneYet.Data;
using DoneYet.Models;

namespace DoneYet.Services;

public sealed class NotificationRequest
{
    public string Title { get; init; } = "";
    public string Body { get; set; } = "";
    public UrgencyTier Tier { get; init; }
    /// <summary>True when this firing includes the baseline "general nag" (used to append extras like missing invoices).</summary>
    public bool BaselineIncluded { get; init; }
}

/// <summary>
/// Decides when a (dismissible) notification should fire.
/// Two layers:
///  1. Baseline cadence — the general nag for having open todos at all (every 4h / twice daily / etc).
///  2. Escalation — per-item reminders that speed up as a deadline approaches:
///     within 7 days: daily · within 3 days: every N h · within 24h: every N h · overdue: every N h.
/// At most ONE notification per tick — items are aggregated, never a toast-storm.
/// </summary>
public sealed class ReminderScheduler
{
    private readonly Store _store;

    public ReminderScheduler(Store store)
    {
        _store = store;
        // First run: anchor the baseline now, so installing the app doesn't instantly nag.
        if (_store.Settings.LastBaselineNotify == null)
        {
            _store.Settings.LastBaselineNotify = DateTime.Now;
            _store.SaveSettings(notify: false);
        }
    }

    public NotificationRequest? Tick(DateTime now)
    {
        var s = _store.Settings;
        if (!s.RemindersEnabled) return null;
        if (s.GlobalSnoozeUntil.HasValue && s.GlobalSnoozeUntil.Value > now) return null;
        if (InQuietHours(s, now)) return null; // deferred, not lost — fires after quiet hours end

        var open = _store.Todos
            .Where(t => !t.IsDone && !(t.SnoozedUntil.HasValue && t.SnoozedUntil.Value > now))
            .ToList();
        if (open.Count == 0) return null;

        // Layer 2: per-item escalation.
        var candidates = new List<TodoItem>();
        if (s.EscalationEnabled)
        {
            foreach (var t in open)
            {
                var interval = EscalationInterval(s, UrgencyService.GetTier(t, now));
                if (interval is not TimeSpan iv) continue;
                var last = t.LastNotifiedAt ?? t.CreatedAt;
                if (now - last >= iv) candidates.Add(t);
            }
        }

        // Layer 1: baseline cadence (only matters while something is open).
        bool baselineDue = now >= NextBaselineTime(s, s.LastBaselineNotify ?? now);

        if (candidates.Count == 0 && !baselineDue) return null;

        var display = UrgencyService.SortForDisplay(candidates.Count > 0 ? candidates : open, now);
        var shown = display.Take(4).ToList();

        var lines = shown.Select(t => "• " + Truncate(t.Title, 38) + " — " + UrgencyService.DueText(t, now));
        var body = string.Join("\n", lines);
        int more = (candidates.Count > 0 ? candidates.Count : open.Count) - shown.Count;
        if (more > 0) body += $"\n…and {more} more";

        var req = new NotificationRequest
        {
            Title = "DoneYet: " + UrgencyService.SummaryLine(open, now),
            Body = body,
            Tier = UrgencyService.WorstTier(open, now),
            BaselineIncluded = baselineDue,
        };

        // Stamp everything we nagged about so it doesn't re-fire next tick.
        foreach (var t in candidates.Concat(shown).Distinct()) t.LastNotifiedAt = now;
        if (baselineDue) s.LastBaselineNotify = now;
        _store.SaveTodos(notify: false);
        _store.SaveSettings(notify: false);

        return req;
    }

    private static TimeSpan? EscalationInterval(AppSettings s, UrgencyTier tier) => tier switch
    {
        UrgencyTier.Overdue => TimeSpan.FromHours(Math.Max(1, s.HoursOverdue)),
        UrgencyTier.Urgent => TimeSpan.FromHours(Math.Max(1, s.HoursWithin24h)),
        UrgencyTier.Soon => TimeSpan.FromHours(Math.Max(1, s.HoursWithin3Days)),
        UrgencyTier.Week => TimeSpan.FromHours(24),
        _ => null, // >7 days out or no deadline: the baseline cadence covers these
    };

    public static bool InQuietHours(AppSettings s, DateTime now)
    {
        if (!s.QuietHoursEnabled) return false;
        var t = now.TimeOfDay;
        if (s.QuietStart == s.QuietEnd) return false;
        return s.QuietStart < s.QuietEnd
            ? t >= s.QuietStart && t < s.QuietEnd
            : t >= s.QuietStart || t < s.QuietEnd; // wraps midnight (e.g. 22:00 → 08:00)
    }

    public static DateTime NextBaselineTime(AppSettings s, DateTime last) => s.Baseline switch
    {
        BaselineMode.Every4Hours => last.AddHours(4),
        BaselineMode.CustomHours => last.AddHours(Math.Max(1, s.CustomHours)),
        BaselineMode.Every3Days => last.AddDays(3),
        BaselineMode.DailyMorning => NextAtTimes(last, new TimeSpan(8, 0, 0)),
        BaselineMode.TwiceDaily => NextAtTimes(last, new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0)),
        BaselineMode.WeeklyMonday => NextWeekday(last, DayOfWeek.Monday, new TimeSpan(8, 0, 0)),
        _ => last.AddHours(4),
    };

    private static DateTime NextAtTimes(DateTime after, params TimeSpan[] times)
    {
        var best = DateTime.MaxValue;
        for (int day = 0; day <= 1; day++)
        {
            foreach (var tod in times)
            {
                var cand = after.Date.AddDays(day) + tod;
                if (cand > after && cand < best) best = cand;
            }
        }
        return best;
    }

    private static DateTime NextWeekday(DateTime after, DayOfWeek dow, TimeSpan tod)
    {
        for (int day = 0; day <= 7; day++)
        {
            var cand = after.Date.AddDays(day) + tod;
            if (cand.DayOfWeek == dow && cand > after) return cand;
        }
        return after.AddDays(7);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}
