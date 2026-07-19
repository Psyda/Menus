using System.Globalization;
using System.Text;
using DoneYet.Data;
using DoneYet.Models;
using DoneYet.Services;

namespace DoneYet.UI;

/// <summary>
/// The full control panel: todos, done archive, expense inbox, missing-invoice report, settings.
/// Closing it just hides it — the app lives in the tray.
/// </summary>
public sealed class ManagerForm : Form
{
    private readonly Store _store;
    private readonly TabControl _tabs = new();

    private readonly TabPage _tabTodos = new("Todos");
    private readonly TabPage _tabDone = new("Done");
    private readonly TabPage _tabExpenses = new("Expenses");
    private readonly TabPage _tabMissing = new("Missing invoices");
    private readonly TabPage _tabSettings = new("Settings");

    private readonly DataGridView _gridTodos = new();
    private readonly DataGridView _gridDone = new();
    private readonly DataGridView _gridExpenses = new();
    private readonly TreeView _treeMissing = new();
    private readonly ComboBox _cmbYear = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly Label _lblExpenseTotals = new();

    // Settings controls
    private bool _loadingSettings;
    private readonly CheckBox _chkReminders = new() { Text = "Enable reminder notifications", AutoSize = true };
    private readonly ComboBox _cmbBaseline = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly NumericUpDown _numCustomHours = new() { Minimum = 1, Maximum = 48, Width = 56 };
    private readonly CheckBox _chkEscalation = new() { Text = "Escalate as deadlines approach (get more persistent, never less dismissible)", AutoSize = true };
    private readonly NumericUpDown _numSoon = new() { Minimum = 1, Maximum = 24, Width = 52 };
    private readonly NumericUpDown _numUrgent = new() { Minimum = 1, Maximum = 24, Width = 52 };
    private readonly NumericUpDown _numOverdue = new() { Minimum = 1, Maximum = 24, Width = 52 };
    private readonly CheckBox _chkQuiet = new() { Text = "Quiet hours — no pings between", AutoSize = true };
    private readonly DateTimePicker _dtQuietFrom = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "h:mm tt", ShowUpDown = true, Width = 92 };
    private readonly DateTimePicker _dtQuietTo = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "h:mm tt", ShowUpDown = true, Width = 92 };
    private readonly CheckBox _chkSound = new() { Text = "Play a sound with notifications", AutoSize = true };
    private readonly ComboBox _cmbSndNormal = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox _cmbSndSoon = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox _cmbSndUrgent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox _cmbSndOverdue = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly CheckBox _chkWidget = new() { Text = "Show the desktop widget", AutoSize = true };
    private readonly ComboBox _cmbWidgetMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly TrackBar _trkOpacity = new() { Minimum = 30, Maximum = 100, TickFrequency = 10, Width = 220 };
    private readonly Label _lblOpacityVal = new() { AutoSize = true };
    private readonly NumericUpDown _numMaxItems = new() { Minimum = 3, Maximum = 30, Width = 56 };
    private readonly CheckBox _chkClickThrough = new() { Text = "Click-through widget (mouse passes through it — turn back off here or from the tray menu)", AutoSize = true };
    private readonly CheckBox _chkPetty = new() { Text = "Petty mode — the app may sass you (inside its own windows only, never popups)", AutoSize = true };
    private readonly CheckBox _chkAutostart = new() { Text = "Start with Windows", AutoSize = true };
    private readonly ComboBox _cmbDefCurrency = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };

    public ManagerForm(Store store)
    {
        _store = store;

        Text = "DoneYet";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1040, 700);
        MinimumSize = new Size(880, 560);
        Theme.ApplyForm(this);

        _tabs.Dock = DockStyle.Fill;
        _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.ItemSize = new Size(164, 34);
        _tabs.DrawItem += DrawTab;
        foreach (var tp in new[] { _tabTodos, _tabDone, _tabExpenses, _tabMissing, _tabSettings })
        {
            tp.BackColor = Theme.Bg;
            _tabs.TabPages.Add(tp);
        }
        Controls.Add(_tabs);

        BuildTodosTab();
        BuildDoneTab();
        BuildExpensesTab();
        BuildMissingTab();
        BuildSettingsTab();

        _store.Changed += RefreshAll;
        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        };

        RefreshAll();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _store.Changed -= RefreshAll;
        base.Dispose(disposing);
    }

    private void DrawTab(object? sender, DrawItemEventArgs e)
    {
        var page = _tabs.TabPages[e.Index];
        bool sel = e.Index == _tabs.SelectedIndex;
        using (var b = new SolidBrush(sel ? Theme.Panel : Theme.Header))
            e.Graphics.FillRectangle(b, e.Bounds);
        TextRenderer.DrawText(e.Graphics, page.Text, sel ? Theme.TitleFont : Theme.BodyFont, e.Bounds,
            sel ? Theme.Text : Theme.SubText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    public void ShowTab(int index)
    {
        if (index >= 0 && index < _tabs.TabCount) _tabs.SelectedIndex = index;
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    private void RefreshAll()
    {
        RefreshTodos();
        RefreshDone();
        RefreshExpenses();
        RefreshMissing();
        LoadSettingsIntoControls();
    }

    // ================================================================= Todos

    private void BuildTodosTab()
    {
        var toolbar = MakeToolbar(_tabTodos, _gridTodos);

        var btnAdd = Theme.MakeButton("＋ Add todo");
        btnAdd.Click += (_, _) => { using var f = new TodoEditForm(_store, null); f.ShowDialog(this); };
        var btnEdit = Theme.MakeButton("Edit…");
        btnEdit.Click += (_, _) => EditSelectedTodo();
        var btnDone = Theme.MakeButton("✓ Complete…");
        btnDone.Click += (_, _) => { if (SelectedTodo() is { } t) UiActions.ConfirmComplete(this, _store, t); };
        var btnSnooze = Theme.MakeButton("Snooze ▾");
        btnSnooze.Click += (_, _) =>
        {
            if (SelectedTodo() is not { } t) return;
            var menu = new ContextMenuStrip();
            var root = new ToolStripMenuItem("snooze");
            UiActions.AddSnoozeMenu(root, _store, t);
            foreach (ToolStripItem it in root.DropDownItems.Cast<ToolStripItem>().ToArray()) menu.Items.Add(it);
            menu.Show(btnSnooze, new Point(0, btnSnooze.Height));
        };
        var btnDelete = Theme.MakeButton("Delete…");
        btnDelete.Click += (_, _) => { if (SelectedTodo() is { } t) UiActions.DeleteTodo(this, _store, t); };
        toolbar.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDone, btnSnooze, btnDelete });

        Theme.StyleGrid(_gridTodos);
        _gridTodos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AddCol(_gridTodos, "", 30, fill: 0);
        AddCol(_gridTodos, "Title", 0, fill: 30);
        AddCol(_gridTodos, "Deadline", 150, fill: 0);
        AddCol(_gridTodos, "In", 120, fill: 0);
        AddCol(_gridTodos, "Repeats", 90, fill: 0);
        AddCol(_gridTodos, "Snoozed", 120, fill: 0);
        AddCol(_gridTodos, "Notes", 0, fill: 22);
        _gridTodos.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelectedTodo(); };
    }

    private TodoItem? SelectedTodo() =>
        _gridTodos.SelectedRows.Count > 0 ? _gridTodos.SelectedRows[0].Tag as TodoItem : null;

    private void EditSelectedTodo()
    {
        if (SelectedTodo() is not { } t) return;
        using var f = new TodoEditForm(_store, t);
        f.ShowDialog(this);
    }

    private void RefreshTodos()
    {
        var now = DateTime.Now;
        var selId = SelectedTodo()?.Id;
        var items = UrgencyService.SortForDisplay(_store.Todos.Where(t => !t.IsDone), now);

        _gridTodos.Rows.Clear();
        foreach (var t in items)
        {
            var tier = UrgencyService.GetTier(t, now);
            var color = UrgencyService.TierColor(tier);
            int i = _gridTodos.Rows.Add(
                "●",
                t.Title,
                t.Deadline?.ToString("ddd, MMM d yyyy · h:mm tt") ?? "—",
                UrgencyService.DueText(t, now),
                RecurrenceService.Describe(t),
                t.IsSnoozed ? "until " + t.SnoozedUntil!.Value.ToString("MMM d, h:mm tt") : "",
                OneLine(t.Notes));
            var row = _gridTodos.Rows[i];
            row.Tag = t;
            row.Cells[0].Style.ForeColor = color;
            row.Cells[0].Style.SelectionForeColor = color;
            row.Cells[3].Style.ForeColor = color;
            row.Cells[3].Style.SelectionForeColor = color;
            if (tier == UrgencyTier.Overdue)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(56, 40, 44);
            }
            if (t.IsSnoozed) row.DefaultCellStyle.ForeColor = Theme.SubText;
        }
        ReselectRow(_gridTodos, selId, o => (o as TodoItem)?.Id);
    }

    // ================================================================= Done

    private void BuildDoneTab()
    {
        var toolbar = MakeToolbar(_tabDone, _gridDone);

        var btnRestore = Theme.MakeButton("Restore to open");
        btnRestore.Click += (_, _) =>
        {
            if (_gridDone.SelectedRows.Count == 0 || _gridDone.SelectedRows[0].Tag is not TodoItem t) return;
            t.CompletedAt = null;
            _store.SaveTodos();
        };
        var btnForget = Theme.MakeButton("Delete forever…");
        btnForget.Click += (_, _) =>
        {
            if (_gridDone.SelectedRows.Count == 0 || _gridDone.SelectedRows[0].Tag is not TodoItem t) return;
            if (MessageBox.Show(this, $"Permanently delete \"{t.Title}\" from history?", "Delete",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _store.Todos.Remove(t);
            _store.SaveTodos();
        };
        var btnClear = Theme.MakeButton("Clear all done…");
        btnClear.Click += (_, _) =>
        {
            int n = _store.Todos.Count(t => t.IsDone);
            if (n == 0) return;
            if (MessageBox.Show(this, $"Permanently delete all {n} completed items?", "Clear history",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _store.Todos.RemoveAll(t => t.IsDone);
            _store.SaveTodos();
        };
        toolbar.Controls.AddRange(new Control[] { btnRestore, btnForget, btnClear });

        Theme.StyleGrid(_gridDone);
        _gridDone.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AddCol(_gridDone, "Title", 0, fill: 40);
        AddCol(_gridDone, "Completed", 200, fill: 0);
        AddCol(_gridDone, "Was due", 200, fill: 0);
        AddCol(_gridDone, "Notes", 0, fill: 25);
    }

    private void RefreshDone()
    {
        _gridDone.Rows.Clear();
        foreach (var t in _store.Todos.Where(t => t.IsDone).OrderByDescending(t => t.CompletedAt))
        {
            int i = _gridDone.Rows.Add(
                t.Title,
                t.CompletedAt?.ToString("ddd, MMM d yyyy · h:mm tt") ?? "",
                t.Deadline?.ToString("MMM d yyyy") ?? "—",
                OneLine(t.Notes));
            _gridDone.Rows[i].Tag = t;
        }
        _tabDone.Text = _gridDone.Rows.Count > 0 ? $"Done ({_gridDone.Rows.Count})" : "Done";
    }

    // ================================================================= Expenses

    private void BuildExpensesTab()
    {
        var toolbar = MakeToolbar(_tabExpenses, _gridExpenses);

        var btnAdd = Theme.MakeButton("＋ Add expense");
        btnAdd.Click += (_, _) => { using var f = new ExpenseEditForm(_store, null); f.ShowDialog(this); };
        var btnEdit = Theme.MakeButton("Edit…");
        btnEdit.Click += (_, _) => EditSelectedExpense();
        var btnDelete = Theme.MakeButton("Delete…");
        btnDelete.Click += (_, _) => DeleteSelectedExpense();

        _cmbYear.Margin = new Padding(16, 8, 0, 4);
        Theme.StyleInput(_cmbYear);
        _cmbYear.SelectedIndexChanged += (_, _) => RefreshExpenses();

        var btnExport = Theme.MakeButton("Export CSV…");
        btnExport.Click += (_, _) => ExportCsv();
        var btnFolder = Theme.MakeButton("Attachments folder");
        btnFolder.Click += (_, _) =>
        {
            var e = SelectedExpense();
            UiActions.OpenExternal(this, e != null && e.Attachments.Count > 0
                ? _store.AttachmentDirFor(e)
                : Store.AttachmentsDir);
        };

        toolbar.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDelete, _cmbYear, btnExport, btnFolder });

        _lblExpenseTotals.Dock = DockStyle.Bottom;
        _lblExpenseTotals.Height = 30;
        _lblExpenseTotals.TextAlign = ContentAlignment.MiddleLeft;
        _lblExpenseTotals.Padding = new Padding(10, 0, 0, 0);
        _lblExpenseTotals.ForeColor = Theme.SubText;
        _lblExpenseTotals.BackColor = Theme.Header;
        _tabExpenses.Controls.Add(_lblExpenseTotals);

        Theme.StyleGrid(_gridExpenses);
        _gridExpenses.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AddCol(_gridExpenses, "Date", 100, fill: 0);
        AddCol(_gridExpenses, "Description", 0, fill: 30);
        AddCol(_gridExpenses, "Category", 0, fill: 24);
        var amountCol = AddCol(_gridExpenses, "Amount", 100, fill: 0);
        amountCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        amountCol.DefaultCellStyle.Padding = new Padding(0, 0, 8, 0);
        AddCol(_gridExpenses, "Cur", 52, fill: 0);
        AddCol(_gridExpenses, "Recurring", 140, fill: 0);
        AddCol(_gridExpenses, "Att", 46, fill: 0);
        AddCol(_gridExpenses, "Link", 52, fill: 0);
        _gridExpenses.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelectedExpense(); };
        _gridExpenses.BringToFront();
    }

    private Expense? SelectedExpense() =>
        _gridExpenses.SelectedRows.Count > 0 ? _gridExpenses.SelectedRows[0].Tag as Expense : null;

    private void EditSelectedExpense()
    {
        if (SelectedExpense() is not { } e) return;
        using var f = new ExpenseEditForm(_store, e);
        f.ShowDialog(this);
    }

    private void DeleteSelectedExpense()
    {
        if (SelectedExpense() is not { } e) return;
        var extra = e.Attachments.Count > 0 ? $"\nIts {e.Attachments.Count} attachment(s) will be deleted too." : "";
        if (MessageBox.Show(this, $"Delete expense \"{e.Description}\" ({e.Amount:N2} {e.Currency})?{extra}",
                "Delete expense", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _store.Expenses.Remove(e);
        try
        {
            var dir = Path.Combine(Store.AttachmentsDir, e.Id);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch { /* best effort */ }
        _store.SaveExpenses();
    }

    private IEnumerable<Expense> FilteredExpenses()
    {
        var sel = _cmbYear.SelectedItem?.ToString();
        return int.TryParse(sel, out int year)
            ? _store.Expenses.Where(e => e.Date.Year == year)
            : _store.Expenses;
    }

    private void RefreshExpenses()
    {
        // Keep the year list in sync without fighting the user's selection.
        var years = _store.Expenses.Select(e => e.Date.Year).Distinct().OrderByDescending(y => y).ToList();
        var wanted = new List<string> { "All years" };
        wanted.AddRange(years.Select(y => y.ToString()));
        var current = _cmbYear.Items.Cast<string>().ToList();
        if (!wanted.SequenceEqual(current))
        {
            var keep = _cmbYear.SelectedItem?.ToString();
            _cmbYear.Items.Clear();
            foreach (var wItem in wanted) _cmbYear.Items.Add(wItem);
            _cmbYear.SelectedItem = keep != null && wanted.Contains(keep) ? keep : "All years";
            return; // SelectedIndexChanged re-enters RefreshExpenses
        }
        if (_cmbYear.SelectedIndex < 0) { _cmbYear.SelectedItem = "All years"; return; }

        var selId = SelectedExpense()?.Id;
        _gridExpenses.Rows.Clear();
        var list = FilteredExpenses().OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt).ToList();
        foreach (var e in list)
        {
            int i = _gridExpenses.Rows.Add(
                e.Date.ToString("yyyy-MM-dd"),
                e.Description,
                e.Category,
                e.Amount.ToString("N2"),
                e.Currency,
                e.IsRecurring ? "↻ " + e.EffectiveSeries : "",
                e.Attachments.Count > 0 ? e.Attachments.Count.ToString() : "",
                string.IsNullOrWhiteSpace(e.Link) ? "" : "link");
            var row = _gridExpenses.Rows[i];
            row.Tag = e;
            if (string.IsNullOrWhiteSpace(e.Link) == false)
            {
                row.Cells[7].Style.ForeColor = Theme.Accent;
                row.Cells[7].Style.SelectionForeColor = Theme.Accent;
            }
        }
        ReselectRow(_gridExpenses, selId, o => (o as Expense)?.Id);

        var totals = list.GroupBy(e => e.Currency)
            .OrderBy(gr => gr.Key)
            .Select(gr => $"{gr.Sum(x => x.Amount):N2} {gr.Key}");
        _lblExpenseTotals.Text = list.Count == 0
            ? "No expenses yet — click “＋ Add expense” and just dump it here. Sorting is for tax-time you."
            : $"{list.Count} expense(s)   ·   totals: {string.Join("   +   ", totals)}";
    }

    private void ExportCsv()
    {
        var sel = _cmbYear.SelectedItem?.ToString() ?? "all";
        using var dlg = new SaveFileDialog
        {
            Title = "Export expenses to CSV",
            Filter = "CSV|*.csv",
            FileName = $"doneyet-expenses-{(sel == "All years" ? "all" : sel)}-{DateTime.Now:yyyyMMdd}.csv",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var sb = new StringBuilder();
        sb.AppendLine("Date,Description,Category,Amount,Currency,Recurring,Series,Link,Attachments,AttachmentFolder,Notes");
        foreach (var e in FilteredExpenses().OrderBy(e => e.Date))
        {
            sb.AppendLine(string.Join(",",
                e.Date.ToString("yyyy-MM-dd"),
                Csv(e.Description),
                Csv(e.Category),
                e.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                e.Currency,
                e.IsRecurring ? "yes" : "no",
                Csv(e.IsRecurring ? e.EffectiveSeries : ""),
                Csv(e.Link),
                Csv(string.Join("; ", e.Attachments)),
                Csv(e.Attachments.Count > 0 ? Path.Combine(Store.AttachmentsDir, e.Id) : ""),
                Csv(e.Notes)));
        }
        try
        {
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            MessageBox.Show(this, "Exported. Your accountant will weep tears of joy.", "Export",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Export failed: " + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

    // ================================================================= Missing invoices

    private void BuildMissingTab()
    {
        var intro = new Label
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(10, 8, 10, 0),
            ForeColor = Theme.SubText,
            Text = "Every expense marked “Recurring” joins a series (by series name). Each month from the series' first entry " +
                   "to now should have one expense — the gaps below are the invoices you'll need to hunt down at tax time. " +
                   "Cancelled a subscription? Select it and click “Mark series ended”.",
        };

        _treeMissing.Dock = DockStyle.Fill;
        _treeMissing.BackColor = Theme.Bg;
        _treeMissing.ForeColor = Theme.Text;
        _treeMissing.BorderStyle = BorderStyle.None;
        _treeMissing.ShowLines = false;
        _treeMissing.FullRowSelect = true;
        _treeMissing.ItemHeight = 26;
        _treeMissing.Font = Theme.BodyFont;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(6, 4, 0, 0), BackColor = Theme.Header };
        var btnEnded = Theme.MakeButton("Mark series ended (stop tracking after last entry)");
        btnEnded.Click += (_, _) => MarkSeries(ended: true);
        var btnResume = Theme.MakeButton("Resume tracking");
        btnResume.Click += (_, _) => MarkSeries(ended: false);
        buttons.Controls.AddRange(new Control[] { btnEnded, btnResume });

        _tabMissing.Controls.Add(_treeMissing);
        _tabMissing.Controls.Add(intro);
        _tabMissing.Controls.Add(buttons);
        _treeMissing.BringToFront();
    }

    private void MarkSeries(bool ended)
    {
        var node = _treeMissing.SelectedNode;
        while (node?.Parent != null) node = node.Parent;
        if (node?.Tag is not SeriesReport r)
        {
            MessageBox.Show(this, "Select a series first.", "DoneYet", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (ended) _store.EndedSeries[r.Series] = MissingInvoiceService.MonthKey(r.LastMonth);
        else _store.EndedSeries.Remove(r.Series);
        _store.SaveExpenses();
    }

    private void RefreshMissing()
    {
        var now = DateTime.Now;
        var reports = MissingInvoiceService.Build(_store, now);
        var currentMonth = new DateTime(now.Year, now.Month, 1);

        _treeMissing.BeginUpdate();
        _treeMissing.Nodes.Clear();
        if (reports.Count == 0)
        {
            _treeMissing.Nodes.Add(new TreeNode(
                "No recurring series yet — tick “Recurring” on an expense (e.g. that USD subscription) to start tracking months.")
            { ForeColor = Theme.SubText });
        }
        foreach (var r in reports)
        {
            var title = $"{r.Series}  —  {r.EntryCount} month(s) on file · avg {r.AvgAmount:N2} {r.Currency} · " +
                        $"{r.FirstMonth:MMM yyyy} → {r.LastMonth:MMM yyyy}";
            if (r.Ended && r.EndedMonth.HasValue) title += $"  ·  ended {r.EndedMonth.Value:MMM yyyy}";
            if (r.MissingMonths.Count > 0) title += $"  ·  {r.MissingMonths.Count} MISSING";

            var node = new TreeNode(title)
            {
                Tag = r,
                ForeColor = r.MissingMonths.Count > 0 ? UrgencyService.TierColor(UrgencyTier.Overdue) : Theme.Ok,
            };
            foreach (var m in r.MissingMonths)
            {
                node.Nodes.Add(new TreeNode(
                    m == currentMonth ? $"{m:MMMM yyyy} — missing (this month, maybe not billed yet)" : $"{m:MMMM yyyy} — missing")
                {
                    ForeColor = m == currentMonth
                        ? UrgencyService.TierColor(UrgencyTier.Soon)
                        : UrgencyService.TierColor(UrgencyTier.Overdue),
                });
            }
            _treeMissing.Nodes.Add(node);
        }
        _treeMissing.ExpandAll();
        _treeMissing.EndUpdate();

        int totalMissing = reports.Sum(r => r.MissingMonths.Count);
        _tabMissing.Text = totalMissing > 0 ? $"Missing invoices ({totalMissing})" : "Missing invoices";
    }

    // ================================================================= Settings

    private void BuildSettingsTab()
    {
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16) };
        _tabSettings.Controls.Add(panel);
        int y = 16;
        const int x = 20, indent = 40;

        void Header(string text)
        {
            panel.Controls.Add(new Label
            {
                Text = text, AutoSize = true, Location = new Point(x, y),
                Font = Theme.TitleFont, ForeColor = Theme.Accent,
            });
            y += 30;
        }
        void Place(Control c, int cx, int dy = 30)
        {
            c.Location = new Point(cx, y);
            panel.Controls.Add(c);
            y += dy;
        }
        Label Note(string text, int cx)
        {
            var l = new Label { Text = text, AutoSize = true, Location = new Point(cx, y), ForeColor = Theme.SubText, Font = Theme.SubFont };
            panel.Controls.Add(l);
            y += 24;
            return l;
        }
        void Inline(params Control[] cs)
        {
            int cx = indent;
            foreach (var c in cs)
            {
                c.Location = new Point(cx, c is Label ? y + 4 : y);
                panel.Controls.Add(c);
                cx += c.Width + 10;
            }
            y += 34;
        }
        Label L(string t) => new() { Text = t, AutoSize = true, ForeColor = Theme.Text, Font = Theme.BodyFont };

        // ---- Reminders ----
        Header("Reminders");
        Place(_chkReminders, indent);
        _cmbBaseline.Items.AddRange(new object[]
        {
            "Every 4 hours", "Twice daily (8 AM & 4 PM)", "Daily (8 AM)",
            "Every 3 days", "Weekly (Monday 8 AM)", "Custom: every N hours",
        });
        Inline(L("Baseline nag while anything is open:"), _cmbBaseline, L("N ="), _numCustomHours);
        Place(_chkEscalation, indent);
        Inline(L("due within 3 days: every"), _numSoon, L("h        due within 24 h: every"), _numUrgent,
               L("h        overdue: every"), _numOverdue, L("h"));
        Note("(due within 7 days = once a day — and everything is always dismissible; nothing ever pops over your work)", indent);
        Inline(_chkQuiet, _dtQuietFrom, L("and"), _dtQuietTo);
        y += 8;

        // ---- Sounds ----
        Header("Sounds");
        Place(_chkSound, indent);
        Inline(L("Gentle (no deadline pressure):"), _cmbSndNormal, TestBtn(_cmbSndNormal));
        Inline(L("Due soon (≤ 3 days):"), _cmbSndSoon, TestBtn(_cmbSndSoon));
        Inline(L("Due today (≤ 24 h):"), _cmbSndUrgent, TestBtn(_cmbSndUrgent));
        Inline(L("Overdue:"), _cmbSndOverdue, TestBtn(_cmbSndOverdue));
        var btnSounds = Theme.MakeButton("Open sounds folder…");
        btnSounds.Click += (_, _) => UiActions.OpenExternal(this, Store.SoundsDir);
        Place(btnSounds, indent, 34);
        Note("Drop your own .wav / .mp3 files in that folder — they appear in the lists above. Scarier sound = higher urgency.", indent);
        y += 8;

        // ---- Widget ----
        Header("Desktop widget");
        Place(_chkWidget, indent);
        _cmbWidgetMode.Items.AddRange(new object[]
        {
            "Desktop — glued behind your windows", "Normal window", "Always on top",
        });
        Inline(L("Mode:"), _cmbWidgetMode);
        _lblOpacityVal.ForeColor = Theme.SubText;
        Inline(L("Opacity:"), _trkOpacity, _lblOpacityVal);
        Inline(L("Show at most"), _numMaxItems, L("items (the rest collapse into “+N more”)"));
        Place(_chkClickThrough, indent);
        y += 8;

        // ---- General ----
        Header("General");
        Place(_chkPetty, indent);
        Place(_chkAutostart, indent);
        Inline(L("Default expense currency:"), _cmbDefCurrency);
        var btnData = Theme.MakeButton("Open data folder…");
        btnData.Click += (_, _) => UiActions.OpenExternal(this, Store.DataDir);
        Place(btnData, indent, 34);
        Note($"Everything lives in {Store.DataDir} as plain JSON — easy to back up, easy to peek at.", indent);

        _cmbDefCurrency.Items.AddRange(new object[] { "CAD", "USD" });
        foreach (var c in new Control[] { _cmbBaseline, _numCustomHours, _numSoon, _numUrgent, _numOverdue,
                     _cmbSndNormal, _cmbSndSoon, _cmbSndUrgent, _cmbSndOverdue, _cmbWidgetMode, _numMaxItems, _cmbDefCurrency })
            Theme.StyleInput(c);

        WireSettingsEvents();
    }

    private Button TestBtn(ComboBox cmb)
    {
        var b = Theme.MakeButton("▶", 34);
        b.Click += (_, _) => { if (cmb.SelectedItem is string f) SoundService.Play(f); };
        return b;
    }

    private void WireSettingsEvents()
    {
        void OnChange(Action<AppSettings> apply)
        {
            if (_loadingSettings) return;
            apply(_store.Settings);
            _store.SaveSettings();
        }

        _chkReminders.CheckedChanged += (_, _) => OnChange(s => s.RemindersEnabled = _chkReminders.Checked);
        _cmbBaseline.SelectedIndexChanged += (_, _) => OnChange(s =>
        {
            s.Baseline = (BaselineMode)Math.Max(0, _cmbBaseline.SelectedIndex);
            _numCustomHours.Enabled = s.Baseline == BaselineMode.CustomHours;
        });
        _numCustomHours.ValueChanged += (_, _) => OnChange(s => s.CustomHours = (int)_numCustomHours.Value);
        _chkEscalation.CheckedChanged += (_, _) => OnChange(s => s.EscalationEnabled = _chkEscalation.Checked);
        _numSoon.ValueChanged += (_, _) => OnChange(s => s.HoursWithin3Days = (int)_numSoon.Value);
        _numUrgent.ValueChanged += (_, _) => OnChange(s => s.HoursWithin24h = (int)_numUrgent.Value);
        _numOverdue.ValueChanged += (_, _) => OnChange(s => s.HoursOverdue = (int)_numOverdue.Value);
        _chkQuiet.CheckedChanged += (_, _) => OnChange(s => s.QuietHoursEnabled = _chkQuiet.Checked);
        _dtQuietFrom.ValueChanged += (_, _) => OnChange(s => s.QuietStart = _dtQuietFrom.Value.TimeOfDay);
        _dtQuietTo.ValueChanged += (_, _) => OnChange(s => s.QuietEnd = _dtQuietTo.Value.TimeOfDay);

        _chkSound.CheckedChanged += (_, _) => OnChange(s => s.SoundEnabled = _chkSound.Checked);
        WireSoundCombo(_cmbSndNormal, (s, v) => s.SoundNormal = v);
        WireSoundCombo(_cmbSndSoon, (s, v) => s.SoundSoon = v);
        WireSoundCombo(_cmbSndUrgent, (s, v) => s.SoundUrgent = v);
        WireSoundCombo(_cmbSndOverdue, (s, v) => s.SoundOverdue = v);

        _chkWidget.CheckedChanged += (_, _) => OnChange(s => s.ShowWidget = _chkWidget.Checked);
        _cmbWidgetMode.SelectedIndexChanged += (_, _) => OnChange(s => s.WidgetMode = (WidgetMode)Math.Max(0, _cmbWidgetMode.SelectedIndex));
        _trkOpacity.ValueChanged += (_, _) =>
        {
            _lblOpacityVal.Text = _trkOpacity.Value + "%";
            if (!_loadingSettings) _store.Settings.WidgetOpacity = _trkOpacity.Value;
        };
        _trkOpacity.MouseUp += (_, _) => { if (!_loadingSettings) _store.SaveSettings(); };
        _trkOpacity.KeyUp += (_, _) => { if (!_loadingSettings) _store.SaveSettings(); };
        _numMaxItems.ValueChanged += (_, _) => OnChange(s => s.WidgetMaxItems = (int)_numMaxItems.Value);
        _chkClickThrough.CheckedChanged += (_, _) => OnChange(s => s.WidgetClickThrough = _chkClickThrough.Checked);

        _chkPetty.CheckedChanged += (_, _) => OnChange(s => s.PettyMode = _chkPetty.Checked);
        _chkAutostart.CheckedChanged += (_, _) =>
        {
            if (_loadingSettings) return;
            try { StartupManager.SetEnabled(_chkAutostart.Checked); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't change autostart: " + ex.Message, "DoneYet",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        _cmbDefCurrency.SelectedIndexChanged += (_, _) => OnChange(s => s.DefaultCurrency = _cmbDefCurrency.SelectedItem?.ToString() ?? "CAD");
    }

    private void WireSoundCombo(ComboBox cmb, Action<AppSettings, string> assign)
    {
        cmb.DropDown += (_, _) => PopulateSoundCombo(cmb, cmb.SelectedItem?.ToString());
        cmb.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingSettings || cmb.SelectedItem is not string f) return;
            assign(_store.Settings, f);
            _store.SaveSettings();
        };
    }

    private static void PopulateSoundCombo(ComboBox cmb, string? select)
    {
        var files = SoundService.ListSounds();
        cmb.Items.Clear();
        foreach (var f in files) cmb.Items.Add(f);
        if (select != null && files.Contains(select)) cmb.SelectedItem = select;
    }

    private void LoadSettingsIntoControls()
    {
        var s = _store.Settings;
        _loadingSettings = true;
        try
        {
            _chkReminders.Checked = s.RemindersEnabled;
            _cmbBaseline.SelectedIndex = (int)s.Baseline;
            _numCustomHours.Value = Math.Clamp(s.CustomHours, 1, 48);
            _numCustomHours.Enabled = s.Baseline == BaselineMode.CustomHours;
            _chkEscalation.Checked = s.EscalationEnabled;
            _numSoon.Value = Math.Clamp(s.HoursWithin3Days, 1, 24);
            _numUrgent.Value = Math.Clamp(s.HoursWithin24h, 1, 24);
            _numOverdue.Value = Math.Clamp(s.HoursOverdue, 1, 24);
            _chkQuiet.Checked = s.QuietHoursEnabled;
            _dtQuietFrom.Value = DateTime.Today + s.QuietStart;
            _dtQuietTo.Value = DateTime.Today + s.QuietEnd;

            _chkSound.Checked = s.SoundEnabled;
            PopulateSoundCombo(_cmbSndNormal, s.SoundNormal);
            PopulateSoundCombo(_cmbSndSoon, s.SoundSoon);
            PopulateSoundCombo(_cmbSndUrgent, s.SoundUrgent);
            PopulateSoundCombo(_cmbSndOverdue, s.SoundOverdue);

            _chkWidget.Checked = s.ShowWidget;
            _cmbWidgetMode.SelectedIndex = (int)s.WidgetMode;
            _trkOpacity.Value = Math.Clamp(s.WidgetOpacity, 30, 100);
            _lblOpacityVal.Text = _trkOpacity.Value + "%";
            _numMaxItems.Value = Math.Clamp(s.WidgetMaxItems, 3, 30);
            _chkClickThrough.Checked = s.WidgetClickThrough;

            _chkPetty.Checked = s.PettyMode;
            try { _chkAutostart.Checked = StartupManager.IsEnabled(); } catch { }
            _cmbDefCurrency.SelectedItem = s.DefaultCurrency == "USD" ? "USD" : "CAD";
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    // ================================================================= helpers

    private FlowLayoutPanel MakeToolbar(TabPage page, Control fillControl)
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(6, 4, 0, 0),
            BackColor = Theme.Header,
        };
        fillControl.Dock = DockStyle.Fill;
        page.Controls.Add(fillControl);
        page.Controls.Add(toolbar);
        fillControl.BringToFront();
        return toolbar;
    }

    private static DataGridViewTextBoxColumn AddCol(DataGridView g, string header, int width, int fill)
    {
        var col = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = true,
        };
        if (fill > 0)
        {
            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            col.FillWeight = fill;
        }
        else
        {
            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            col.Width = width;
        }
        g.Columns.Add(col);
        return col;
    }

    private static void ReselectRow(DataGridView g, string? id, Func<object?, string?> idOf)
    {
        if (id == null) return;
        foreach (DataGridViewRow row in g.Rows)
        {
            if (idOf(row.Tag) == id)
            {
                row.Selected = true;
                if (row.Index >= 0 && g.Rows.Count > 0)
                    g.CurrentCell = row.Cells[Math.Min(1, row.Cells.Count - 1)];
                return;
            }
        }
    }

    private static string OneLine(string s) =>
        string.IsNullOrWhiteSpace(s) ? "" : s.Replace("\r", " ").Replace("\n", " ");
}
