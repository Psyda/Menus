namespace DoneYet.UI;

public static class Theme
{
    public static readonly Color Bg = Color.FromArgb(30, 33, 38);
    public static readonly Color Panel = Color.FromArgb(38, 42, 49);
    public static readonly Color Header = Color.FromArgb(46, 51, 59);
    public static readonly Color Hover = Color.FromArgb(52, 58, 67);
    public static readonly Color Border = Color.FromArgb(60, 66, 75);
    public static readonly Color Text = Color.FromArgb(232, 234, 237);
    public static readonly Color SubText = Color.FromArgb(152, 158, 166);
    public static readonly Color Accent = Color.FromArgb(86, 148, 217);
    public static readonly Color Danger = Color.FromArgb(226, 84, 84);
    public static readonly Color Ok = Color.FromArgb(96, 176, 128);

    public static readonly Font HeaderFont = new("Segoe UI", 10f, FontStyle.Bold);
    public static readonly Font TitleFont = new("Segoe UI", 9.75f, FontStyle.Bold);
    public static readonly Font BodyFont = new("Segoe UI", 9.25f);
    public static readonly Font SubFont = new("Segoe UI", 8.25f);
    public static readonly Font SubItalicFont = new("Segoe UI", 8.25f, FontStyle.Italic);

    public static void ApplyForm(Form f)
    {
        f.BackColor = Bg;
        f.ForeColor = Text;
        f.Font = BodyFont;
    }

    public static void StyleInput(Control c)
    {
        c.BackColor = Panel;
        c.ForeColor = Text;
        switch (c)
        {
            case TextBox tb: tb.BorderStyle = BorderStyle.FixedSingle; break;
            case ComboBox cb: cb.FlatStyle = FlatStyle.Flat; break;
            case ListBox lb: lb.BorderStyle = BorderStyle.FixedSingle; break;
        }
    }

    public static Button MakeButton(string text, int width = 0)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Header,
            ForeColor = Text,
            UseVisualStyleBackColor = false,
            AutoSize = width == 0,
            AutoSizeMode = width == 0 ? AutoSizeMode.GrowAndShrink : AutoSizeMode.GrowOnly,
            Padding = new Padding(8, 3, 8, 3),
            Margin = new Padding(4, 4, 0, 4),
        };
        if (width > 0) b.Width = width;
        b.FlatAppearance.BorderColor = Border;
        b.FlatAppearance.MouseOverBackColor = Hover;
        return b;
    }

    public static void StyleGrid(DataGridView g)
    {
        g.BorderStyle = BorderStyle.None;
        g.BackgroundColor = Bg;
        g.GridColor = Border;
        g.EnableHeadersVisualStyles = false;
        g.RowHeadersVisible = false;
        g.AllowUserToAddRows = false;
        g.AllowUserToDeleteRows = false;
        g.AllowUserToResizeRows = false;
        g.ReadOnly = true;
        g.MultiSelect = false;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        g.ColumnHeadersHeight = 34;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.RowTemplate.Height = 32;
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Header,
            ForeColor = Text,
            SelectionBackColor = Header,
            SelectionForeColor = Text,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Padding = new Padding(4, 0, 0, 0),
        };
        g.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Panel,
            ForeColor = Text,
            SelectionBackColor = Hover,
            SelectionForeColor = Text,
            Padding = new Padding(4, 0, 0, 0),
        };
    }
}
