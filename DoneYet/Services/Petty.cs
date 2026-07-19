namespace DoneYet.Services;

/// <summary>
/// The app's optional personality. Petty inside the app's own UI only —
/// it never interrupts, never blocks, never steals focus.
/// </summary>
public static class Petty
{
    private static readonly string[] OverdueQuips =
    {
        "we both knew this would happen",
        "it's still not going to do itself",
        "day {0} of creative avoidance",
        "your future self is judging you",
        "remember when this was 'plenty of time'? good times",
        "the deadline was several exits back",
        "still pretending you didn't see this?",
        "moving it to 'tomorrow' again? bold",
        "somewhere, an accountant just winced",
        "this is the thing. do the thing",
    };

    private static readonly string[] ConfirmPrompts =
    {
        "Actually done, or just tired of looking at it?",
        "Done done? Not 'basically done'?",
        "Confirm it's finished — no take-backs.",
        "Swear it's complete?",
    };

    private static readonly string[] Praise =
    {
        "Look at you, doing things.",
        "One less thing yelling at you.",
        "Filed. Forgotten. Free.",
        "The list shrinks. The legend grows.",
    };

    /// <summary>Stable per-item quip that rotates once a day, not every repaint.</summary>
    public static string OverdueQuip(string id, int daysOverdue, DateTime now)
    {
        int seed = StableHash(id) ^ now.DayOfYear;
        var quip = OverdueQuips[Math.Abs(seed) % OverdueQuips.Length];
        return string.Format(quip, Math.Max(1, daysOverdue));
    }

    public static string ConfirmPrompt(string title, bool petty)
    {
        if (!petty) return $"Mark \"{title}\" as done?";
        int i = Math.Abs(StableHash(title) ^ Environment.TickCount / 60000) % ConfirmPrompts.Length;
        return $"\"{title}\"\n\n{ConfirmPrompts[i]}";
    }

    public static string PraiseLine()
    {
        return Praise[Math.Abs(Environment.TickCount / 1000) % Praise.Length];
    }

    /// <summary>string.GetHashCode is randomized per process; this one is stable across runs.</summary>
    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (char c in s) h = h * 31 + c;
            return h;
        }
    }
}
