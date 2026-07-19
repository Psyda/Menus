using System.Text.Json.Serialization;

namespace DoneYet.Models;

public class Expense
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Description { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }

    /// <summary>"CAD" or "USD".</summary>
    public string Currency { get; set; } = "CAD";

    public string Category { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Link { get; set; } = "";

    /// <summary>File names inside Data/Attachments/&lt;Id&gt;/.</summary>
    public List<string> Attachments { get; set; } = new();

    public bool IsRecurring { get; set; }

    /// <summary>Groups recurring expenses into a series (e.g. "Adobe") even when the amount varies month to month.</summary>
    public string SeriesName { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string EffectiveSeries =>
        string.IsNullOrWhiteSpace(SeriesName) ? Description.Trim() : SeriesName.Trim();
}
