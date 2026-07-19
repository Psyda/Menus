using DoneYet.Data;
using DoneYet.Models;
using DoneYet.Services;
using DoneYet.UI;

namespace DoneYet.App;

/// <summary>
/// The always-running heart of the app: tray icon with live count + urgency color,
/// the 30-second scheduler tick, and ownership of the widget and manager windows.
/// </summary>
public sealed class TrayAppContext : ApplicationContext
{
    private readonly Store _store = new();
    private readonly ReminderScheduler _scheduler;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Control _marshal = new(); // gives us a handle to BeginInvoke onto the UI thread
    private readonly RegisteredWaitHandle? _showWait;

    private WidgetForm? _widget;
    private ManagerForm? _manager;
    private (int count, int argb) _iconState = (-1, 0);

    public TrayAppContext(EventWaitHandle showSignal)
    {
        _store.Load();
        SoundService.EnsureDefaults();
        _scheduler = new ReminderScheduler(_store);
        _marshal.CreateControl();

        _tray = new NotifyIcon
        {
            Icon = TrayIconRenderer.Render(0, Theme.Ok),
            Text = "DoneYet",
            ContextMenuStrip = new ContextMenuStrip(),
        };
        _tray.ContextMenuStrip.Opening += (_, e) =>
        {
            RebuildMenu();
            e.Cancel = false;
        };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowManager(0); };
        _tray.BalloonTipClicked += (_, _) => ShowManager(0);
        _tray.Visible = true;

        _store.Changed += OnStoreChanged;

        _timer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();

        if (_store.Settings.ShowWidget) EnsureWidgetShown();
        UpdateTray();

