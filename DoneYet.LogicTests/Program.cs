using System.Drawing;
using DoneYet.Data;
using DoneYet.Models;
using DoneYet.Services;

int passed = 0, failed = 0;

void Check(bool cond, string name)
{
    if (cond) { passed++; Console.WriteLine($"  ok  {name}"); }
    else { failed++; Console.WriteLine($"FAIL  {name}"); }
}

Console.WriteLine("== Recurrence ==");
{
    // Monthly bill anchored to the 31st must not drift after February.
    var t = new TodoItem
    {
        Title = "rent-ish",
        Deadline = new DateTime(2026, 1, 31, 17, 0, 0),
        Recurrence = RecurrenceKind.Monthly,
        RecurrenceAnchorDay = 31,
    };
    RecurrenceService.CompleteCycle(t, new DateTime(2026, 2, 2));
    Check(t.Deadline == new DateTime(2026, 2, 28, 17, 0, 0), $"monthly Jan31 -> Feb28 (got {t.Deadline})");
    RecurrenceService.CompleteCycle(t, new DateTime(2026, 3, 1));
    Check(t.Deadline == new DateTime(2026, 3, 31, 17, 0, 0), $"monthly Feb28 -> Mar31 anchor restored (got {t.Deadline})");
    Check(t.TimesCompleted == 2, "times completed tracked");

    // Ten skipped daily cycles collapse into one completion.
    var d = new TodoItem { Deadline = new DateTime(2026, 7, 1, 9, 0, 0), Recurrence = RecurrenceKind.Daily };
    RecurrenceService.CompleteCycle(d, new DateTime(2026, 7, 11, 12, 0, 0));
    Check(d.Deadline == new DateTime(2026, 7, 12, 9, 0, 0), $"daily overdue collapses to next future (got {d.Deadline})");

    var w = new TodoItem { Deadline = new DateTime(2026, 7, 10, 9, 0, 0), Recurrence = RecurrenceKind.EveryNDays, RecurrenceN = 3 };
    RecurrenceService.CompleteCycle(w, new DateTime(2026, 7, 10, 10, 0, 0));
    Check(w.Deadline == new DateTime(2026, 7, 13, 9, 0, 0), $"every-3-days rolls once (got {w.Deadline})");
}

Console.WriteLine("== Urgency tiers ==");
{
    var now = new DateTime(2026, 7, 19, 12, 0, 0);
    TodoItem T(DateTime? dl) => new() { Deadline = dl };
    Check(UrgencyService.GetTier(T(null), now) == UrgencyTier.None, "no deadline -> None");
    Check(UrgencyService.GetTier(T(now.AddHours(-1)), now) == UrgencyTier.Overdue, "past -> Overdue");
    Check(UrgencyService.GetTier(T(now.AddHours(5)), now) == UrgencyTier.Urgent, "5h -> Urgent");
    Check(UrgencyService.GetTier(T(now.AddDays(2)), now) == UrgencyTier.Soon, "2d -> Soon");
    Check(UrgencyService.GetTier(T(now.AddDays(5)), now) == UrgencyTier.Week, "5d -> Week");
    Check(UrgencyService.GetTier(T(now.AddDays(20)), now) == UrgencyTier.Later, "20d -> Later");
    Check(UrgencyService.DueText(T(now.AddDays(-3)), now).StartsWith("OVERDUE 3d"), "overdue text");
}

Console.WriteLine("== Store round-trip ==");
{
    Store store = new();
    store.Load();
    store.Todos.Clear();
    store.Expenses.Clear();
    store.EndedSeries.Clear();

    store.Todos.Add(new TodoItem { Title = "pay hydro", Deadline = new DateTime(2026, 8, 1, 17, 0, 0), Recurrence = RecurrenceKind.Monthly, RecurrenceAnchorDay = 1 });
    store.Expenses.Add(new Expense { Description = "Adobe", Amount = 34.12m, Currency = "USD", Category = "Software & subscriptions", IsRecurring = true, SeriesName = "Adobe", Date = new DateTime(2026, 5, 3) });
    store.EndedSeries["OldTool"] = "2026-02";
    store.SaveTodos(notify: false);
    store.SaveExpenses(notify: false);
    store.SaveSettings(notify: false);

    Store store2 = new();
    store2.Load();
    Check(store2.Todos.Count == 1 && store2.Todos[0].Title == "pay hydro", "todo round-trips");
    Check(store2.Todos[0].Recurrence == RecurrenceKind.Monthly, "enum-as-string round-trips");
    Check(store2.Expenses.Count == 1 && store2.Expenses[0].Amount == 34.12m, "expense decimal round-trips");
    Check(store2.EndedSeries.TryGetValue("oldtool", out var em) && em == "2026-02", "ended series case-insensitive");
    Check(store2.Settings.QuietStart == new TimeSpan(22, 0, 0), "TimeSpan setting round-trips");
}

