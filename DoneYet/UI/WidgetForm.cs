using System.Runtime.InteropServices;
using DoneYet.Data;
using DoneYet.Models;
using DoneYet.Services;

namespace DoneYet.UI;

/// <summary>
/// The always-visible glanceable list. Borderless dark panel that can live on the
/// desktop layer (behind your windows), float normally, or stay always-on-top.
/// It never steals focus and never pops up in front of anything on its own.
/// </summary>
public sealed class WidgetForm : Form
{
    private const int HeaderH = 36, RowH = 40, FooterH = 24, MoreH = 22, EmptyH = 72;
    private const int StripeW = 4;

    private enum Zone { None, Check, Snooze, RowBody, HeaderAdd, HeaderMenu, More }

    private readonly Store _store;
    private readonly Action<int> _openManager; // tab index
    private readonly System.Windows.Forms.Timer _repaintTimer;

    private List<TodoItem> _rows = new();
    private int _moreCount;
    private int _hoverRow = -1;
    private Zone _hoverZone = Zone.None;
    private bool _positioned;

    // ---- Win32 ----
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int GWL_EXSTYLE = -20;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_EXITSIZEMOVE = 0x0232;
    private const int HTCAPTION = 2, HTLEFT = 10, HTRIGHT = 11, HTCLIENT = 1;
    private static readonly IntPtr HWND_BOTTOM = new(1);
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int idx);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int idx, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong32(IntPtr hWnd, int idx);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong32(IntPtr hWnd, int idx, int value);

    private static IntPtr GetExStyle(IntPtr h) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(h, GWL_EXSTYLE) : new IntPtr(GetWindowLong32(h, GWL_EXSTYLE));
    private static void SetExStyle(IntPtr h, IntPtr v)
    {
        if (IntPtr.Size == 8) SetWindowLongPtr64(h, GWL_EXSTYLE, v);
        else SetWindowLong32(h, GWL_EXSTYLE, v.ToInt32());
    }

    public WidgetForm(Store store, Action<int> openManager)
    {
        _store = store;
        _openManager = openManager;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Bg;
        MinimumSize = new Size(260, HeaderH + FooterH);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        _store.Changed += OnStoreChanged;

        // Due-in texts age even when nothing changes; refresh once a minute.
        _repaintTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _repaintTimer.Tick += (_, _) => Reload();
        _repaintTimer.Start();

        Reload();
        ApplySettings(firstShow: true);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW; // no taskbar button, no alt-tab entry
            return cp;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _store.Changed -= OnStoreChanged;
            _repaintTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    private void OnStoreChanged()
    {
        ApplySettings(firstShow: false);
        Reload();
    }

    public void ApplySettings(bool firstShow)
    {
        var s = _store.Settings;
        Opacity = Math.Clamp(s.WidgetOpacity, 30, 100) / 100.0;
        TopMost = s.WidgetMode == WidgetMode.AlwaysOnTop;

        if (firstShow && !_positioned)
        {
            _positioned = true;
            var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
            int w = Math.Max(MinimumSize.Width, s.WidgetW);
            int x = s.WidgetX, y = s.WidgetY;
            if (x < 0 || y < 0) { x = wa.Right - w - 24; y = wa.Top + 72; }
            // If the saved spot is off-screen (monitor unplugged), pull it back.
            var virt = SystemInformation.VirtualScreen;
            x = Math.Clamp(x, virt.Left, Math.Max(virt.Left, virt.Right - 100));
            y = Math.Clamp(y, virt.Top, Math.Max(virt.Top, virt.Bottom - 100));
            Location = new Point(x, y);
            Width = w;
        }

        if (IsHandleCreated)
        {
            SetClickThrough(s.WidgetClickThrough);
            if (s.WidgetMode == WidgetMode.Desktop) SendToBottomLayer();
        }
    }

    private void SetClickThrough(bool on)
    {
        var ex = GetExStyle(Handle).ToInt64();
        if (on) ex |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
        else ex &= ~WS_EX_TRANSPARENT;
        SetExStyle(Handle, new IntPtr(ex));
    }

    private void SendToBottomLayer() =>
        SetWindowPos(Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplySettings(firstShow: false);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        // Desktop mode: after you interact and click away, sink back under everything.
        if (_store.Settings.WidgetMode == WidgetMode.Desktop) SendToBottomLayer();
    }

    // ------------------------------------------------------------------ data

    private void Reload()
    {
        var now = DateTime.Now;
        var open = _store.Todos.Where(t => !t.IsDone).ToList();
        var sorted = UrgencyService.SortForDisplay(open, now);
        int max = Math.Clamp(_store.Settings.WidgetMaxItems, 1, 30);
        _rows = sorted.Take(max).ToList();
        _moreCount = sorted.Count - _rows.Count;

        int body = _rows.Count == 0 ? EmptyH : _rows.Count * RowH + (_moreCount > 0 ? MoreH : 0);
        Height = HeaderH + body + FooterH + 2;
        Invalidate();
    }

    // ------------------------------------------------------------------ painting

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var now = DateTime.Now;
        g.Clear(Theme.Bg);

        // Header
        using (var hb = new SolidBrush(Theme.Header)) g.FillRectangle(hb, 0, 0, Width, HeaderH);
        TextRenderer.DrawText(g, "DoneYet", Theme.HeaderFont, new Rectangle(12, 0, 100, HeaderH),
            Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

        var open = _store.Todos.Where(t => !t.IsDone).ToList();
        int attention = open.Count(t => UrgencyService.GetTier(t, now) >= UrgencyTier.Soon);
        string headSummary = attention > 0 ? $"{attention} need attention" : (open.Count > 0 ? $"{open.Count} open" : "all clear");
        var headColor = attention > 0 ? UrgencyService.TierColor(UrgencyService.WorstTier(open, now)) : Theme.SubText;
        TextRenderer.DrawText(g, headSummary, Theme.SubFont, new Rectangle(86, 0, Width - 86 - 64, HeaderH),
            headColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        DrawHeaderButton(g, AddButtonRect(), "+", _hoverZone == Zone.HeaderAdd);
        DrawHeaderButton(g, MenuButtonRect(), "≡", _hoverZone == Zone.HeaderMenu);

        // Rows
        if (_rows.Count == 0)
        {
            var r = new Rectangle(0, HeaderH, Width, EmptyH);
            TextRenderer.DrawText(g, "Nothing here. Suspicious.", Theme.TitleFont,
                new Rectangle(r.X, r.Y + 10, r.Width, 24), Theme.SubText, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, "Click + to add a todo", Theme.SubFont,
                new Rectangle(r.X, r.Y + 36, r.Width, 20), Theme.SubText, TextFormatFlags.HorizontalCenter);
        }
        else
        {
            for (int i = 0; i < _rows.Count; i++) DrawRow(g, i, _rows[i], now);
            if (_moreCount > 0)
            {
                var mr = MoreRect();
                if (_hoverZone == Zone.More) using (var b = new SolidBrush(Theme.Hover)) g.FillRectangle(b, mr);
                TextRenderer.DrawText(g, $"+{_moreCount} more — open manager", Theme.SubFont, mr,
                    Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        // Footer
        var fr = new Rectangle(0, Height - FooterH - 1, Width, FooterH);
        string footer = open.Count == 0 ? "nothing due. enjoy it." : UrgencyService.SummaryLine(open, now);
        var s = _store.Settings;
        if (s.GlobalSnoozeUntil.HasValue && s.GlobalSnoozeUntil.Value > now)
            footer += $"  ·  snoozed until {s.GlobalSnoozeUntil.Value:h:mm tt}";
        else if (s.GlobalMuteUntil.HasValue && s.GlobalMuteUntil.Value > now)
            footer += $"  ·  muted until {s.GlobalMuteUntil.Value:h:mm tt}";
        TextRenderer.DrawText(g, footer, Theme.SubFont, new Rectangle(10, fr.Y, Width - 20, FooterH),
            Theme.SubText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        using var pen = new Pen(Theme.Border);
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    private void DrawHeaderButton(Graphics g, Rectangle r, string glyph, bool hover)
    {
        if (hover) using (var b = new SolidBrush(Theme.Hover)) g.FillRectangle(b, r);
        TextRenderer.DrawText(g, glyph, Theme.HeaderFont, r,
            hover ? Theme.Text : Theme.SubText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void DrawRow(Graphics g, int i, TodoItem t, DateTime now)
    {
        int y = HeaderH + i * RowH;
        var tier = UrgencyService.GetTier(t, now);
        var tierColor = UrgencyService.TierColor(tier);
        bool snoozed = t.IsSnoozed;

        if (_hoverRow == i)
            using (var hb = new SolidBrush(Theme.Hover)) g.FillRectangle(hb, 0, y, Width, RowH);

        using (var sb = new SolidBrush(snoozed ? Color.FromArgb(90, tierColor) : tierColor))
            g.FillRectangle(sb, 0, y, StripeW, RowH);

        // Check circle
        var c = CheckRect(i);
        var old = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (_hoverRow == i && _hoverZone == Zone.Check)
        {
            using var fill = new SolidBrush(Theme.Ok);
            g.FillEllipse(fill, c);
            using var check = new Pen(Color.White, 2f);
            g.DrawLines(check, new[]
            {
                new PointF(c.X + 5f, c.Y + 10.5f),
                new PointF(c.X + 8.5f, c.Y + 14f),
                new PointF(c.X + 15f, c.Y + 6f),
            });
        }
        else
        {
            using var ring = new Pen(snoozed ? Theme.SubText : tierColor, 1.8f);
            g.DrawEllipse(ring, c);
        }
        g.SmoothingMode = old;

        // Text block
        int rightPad = _hoverRow == i ? 34 : 12;
        int textX = 44;
        int textW = Width - textX - rightPad;
        TextRenderer.DrawText(g, t.Title, Theme.TitleFont, new Rectangle(textX, y + 4, textW, 18),
            snoozed ? Theme.SubText : Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        string sub = UrgencyService.DueText(t, now);
        if (t.IsRecurring) sub += "  ↻ " + RecurrenceService.Describe(t);
        if (snoozed && t.SnoozedUntil.HasValue) sub += $"  ·  zzz until {FormatShort(t.SnoozedUntil.Value, now)}";
        if (tier == UrgencyTier.Overdue && _store.Settings.PettyMode && t.Deadline.HasValue)
        {
            int daysOver = Math.Max(1, (int)(now - t.Deadline.Value).TotalDays);
            sub += "  ·  " + Petty.OverdueQuip(t.Id, daysOver, now);
        }
        var subColor = tier >= UrgencyTier.Urgent && !snoozed ? tierColor : Theme.SubText;
        TextRenderer.DrawText(g, sub, Theme.SubFont, new Rectangle(textX, y + 21, textW, 16),
            subColor, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        // Snooze affordance (hover only)
        if (_hoverRow == i)
        {
            var zr = SnoozeRect(i);
            TextRenderer.DrawText(g, "z", Theme.HeaderFont, zr,
                _hoverZone == Zone.Snooze ? Theme.Accent : Theme.SubText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private static string FormatShort(DateTime d, DateTime now) =>
        d.Date == now.Date ? d.ToString("h:mm tt") : d.ToString("ddd h:mm tt");

    // ------------------------------------------------------------------ geometry

    private Rectangle AddButtonRect() => new(Width - 62, 5, 26, 26);
    private Rectangle MenuButtonRect() => new(Width - 32, 5, 26, 26);
    private Rectangle CheckRect(int i) => new(13, HeaderH + i * RowH + (RowH - 20) / 2, 20, 20);
    private Rectangle SnoozeRect(int i) => new(Width - 30, HeaderH + i * RowH + (RowH - 22) / 2, 22, 22);
    private Rectangle MoreRect() => new(0, HeaderH + _rows.Count * RowH, Width, MoreH);

    private (int row, Zone zone) HitTest(Point p)
    {
        if (AddButtonRect().Contains(p)) return (-1, Zone.HeaderAdd);
        if (MenuButtonRect().Contains(p)) return (-1, Zone.HeaderMenu);
        if (_moreCount > 0 && MoreRect().Contains(p)) return (-1, Zone.More);
        if (p.Y >= HeaderH && _rows.Count > 0)
        {
            int i = (p.Y - HeaderH) / RowH;
            if (i >= 0 && i < _rows.Count)
            {
                if (CheckRect(i).Contains(p)) return (i, Zone.Check);
                if (SnoozeRect(i).Contains(p)) return (i, Zone.Snooze);
                return (i, Zone.RowBody);
            }
        }
        return (-1, Zone.None);
    }

    // ------------------------------------------------------------------ input

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var (row, zone) = HitTest(e.Location);
        if (row != _hoverRow || zone != _hoverZone)
        {
            _hoverRow = row;
            _hoverZone = zone;
            Cursor = zone is Zone.Check or Zone.Snooze or Zone.HeaderAdd or Zone.HeaderMenu or Zone.More
                ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverRow = -1;
        _hoverZone = Zone.None;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        var (_, zone) = HitTest(e.Location);
        bool dragArea = (e.Y < HeaderH && zone == Zone.None) || e.Y > Height - FooterH;
        if (dragArea)
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var (row, zone) = HitTest(e.Location);

        if (e.Button == MouseButtons.Right)
        {
            if (row >= 0) ShowRowMenu(_rows[row]);
            else ShowWidgetMenu();
            return;
        }
        if (e.Button != MouseButtons.Left) return;

        switch (zone)
        {
            case Zone.HeaderAdd:
                using (var f = new TodoEditForm(_store, null)) f.ShowDialog(this);
                break;
            case Zone.HeaderMenu:
                _openManager(0);
                break;
            case Zone.More:
                _openManager(0);
                break;
            case Zone.Check when row >= 0:
                UiActions.ConfirmComplete(this, _store, _rows[row]);
                break;
            case Zone.Snooze when row >= 0:
                ShowSnoozeMenu(_rows[row]);
                break;
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left) return;
        var (row, zone) = HitTest(e.Location);
        if (zone == Zone.RowBody && row >= 0)
            using (var f = new TodoEditForm(_store, _rows[row])) f.ShowDialog(this);
    }

    private void ShowSnoozeMenu(TodoItem t)
    {
        var menu = new ContextMenuStrip();
        var root = new ToolStripMenuItem("Snooze");
        UiActions.AddSnoozeMenu(root, _store, t);
        foreach (ToolStripItem item in root.DropDownItems.Cast<ToolStripItem>().ToArray())
            menu.Items.Add(item);
        menu.Show(Cursor.Position);
    }

    private void ShowRowMenu(TodoItem t)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("✓  Complete…", null, (_, _) => UiActions.ConfirmComplete(this, _store, t));
        menu.Items.Add("Edit…", null, (_, _) => { using var f = new TodoEditForm(_store, t); f.ShowDialog(this); });
        var snooze = new ToolStripMenuItem("Snooze");
        UiActions.AddSnoozeMenu(snooze, _store, t);
        menu.Items.Add(snooze);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Delete…", null, (_, _) => UiActions.DeleteTodo(this, _store, t));
        menu.Show(Cursor.Position);
    }

    private void ShowWidgetMenu()
    {
        var s = _store.Settings;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Add todo…", null, (_, _) => { using var f = new TodoEditForm(_store, null); f.ShowDialog(this); });
        menu.Items.Add("Open manager", null, (_, _) => _openManager(0));
        menu.Items.Add(new ToolStripSeparator());

        var mode = new ToolStripMenuItem("Widget mode");
        foreach (var (label, val) in new[]
                 {
                     ("Desktop (behind windows)", WidgetMode.Desktop),
                     ("Normal", WidgetMode.Normal),
                     ("Always on top", WidgetMode.AlwaysOnTop),
                 })
        {
            var item = new ToolStripMenuItem(label) { Checked = s.WidgetMode == val };
            item.Click += (_, _) => { s.WidgetMode = val; _store.SaveSettings(); };
            mode.DropDownItems.Add(item);
        }
        menu.Items.Add(mode);

        var opacity = new ToolStripMenuItem("Opacity");
        foreach (int pct in new[] { 60, 75, 90, 100 })
        {
            var item = new ToolStripMenuItem(pct + "%") { Checked = s.WidgetOpacity == pct };
            item.Click += (_, _) => { s.WidgetOpacity = pct; _store.SaveSettings(); };
            opacity.DropDownItems.Add(item);
        }
        menu.Items.Add(opacity);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Hide widget (tray keeps running)", null, (_, _) =>
        {
            s.ShowWidget = false;
            _store.SaveSettings();
        });
        menu.Items.Add("Settings…", null, (_, _) => _openManager(4));
        menu.Show(Cursor.Position);
    }

    // ------------------------------------------------------------------ window plumbing

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            base.WndProc(ref m);
            if ((int)m.Result == HTCLIENT)
            {
                long lp = m.LParam.ToInt64();
                var p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                if (p.X >= Width - 6) m.Result = (IntPtr)HTRIGHT;
                else if (p.X <= 6) m.Result = (IntPtr)HTLEFT;
            }
            return;
        }

        if (m.Msg == WM_EXITSIZEMOVE)
        {
            var s = _store.Settings;
            s.WidgetX = Location.X;
            s.WidgetY = Location.Y;
            s.WidgetW = Width;
            _store.SaveSettings(notify: false);
        }

        base.WndProc(ref m);
    }
}
