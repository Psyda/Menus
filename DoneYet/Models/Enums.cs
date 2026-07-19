namespace DoneYet.Models;

public enum RecurrenceKind
{
    None,
    Daily,
    Weekly,
    Monthly,
    Yearly,
    EveryNDays,
}

/// <summary>Ordered by severity — higher = more urgent.</summary>
public enum UrgencyTier
{
    None = 0,     // no deadline
    Later = 1,    // > 7 days out
    Week = 2,     // within 7 days
    Soon = 3,     // within 3 days
    Urgent = 4,   // within 24 hours
    Overdue = 5,  // past deadline
}

public enum BaselineMode
{
    Every4Hours,
    TwiceDaily,    // 8:00 and 16:00
    DailyMorning,  // 8:00
    Every3Days,
    WeeklyMonday,
    CustomHours,   // every N hours
}

public enum WidgetMode
{
    Desktop,      // sits behind other windows, visible on the desktop
    Normal,
    AlwaysOnTop,
}
