using System.Globalization;
using DoneYet.Data;
using DoneYet.Models;
using DoneYet.Services;

namespace DoneYet.UI;

/// <summary>
/// The "just drop it somewhere" expense box: description, date, amount (CAD/USD),
/// a tax category with plain-language examples, and attach invoices / screenshots
/// (including straight from the clipboard) or a link.
/// </summary>
public sealed class ExpenseEditForm : Form
{
    private sealed class AttRow
    {
        public string Name = "";
        public bool IsNew;
        public string? SourcePath; // staged copy from disk
        public Image? Img;         // staged clipboard paste
        public override string ToString() => Name + (IsNew ? "  (new)" : "");
    }

    private readonly Store _store;
    private readonly Expense? _existing;

    private readonly TextBox _txtDesc = new() { PlaceholderText = "What did you buy? (e.g. \"Figma annual plan\")" };
    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "ddd, MMM d yyyy" };
    private readonly TextBox _txtAmount = new() { TextAlign = HorizontalAlignment.Right, PlaceholderText = "0.00" };
    private readonly ComboBox _cmbCurrency = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbCategory = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _lblExamples = new() { AutoSize = false };
    private readonly TextBox _txtLink = new() { PlaceholderText = "https://… (receipt page, invoice portal)" };
    private readonly CheckBox _chkRecurring = new() { Text = "Recurring (monthly subscription)" };
    private readonly TextBox _txtSeries = new() { PlaceholderText = "series name, e.g. \"Adobe\" — groups the months together" };
    private readonly ListBox _lstAttach = new();
    private readonly TextBox _txtNotes = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };

    public ExpenseEditForm(Store store, Expense? existing)
    {
        _store = store;
        _existing = existing;

        Text = existing == null ? "Add expense" : "Edit expense";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(580, 568);
        Theme.ApplyForm(this);
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        int w = ClientSize.Width;

        AddLabel("Description", 12, 12);
        _txtDesc.SetBounds(12, 32, w - 24, 26);

        AddLabel("Date", 12, 70);
        _date.SetBounds(12, 90, 170, 26);
        AddLabel("Amount", 200, 70);
        _txtAmount.SetBounds(200, 90, 120, 26);
        AddLabel("Currency", 338, 70);
        _cmbCurrency.SetBounds(338, 90, 80, 26);
        _cmbCurrency.Items.AddRange(new object[] { "CAD", "USD" });
        _cmbCurrency.SelectedItem = store.Settings.DefaultCurrency == "USD" ? "USD" : "CAD";

        AddLabel("Tax category (examples show below — when in doubt, 'Other')", 12, 128);
        _cmbCategory.SetBounds(12, 148, w - 24, 26);
        _cmbCategory.Items.AddRange(TaxCategories.Names);
        _lblExamples.SetBounds(12, 178, w - 24, 34);
        _lblExamples.ForeColor = Theme.SubText;
        _lblExamples.Font = Theme.SubFont;
        _lblExamples.Text = "e.g. pick a category to see what belongs in it";
        _cmbCategory.SelectedIndexChanged += (_, _) =>
        {
            var name = _cmbCategory.SelectedItem?.ToString() ?? "";
            _lblExamples.Text = "e.g. " + TaxCategories.ExamplesFor(name);
        };

        AddLabel("Link", 12, 218);
        _txtLink.SetBounds(12, 238, w - 116, 26);
        var btnLink = Theme.MakeButton("Open", 88);
        btnLink.SetBounds(w - 100, 237, 88, 27);
        btnLink.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_txtLink.Text)) UiActions.OpenExternal(this, _txtLink.Text.Trim());
        };

        _chkRecurring.SetBounds(12, 274, 240, 24);
        _txtSeries.SetBounds(256, 273, w - 268, 26);
        _txtSeries.Enabled = false;
        _chkRecurring.CheckedChanged += (_, _) => _txtSeries.Enabled = _chkRecurring.Checked;

        AddLabel("Attachments — invoices, receipts, screenshots", 12, 310);
        _lstAttach.SetBounds(12, 330, w - 136, 92);
        var btnAddFile = Theme.MakeButton("Add files…", 110);
        var btnPaste = Theme.MakeButton("Paste image", 110);
        var btnOpen = Theme.MakeButton("Open", 110);
        var btnRemove = Theme.MakeButton("Remove", 110);
        btnAddFile.SetBounds(w - 122, 330, 110, 26);
        btnPaste.SetBounds(w - 122, 360, 110, 26);
        btnOpen.SetBounds(w - 122, 390, 110, 26);
        btnRemove.SetBounds(w - 122, 420, 110, 26);
        btnAddFile.Click += (_, _) => AddFiles();
        btnPaste.Click += (_, _) => PasteImage();
        btnOpen.Click += (_, _) => OpenSelected();
        btnRemove.Click += (_, _) => { if (_lstAttach.SelectedIndex >= 0) _lstAttach.Items.RemoveAt(_lstAttach.SelectedIndex); };

        AddLabel("Notes", 12, 434);
        _txtNotes.SetBounds(12, 454, w - 136, 58);

        var ok = Theme.MakeButton("Save", 92);
        ok.SetBounds(w - 196, 526, 92, 30);
        ok.BackColor = Theme.Accent;
        ok.ForeColor = Color.White;
        ok.Click += (_, _) => Save();
        var cancel = Theme.MakeButton("Cancel", 88);
        cancel.SetBounds(w - 100, 526, 88, 30);
        cancel.Click += (_, _) => Close();

        foreach (var c in new Control[] { _txtDesc, _txtAmount, _cmbCurrency, _cmbCategory, _txtLink, _txtSeries, _lstAttach, _txtNotes })
            Theme.StyleInput(c);

        Controls.AddRange(new Control[]
        {
            _txtDesc, _date, _txtAmount, _cmbCurrency, _cmbCategory, _lblExamples,
            _txtLink, btnLink, _chkRecurring, _txtSeries, _lstAttach,
            btnAddFile, btnPaste, btnOpen, btnRemove, _txtNotes, ok, cancel,
        });

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
        if (_existing == null) return;
        _txtDesc.Text = _existing.Description;
        _date.Value = _existing.Date;
        _txtAmount.Text = _existing.Amount.ToString("0.00", CultureInfo.CurrentCulture);
        _cmbCurrency.SelectedItem = _existing.Currency == "USD" ? "USD" : "CAD";
        int idx = Array.IndexOf(TaxCategories.Names, _existing.Category);
        if (idx >= 0) _cmbCategory.SelectedIndex = idx;
        _txtLink.Text = _existing.Link;
        _chkRecurring.Checked = _existing.IsRecurring;
        _txtSeries.Text = _existing.SeriesName;
        _txtNotes.Text = _existing.Notes;
        foreach (var name in _existing.Attachments)
            _lstAttach.Items.Add(new AttRow { Name = name, IsNew = false });
    }

    private void AddFiles()
    {
        using var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Attach invoice / receipt / screenshot",
            Filter = "Documents & images|*.pdf;*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp;*.heic;*.eml;*.msg;*.txt;*.csv;*.xlsx;*.docx|All files|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        foreach (var f in dlg.FileNames)
            _lstAttach.Items.Add(new AttRow { Name = Path.GetFileName(f), IsNew = true, SourcePath = f });
    }

    private void PasteImage()
    {
        if (!Clipboard.ContainsImage())
        {
            MessageBox.Show(this, "No image on the clipboard. Screenshot something first (Win+Shift+S).",
                "Paste image", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var img = Clipboard.GetImage();
        if (img == null) return;
        _lstAttach.Items.Add(new AttRow
        {
            Name = "screenshot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png",
            IsNew = true,
            Img = img,
        });
    }

    private void OpenSelected()
    {
        if (_lstAttach.SelectedItem is not AttRow row) return;
        if (!row.IsNew && _existing != null)
            UiActions.OpenExternal(this, Path.Combine(_store.AttachmentDirFor(_existing), row.Name));
        else if (row.SourcePath != null)
            UiActions.OpenExternal(this, row.SourcePath);
        else
            MessageBox.Show(this, "Pasted image — save the expense first, then open it.", "DoneYet",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static bool TryParseAmount(string raw, out decimal amount)
    {
        raw = raw.Trim().Replace("$", "").Replace(" ", "");
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out amount) && amount >= 0)
            return true;
        return decimal.TryParse(raw.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
               && amount >= 0;
    }

    private void Save()
    {
        var desc = _txtDesc.Text.Trim();
        if (desc.Length == 0) { _txtDesc.Focus(); System.Media.SystemSounds.Beep.Play(); return; }

        if (!TryParseAmount(_txtAmount.Text, out var amount))
        {
            MessageBox.Show(this, "Couldn't read that amount. Plain numbers work best: 29.99",
                "Amount", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtAmount.Focus();
            return;
        }

        if (_cmbCategory.SelectedIndex < 0)
        {
            MessageBox.Show(this, "Pick a tax category — \"Other / ask accountant\" is always a safe parking spot.",
                "Category", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _cmbCategory.Focus();
            return;
        }

        var e = _existing ?? new Expense();
        e.Description = desc;
        e.Date = _date.Value.Date;
        e.Amount = Math.Round(amount, 2);
        e.Currency = _cmbCurrency.SelectedItem?.ToString() ?? "CAD";
        e.Category = _cmbCategory.SelectedItem?.ToString() ?? "";
        e.Link = _txtLink.Text.Trim();
        e.Notes = _txtNotes.Text.Trim();
        e.IsRecurring = _chkRecurring.Checked;
        e.SeriesName = _chkRecurring.Checked
            ? (_txtSeries.Text.Trim().Length > 0 ? _txtSeries.Text.Trim() : desc)
            : _txtSeries.Text.Trim();

        // Materialize attachments: copy staged files / save pasted images, delete removed ones.
        var rows = _lstAttach.Items.Cast<AttRow>().ToList();
        if (rows.Count == 0 && e.Attachments.Count == 0)
        {
            // No attachments before or after — don't litter empty folders.
            FinishSave(e);
            return;
        }
        var dir = _store.AttachmentDirFor(e);
        var kept = rows.Where(r => !r.IsNew).Select(r => r.Name).ToList();
        foreach (var oldName in e.Attachments.Except(kept))
        {
            try { File.Delete(Path.Combine(dir, oldName)); } catch { /* best effort */ }
        }

        var final = new List<string>(kept);
        var taken = new HashSet<string>(kept, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(r => r.IsNew))
        {
            try
            {
                var name = UniqueName(dir, taken, row.Name);
                var dest = Path.Combine(dir, name);
                if (row.SourcePath != null) File.Copy(row.SourcePath, dest, overwrite: false);
                else row.Img?.Save(dest, System.Drawing.Imaging.ImageFormat.Png);
                final.Add(name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Couldn't attach {row.Name}:\n{ex.Message}", "Attachment",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        e.Attachments = final;
        FinishSave(e);
    }

    private void FinishSave(Expense e)
    {
        if (_existing == null) _store.Expenses.Add(e);
        _store.SaveExpenses();
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string UniqueName(string dir, HashSet<string> taken, string desired)
    {
        var baseName = Path.GetFileNameWithoutExtension(desired);
        var ext = Path.GetExtension(desired);
        var name = baseName + ext;
        int n = 2;
        while (taken.Contains(name) || File.Exists(Path.Combine(dir, name)))
            name = $"{baseName} ({n++}){ext}";
        taken.Add(name);
        return name;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var row in _lstAttach.Items.OfType<AttRow>())
                row.Img?.Dispose();
        }
        base.Dispose(disposing);
    }
}
