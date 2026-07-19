using DoneYet.Data;
using DoneYet.Models;

namespace DoneYet.UI;

/// <summary>Add/edit a todo. Built for speed: title, hit a preset deadline button, Enter, gone.</summary>
public sealed class TodoEditForm : Form
{
    private readonly Store _store;
    private readonly TodoItem? _existing;

    private readonly TextBox _txtTitle = new();
    private readonly CheckBox _chkDeadline = new() { Text = "Deadline" };
    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "ddd, MMM d yyyy" };
    private readonly ComboBox _cmbTime = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbRecur = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _numN = new() { Minimum = 1, Maximum = 365, Value = 3, Width = 56 };
    private readonly Label _lblN = new() { Text = "days", AutoSize = true };
    private readonly TextBox _txtNotes = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };

    private static readonly TimeSpan[] TimePresets =
    {
        new(9, 0, 0), new(12, 0, 0), new(17, 0, 0), new(23, 59, 0),
    };
    private TimeSpan? _customTime;

    public TodoEditForm(Store store, TodoItem? existing)
    {
        _store = store;
        _existing = existing;

        Text = existing == null ? "Add todo" : "Edit todo";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(470, 356);
        Theme.ApplyForm(this);
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        int w = ClientSize.Width;

        AddLabel("What needs doing?", 12, 12);
        _txtTitle.SetBounds(12, 32, w - 24, 26);
        Theme.StyleInput(_txtTitle);

        AddLabel("Deadline", 12, 70);
        _chkDeadline.SetBounds(12, 90, 90, 24);
        _date.SetBounds(106, 90, 190, 26);
        _cmbTime.SetBounds(304, 90, 116, 26);
        Theme.StyleInput(_cmbTime);
        _cmbTime.Items.AddRange(new object[] { "9:00 AM", "Noon", "5:00 PM", "11:59 PM" });
        _cmbTime.SelectedIndex = 3;

        // One-click presets — the ADHD express lane.
        var presets = new (string, Func<DateTime>)[]
        {
            ("Today", () => DateTime.Today),
            ("Tomorrow", () => DateTime.Today.AddDays(1)),
            ("+3 days", () => DateTime.Today.AddDays(3)),
            ("Next week", () => DateTime.Today.AddDays(7)),
            ("No deadline", () => DateTime.MinValue),
        };
        int px = 12;
        foreach (var (label, dateFn) in presets)
        {
            var b = Theme.MakeButton(label);
            b.Location = new Point(px, 122);
            b.Font = Theme.SubFont;
            b.Click += (_, _) =>
            {
                var d = dateFn();
                if (d == DateTime.MinValue) { _chkDeadline.Checked = false; return; }
                _chkDeadline.Checked = true;
                _date.Value = d;
            };
            Controls.Add(b);
            px += TextRenderer.MeasureText(label, Theme.SubFont).Width + 26;
        }

        AddLabel("Repeats", 12, 162);
        _cmbRecur.SetBounds(106, 182, 190, 26);
        Theme.StyleInput(_cmbRecur);
        _cmbRecur.Items.AddRange(new object[] { "Never", "Daily", "Weekly", "Monthly", "Yearly", "Every N days" });
        _cmbRecur.SelectedIndex = 0;
        _numN.SetBounds(304, 183, 56, 26);
        Theme.StyleInput(_numN);
        _lblN.Location = new Point(364, 187);
        _lblN.ForeColor = Theme.SubText;
        _cmbRecur.SelectedIndexChanged += (_, _) =>
        {
            bool n = _cmbRecur.SelectedIndex == 5;
            _numN.Visible = n;
            _lblN.Visible = n;
        };
        _numN.Visible = _lblN.Visible = false;

        AddLabel("Notes", 12, 218);
        _txtNotes.SetBounds(12, 238, w - 24, 62);
        Theme.StyleInput(_txtNotes);

        var ok = Theme.MakeButton("Save", 92);
        ok.SetBounds(w - 196, 314, 92, 30);
        ok.BackColor = Theme.Accent;
        ok.ForeColor = Color.White;
        ok.Click += (_, _) => Save();
        var cancel = Theme.MakeButton("Cancel", 92);
        cancel.SetBounds(w - 100, 314, 88, 30);
        cancel.Click += (_, _) => Close();
        AcceptButton = ok;

        Controls.AddRange(new Control[]
        {
            _txtTitle, _chkDeadline, _date, _cmbTime, _cmbRecur, _numN, _lblN, _txtNotes, ok, cancel,
        });

        _chkDeadline.CheckedChanged += (_, _) =>
        {
            _date.Enabled = _cmbTime.Enabled = _chkDeadline.Checked;
        };

        LoadItem();
    }

    private void AddLabel(string text, int x, int y)
    {
        Controls.Add(new Label
        {
            Text = text, AutoSize = true, Location = new Point(x, y),
            ForeColor = Theme.SubText, Font = Theme.SubFont,
        });
    }

    private void LoadItem()
    {
        if (_existing == null)
        {
            _chkDeadline.Checked = false;
            _date.Value = DateTime.Today.AddDays(1);
            _date.Enabled = _cmbTime.Enabled = false;
            return;
        }

        _txtTitle.Text = _existing.Title;
        _txtNotes.Text = _existing.Notes;
        if (_existing.Deadline is DateTime d)
        {
            _chkDeadline.Checked = true;
            _date.Value = d.Date;
            int idx = Array.IndexOf(TimePresets, d.TimeOfDay);
            if (idx >= 0)
            {
                _cmbTime.SelectedIndex = idx;
            }
            else
            {
                _customTime = d.TimeOfDay;
                _cmbTime.Items.Add(d.ToString("h:mm tt"));
                _cmbTime.SelectedIndex = _cmbTime.Items.Count - 1;
            }
        }
        else
        {
            _chkDeadline.Checked = false;
            _date.Enabled = _cmbTime.Enabled = false;
        }

        _cmbRecur.SelectedIndex = _existing.Recurrence switch
        {
            RecurrenceKind.Daily => 1,
            RecurrenceKind.Weekly => 2,
            RecurrenceKind.Monthly => 3,
            RecurrenceKind.Yearly => 4,
            RecurrenceKind.EveryNDays => 5,
            _ => 0,
        };
        if (_existing.Recurrence == RecurrenceKind.EveryNDays)
            _numN.Value = Math.Clamp(_existing.RecurrenceN, 1, 365);
    }

    private void Save()
    {
        var title = _txtTitle.Text.Trim();
        if (title.Length == 0)
        {
            _txtTitle.Focus();
            System.Media.SystemSounds.Beep.Play();
            return;
        }

        DateTime? deadline = null;
        if (_chkDeadline.Checked)
        {
            var tod = _cmbTime.SelectedIndex >= 0 && _cmbTime.SelectedIndex < TimePresets.Length
                ? TimePresets[_cmbTime.SelectedIndex]
                : _customTime ?? new TimeSpan(23, 59, 0);
            deadline = _date.Value.Date + tod;
        }

        var kind = _cmbRecur.SelectedIndex switch
        {
            1 => RecurrenceKind.Daily,
            2 => RecurrenceKind.Weekly,
            3 => RecurrenceKind.Monthly,
            4 => RecurrenceKind.Yearly,
            5 => RecurrenceKind.EveryNDays,
            _ => RecurrenceKind.None,
        };

        var t = _existing ?? new TodoItem();
        bool deadlineChanged = t.Deadline != deadline;
        t.Title = title;
        t.Notes = _txtNotes.Text.Trim();
        t.Deadline = deadline;
        t.Recurrence = kind;
        t.RecurrenceN = (int)_numN.Value;
        t.RecurrenceAnchorDay = kind == RecurrenceKind.Monthly && deadline.HasValue ? deadline.Value.Day : 0;
        if (deadlineChanged) t.LastNotifiedAt = null; // fresh deadline, fresh reminder cycle

        if (_existing == null) _store.Todos.Add(t);
        _store.SaveTodos();
        DialogResult = DialogResult.OK;
        Close();
    }
}
