using System.Runtime.InteropServices;

namespace DoneYet.Services;

/// <summary>Draws the tray icon at runtime: a colored badge with the attention count, or a checkmark when clear.</summary>
public static class TrayIconRenderer
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon Render(int count, Color color)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 1, 1, 30, 30);

            string text = count <= 0 ? "✓" : (count > 99 ? "99" : count.ToString());
            float px = text.Length >= 2 ? 17f : 20f;
            using var font = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, Brushes.White, new RectangleF(0, -1, 32, 33), sf);
        }

        // GetHicon leaks unless explicitly destroyed, and we re-render on every state change.
        IntPtr h = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(h);
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(h);
        }
    }
}
