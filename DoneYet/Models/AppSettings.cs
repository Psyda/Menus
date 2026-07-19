namespace DoneYet.Models;

public class AppSettings
{
    // ----- Reminders -----
    public bool RemindersEnabled { get; set; } = true;
    public BaselineMode Baseline { get; set; } = BaselineMode.TwiceDaily;
    public int CustomHours { get; set; } = 4;

    /// <summary>Reminders speed up as deadlines get close.</summary>
    public bool EscalationEnabled { get; set; } = true;
    public int HoursWithin3Days { get; set; } = 6;   // remind every N hours when due within 3 days
    public int HoursWithin24h { get; set; } = 3;     // ... within 24 hours
    public int HoursOverdue { get; set; } = 3;       // ... when overdue

    public bool QuietHoursEnabled { get; set; } = true;
    public TimeSpan QuietStart { get; set; } = new(22, 0, 0);
    public TimeSpan QuietEnd { get; set; } = new(8, 0, 0);

    // ----- Sound -----
    public bool SoundEnabled { get; set; } = true;
    public string SoundNormal { get; set; } = "chime.wav";
    public string SoundSoon { get; set; } = "ding.wav";
    public string SoundUrgent { get; set; } = "urgent.wav";
    public string SoundOverdue { get; set; } = "alarm.wav";

    // ----- Personality -----
    public bool PettyMode { get; set; } = true;

    // ----- Widget -----
    public bool ShowWidget { get; set; } = true;
    public WidgetMode WidgetMode { get; set; } = WidgetMode.Desktop;
    public int WidgetOpacity { get; set; } = 92;     // percent, 30..100
    public int WidgetMaxItems { get; set; } = 8;
    public bool WidgetClickThrough { get; set; }
    public int WidgetX { get; set; } = -1;
    public int WidgetY { get; set; } = -1;
    public int WidgetW { get; set; } = 360;

    // ----- Expenses -----
    public string DefaultCurrency { get; set; } = "CAD";

    // ----- Runtime state (persisted so restarts behave) -----
    public DateTime? GlobalSnoozeUntil { get; set; }   // "snooze all": no notifications at all
    public DateTime? GlobalMuteUntil { get; set; }     // sounds off, balloons still show
    public DateTime? LastBaselineNotify { get; set; }
}
