using System.Globalization;
using DoneYet.Data;
using DoneYet.Models;

namespace DoneYet.Services;

public sealed record SeriesReport(
    string Series,
    string Currency,
    decimal AvgAmount,
    int EntryCount,
    DateTime FirstMonth,
    DateTime LastMonth,
    bool Ended,
    DateTime? EndedMonth,
    List<DateTime> MissingMonths);

/// <summary>
/// For every recurring expense series (e.g. "Adobe"), walks month by month from the first
/// entry to now (or to when the series was marked ended) and reports gaps — so at tax time
/// you get a shopping list of invoices to hunt down instead of a surprise.
/// Matching is by series name, not amount, because USD-priced subscriptions land differently every month.
/// </summary>
public static class MissingInvoiceService
{
    public static List<SeriesReport> Build(Store store, DateTime now)
    {
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        var reports = new List<SeriesReport>();

        var groups = store.Expenses
            .Where(e => e.IsRecurring && !string.IsNullOrWhiteSpace(e.EffectiveSeries))
            .GroupBy(e => e.EffectiveSeries, StringComparer.OrdinalIgnoreCase);

        foreach (var g in groups)
        {
            var monthsPresent = new HashSet<DateTime>(g.Select(e => new DateTime(e.Date.Year, e.Date.Month, 1)));
            var first = monthsPresent.Min();
            var last = monthsPresent.Max();

            bool ended = store.EndedSeries.TryGetValue(g.Key, out var endedStr)
                         && TryParseMonth(endedStr, out var endedMonth);
            DateTime? endedAt = ended ? ParseMonth(endedStr!) : null;

            // Track through the ended month if marked, otherwise through the current month.
            // Entries dated after the "ended" month implicitly resume tracking.
            var trackUntil = endedAt.HasValue
                ? (last > endedAt.Value ? last : endedAt.Value)
                : (currentMonth > last ? currentMonth : last);

            var missing = new List<DateTime>();
            for (var m = first; m <= trackUntil; m = m.AddMonths(1))
                if (!monthsPresent.Contains(m)) missing.Add(m);

            var currency = g.GroupBy(e => e.Currency).OrderByDescending(c => c.Count()).First().Key;
            var avg = Math.Round(g.Average(e => e.Amount), 2);

            reports.Add(new SeriesReport(g.Key, currency, avg, g.Count(), first, last, ended, endedAt, missing));
        }

        return reports.OrderBy(r => r.Series, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static int TotalMissing(Store store, DateTime now) =>
        Build(store, now).Sum(r => r.MissingMonths.Count);

    public static string MonthKey(DateTime m) => m.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static bool TryParseMonth(string? s, out DateTime month)
    {
        month = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (!DateTime.TryParseExact(s, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return false;
        month = d;
        return true;
    }

    private static DateTime ParseMonth(string s) =>
        DateTime.ParseExact(s, "yyyy-MM", CultureInfo.InvariantCulture);
}