Console.WriteLine("== Missing invoice months ==");
{
    Store store = new();
    store.Load();
    store.Expenses.Clear();
    store.EndedSeries.Clear();
    void Add(int y, int m) => store.Expenses.Add(new Expense
    {
        Description = "Adobe sub",
        SeriesName = "Adobe",
        IsRecurring = true,
        Amount = 27m + m, // USD-tied: varies every month
        Currency = "USD",
        Date = new DateTime(y, m, 5),
    });
    Add(2026, 1); Add(2026, 2); Add(2026, 4);

    var now = new DateTime(2026, 7, 19);
    var reports = MissingInvoiceService.Build(store, now);
    Check(reports.Count == 1, "one series");
    var r = reports[0];
    var miss = r.MissingMonths.Select(MissingInvoiceService.MonthKey).ToList();
    Check(string.Join(",", miss) == "2026-03,2026-05,2026-06,2026-07",
        $"gaps Mar,May,Jun + current Jul (got {string.Join(",", miss)})");

    store.EndedSeries["Adobe"] = "2026-04";
    var r2 = MissingInvoiceService.Build(store, now)[0];
    Check(string.Join(",", r2.MissingMonths.Select(MissingInvoiceService.MonthKey)) == "2026-03",
        $"ended series only reports gaps up to end (got {string.Join(",", r2.MissingMonths.Select(MissingInvoiceService.MonthKey))})");
    Check(r2.Ended, "ended flag set");

    // Non-recurring expenses never join a series.
    store.Expenses.Add(new Expense { Description = "one-off mouse", Amount = 50, Date = new DateTime(2026, 6, 1) });
    Check(MissingInvoiceService.Build(store, now).Count == 1, "non-recurring ignored");
}

Console.WriteLine("== Reminder scheduler ==");
{
    Store store = new();
    store.Load();
    store.Todos.Clear();
    store.Settings.RemindersEnabled = true;
    store.Settings.EscalationEnabled = true;
    store.Settings.Baseline = BaselineMode.TwiceDaily;
    store.Settings.QuietHoursEnabled = true;
    store.Settings.QuietStart = new TimeSpan(22, 0, 0);
    store.Settings.QuietEnd = new TimeSpan(8, 0, 0);
    store.Settings.HoursOverdue = 3;
    store.Settings.GlobalSnoozeUntil = null;
    store.Settings.LastBaselineNotify = new DateTime(2026, 7, 19, 7, 0, 0);

    var sched = new ReminderScheduler(store);

    // Nothing open -> silence.
    Check(sched.Tick(new DateTime(2026, 7, 19, 9, 0, 0)) == null, "no todos -> no nag");

    // Overdue item, never notified: created long ago, interval 3h passed.
    var od = new TodoItem
    {
        Title = "send the files",
        Deadline = new DateTime(2026, 7, 16, 17, 0, 0),
        CreatedAt = new DateTime(2026, 7, 10),
    };
    store.Todos.Add(od);

    // 23:30 = quiet hours -> deferred.
    Check(sched.Tick(new DateTime(2026, 7, 19, 23, 30, 0)) == null, "quiet hours defer");
    // 03:00 also quiet (wraps midnight).
    Check(sched.Tick(new DateTime(2026, 7, 20, 3, 0, 0)) == null, "quiet wraps past midnight");

    // 09:00 -> fires, overdue tier, stamps LastNotifiedAt, baseline (8:00 due) included.
    var req = sched.Tick(new DateTime(2026, 7, 20, 9, 0, 0));
    Check(req != null, "fires after quiet hours");
    Check(req!.Tier == UrgencyTier.Overdue, "tier is overdue");
    Check(req.BaselineIncluded, "baseline folded in");
    Check(req.Body.Contains("send the files"), "body names the item");
    Check(od.LastNotifiedAt == new DateTime(2026, 7, 20, 9, 0, 0), "item stamped");

    // 10:00 -> only 1h since stamp, interval 3h -> silent.
    Check(sched.Tick(new DateTime(2026, 7, 20, 10, 0, 0)) == null, "respects escalation interval");
    // 12:30 -> 3.5h -> fires again (escalation only, no baseline).
    var req2 = sched.Tick(new DateTime(2026, 7, 20, 12, 30, 0));
    Check(req2 != null && !req2.BaselineIncluded, "re-fires after interval, no baseline");

    // Snoozed item stops nagging.
    od.SnoozedUntil = new DateTime(2026, 7, 21, 8, 0, 0);
    Check(sched.Tick(new DateTime(2026, 7, 20, 16, 30, 0)) == null, "snoozed item is quiet");
    od.SnoozedUntil = null;

    // Global snooze wins over everything.
    store.Settings.GlobalSnoozeUntil = new DateTime(2026, 7, 20, 18, 0, 0);
    Check(sched.Tick(new DateTime(2026, 7, 20, 17, 0, 0)) == null, "global snooze silences");
    store.Settings.GlobalSnoozeUntil = null;

    // Baseline-only: item with no deadline nags on cadence, not constantly.
    store.Todos.Clear();
    store.Todos.Add(new TodoItem { Title = "clean desk", CreatedAt = new DateTime(2026, 7, 1) });
    store.Settings.LastBaselineNotify = new DateTime(2026, 7, 20, 16, 0, 0);
    Check(sched.Tick(new DateTime(2026, 7, 20, 17, 0, 0)) == null, "no-deadline item silent between baselines");
    var req3 = sched.Tick(new DateTime(2026, 7, 21, 8, 30, 0));
    Check(req3 != null && req3.Tier == UrgencyTier.None, "baseline fires next morning at gentle tier");

    // Escalation disabled -> overdue items only ride the baseline.
    store.Settings.EscalationEnabled = false;
    store.Todos.Add(new TodoItem { Title = "old thing", Deadline = new DateTime(2026, 7, 1), CreatedAt = new DateTime(2026, 6, 1) });
    store.Settings.LastBaselineNotify = new DateTime(2026, 7, 21, 8, 30, 0);
    Check(sched.Tick(new DateTime(2026, 7, 21, 10, 0, 0)) == null, "escalation off -> quiet until baseline");
}