        // Second launches of the exe just poke this event to surface the manager.
        _showWait = ThreadPool.RegisterWaitForSingleObject(showSignal,
            (_, _) => { try { _marshal.BeginInvoke(new Action(() => ShowManager(0))); } catch { /* shutting down */ } },
            null, Timeout.Infinite, executeOnlyOnce: false);
    }

    private void OnStoreChanged()
    {
        if (_store.Settings.ShowWidget) EnsureWidgetShown();
        else if (_widget is { IsDisposed: false, Visible: true }) _widget.Hide();
        UpdateTray();
    }

    private void EnsureWidgetShown()
    {
        if (_widget == null || _widget.IsDisposed)
            _widget = new WidgetForm(_store, ShowManager);
        if (!_widget.Visible) _widget.Show();
    }

    public void ShowManager(int tab)
    {
        if (_manager == null || _manager.IsDisposed) _manager = new ManagerForm(_store);
        _manager.ShowTab(tab);
    }

    private void OnTick()
    {
        try
        {
            var req = _scheduler.Tick(DateTime.Now);
            if (req != null) Notify(req);
            UpdateTray();
        }
        catch (Exception ex)
        {
            Store.Log("Tick failed: " + ex);
        }
    }

    private void Notify(NotificationRequest req)
    {
        var now = DateTime.Now;
        var body = req.Body;

        if (req.BaselineIncluded)
        {
            try
            {
                int missing = MissingInvoiceService.TotalMissing(_store, now);
                if (missing > 0)
                    body += $"\n📎 {missing} recurring-invoice month(s) missing — see Missing invoices";
            }
            catch { /* report is cosmetic here */ }
        }

        // Standard Windows toast: lands in notification center, always dismissible,
        // never steals focus, never blocks input. That's the deal.
        _tray.BalloonTipTitle = Truncate(req.Title, 63);
        _tray.BalloonTipText = Truncate(string.IsNullOrWhiteSpace(body) ? "You have open todos." : body, 250);
        _tray.BalloonTipIcon = ToolTipIcon.None;
        _tray.ShowBalloonTip(10_000);

        var s = _store.Settings;
        bool muted = s.GlobalMuteUntil.HasValue && s.GlobalMuteUntil.Value > now;
        if (s.SoundEnabled && !muted)
        {
            SoundService.Play(req.Tier switch
            {
                UrgencyTier.Overdue => s.SoundOverdue,
                UrgencyTier.Urgent => s.SoundUrgent,
                UrgencyTier.Soon => s.SoundSoon,
                _ => s.SoundNormal,
            });
        }
    }

    private void UpdateTray()
    {
        var now = DateTime.Now;
        var open = _store.Todos.Where(t => !t.IsDone).ToList();
        var active = open.Where(t => !t.IsSnoozed).ToList();
        int attention = active.Count(t => UrgencyService.GetTier(t, now) >= UrgencyTier.Soon);

        int count;
        Color color;
        if (attention > 0)
        {
            count = attention;
            color = UrgencyService.TierColor(UrgencyService.WorstTier(active, now));
        }
        else if (open.Count > 0)
        {
            count = open.Count;
            color = Color.FromArgb(96, 102, 110); // calm gray: things exist, nothing's on fire
        }
        else
        {
            count = 0;
            color = Theme.Ok;
        }

        if (_iconState != (count, color.ToArgb()))
        {
            _iconState = (count, color.ToArgb());
            var old = _tray.Icon;
            _tray.Icon = TrayIconRenderer.Render(count, color);
            old?.Dispose();
        }

        var tip = open.Count == 0 ? "DoneYet — all clear" : "DoneYet — " + UrgencyService.SummaryLine(open, now);
        _tray.Text = Truncate(tip, 63);
    }

    private void RebuildMenu()
    {
        var menu = _tray.ContextMenuStrip!;
        menu.Items.Clear();
        var s = _store.Settings;
        var now = DateTime.Now;

        menu.Items.Add("Open DoneYet", null, (_, _) => ShowManager(0));
        menu.Items.Add(s.ShowWidget ? "Hide widget" : "Show widget", null, (_, _) =>
        {
            s.ShowWidget = !s.ShowWidget;
            _store.SaveSettings();
        });
        menu.Items.Add("Add todo…", null, (_, _) => { using var f = new TodoEditForm(_store, null); f.ShowDialog(); });
        menu.Items.Add("Add expense…", null, (_, _) => { using var f = new ExpenseEditForm(_store, null); f.ShowDialog(); });
        menu.Items.Add(new ToolStripSeparator());

        bool snoozed = s.GlobalSnoozeUntil.HasValue && s.GlobalSnoozeUntil.Value > now;
        var snooze = new ToolStripMenuItem(snoozed
            ? $"Reminders snoozed until {s.GlobalSnoozeUntil!.Value:h:mm tt}"
            : "Snooze all reminders");
        void SnoozeAll(string label, Func<DateTime> until) =>
            snooze.DropDownItems.Add(label, null, (_, _) => { s.GlobalSnoozeUntil = until(); _store.SaveSettings(); });
        SnoozeAll("1 hour", () => DateTime.Now.AddHours(1));
        SnoozeAll("4 hours", () => DateTime.Now.AddHours(4));
        SnoozeAll("Until tomorrow 8 AM", () => DateTime.Today.AddDays(1).AddHours(8));
        if (snoozed)
        {
            snooze.DropDownItems.Add(new ToolStripSeparator());
            snooze.DropDownItems.Add("Resume reminders now", null, (_, _) => { s.GlobalSnoozeUntil = null; _store.SaveSettings(); });
        }
        menu.Items.Add(snooze);

        bool muted = s.GlobalMuteUntil.HasValue && s.GlobalMuteUntil.Value > now;
        var mute = new ToolStripMenuItem(muted
            ? $"Sounds muted until {s.GlobalMuteUntil!.Value:h:mm tt}"
            : "Mute sounds");
        void MuteFor(string label, Func<DateTime> until) =>
            mute.DropDownItems.Add(label, null, (_, _) => { s.GlobalMuteUntil = until(); _store.SaveSettings(); });
        MuteFor("1 hour", () => DateTime.Now.AddHours(1));
        MuteFor("4 hours", () => DateTime.Now.AddHours(4));
        MuteFor("Rest of today", () => DateTime.Today.AddDays(1));
        if (muted)
        {
            mute.DropDownItems.Add(new ToolStripSeparator());
            mute.DropDownItems.Add("Unmute now", null, (_, _) => { s.GlobalMuteUntil = null; _store.SaveSettings(); });
        }
        menu.Items.Add(mute);

        var ct = new ToolStripMenuItem("Widget click-through") { Checked = s.WidgetClickThrough };
        ct.Click += (_, _) => { s.WidgetClickThrough = !s.WidgetClickThrough; _store.SaveSettings(); };
        menu.Items.Add(ct);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings…", null, (_, _) => ShowManager(4));
        menu.Items.Add("Open data folder", null, (_, _) => UiActions.OpenExternal(null, Store.DataDir));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit DoneYet", null, (_, _) => ExitApp());
    }

    private void ExitApp()
    {
        _timer.Stop();
        _showWait?.Unregister(null);
        _tray.Visible = false;
        _tray.Dispose();
        _widget?.Dispose();
        _manager?.Dispose(); // Dispose skips the hide-on-close trap
        ExitThread();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}