Console.WriteLine("== Baseline schedule math ==");
{
    var s = new AppSettings { Baseline = BaselineMode.TwiceDaily };
    Check(ReminderScheduler.NextBaselineTime(s, new DateTime(2026, 7, 19, 9, 0, 0)) == new DateTime(2026, 7, 19, 16, 0, 0), "twice daily 9am -> 4pm");
    Check(ReminderScheduler.NextBaselineTime(s, new DateTime(2026, 7, 19, 16, 30, 0)) == new DateTime(2026, 7, 20, 8, 0, 0), "twice daily 4:30pm -> next 8am");
    s.Baseline = BaselineMode.WeeklyMonday;
    Check(ReminderScheduler.NextBaselineTime(s, new DateTime(2026, 7, 19, 9, 0, 0)) == new DateTime(2026, 7, 20, 8, 0, 0), "sunday -> monday 8am");
    s.Baseline = BaselineMode.CustomHours; s.CustomHours = 6;
    Check(ReminderScheduler.NextBaselineTime(s, new DateTime(2026, 7, 19, 9, 0, 0)) == new DateTime(2026, 7, 19, 15, 0, 0), "custom 6h");
    s.Baseline = BaselineMode.Every3Days;
    Check(ReminderScheduler.NextBaselineTime(s, new DateTime(2026, 7, 19, 9, 0, 0)) == new DateTime(2026, 7, 22, 9, 0, 0), "every 3 days");
}

Console.WriteLine("== Petty & categories ==");
{
    var q1 = Petty.OverdueQuip("abc", 3, new DateTime(2026, 7, 19));
    var q2 = Petty.OverdueQuip("abc", 3, new DateTime(2026, 7, 19, 18, 0, 0));
    Check(q1 == q2, "quip stable within a day");
    Check(!string.IsNullOrWhiteSpace(q1), "quip non-empty");
    Check(TaxCategories.Names.Length >= 15, "plenty of categories");
    Check(TaxCategories.ExamplesFor("Software & subscriptions").Contains("Adobe"), "examples wired up");
    Check(UrgencyService.TierColor(UrgencyTier.Overdue) != Color.Empty, "tier colors exist");
}

Console.WriteLine($"\n{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
