using System.Text.Json;
using System.Text.Json.Serialization;
using DoneYet.Models;

namespace DoneYet.Data;

/// <summary>
/// All app data lives as human-readable JSON in %APPDATA%\DoneYet.
/// Writes are atomic (temp file + rename); corrupt files are backed up, never silently deleted.
/// </summary>
public sealed class Store
{
    private class TodoFile
    {
        public int Version { get; set; } = 1;
        public List<TodoItem> Todos { get; set; } = new();
    }

    private class ExpenseFile
    {
        public int Version { get; set; } = 1;
        public List<Expense> Expenses { get; set; } = new();
        /// <summary>seriesName -> "yyyy-MM" last month to track (series cancelled after that).</summary>
        public Dictionary<string, string> EndedSeries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DoneYet");

    public static string SoundsDir => Path.Combine(DataDir, "Sounds");
    public static string AttachmentsDir => Path.Combine(DataDir, "Attachments");
    private static string TodosPath => Path.Combine(DataDir, "todos.json");
    private static string ExpensesPath => Path.Combine(DataDir, "expenses.json");
    private static string SettingsPath => Path.Combine(DataDir, "settings.json");
    public static string LogPath => Path.Combine(DataDir, "error.log");

    public List<TodoItem> Todos { get; private set; } = new();
    public List<Expense> Expenses { get; private set; } = new();
    public Dictionary<string, string> EndedSeries { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public AppSettings Settings { get; private set; } = new();

    /// <summary>Fired after any Save*. UI listens to refresh itself. Single UI thread, so no locking.</summary>
    public event Action? Changed;

    public void Load()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(SoundsDir);
        Directory.CreateDirectory(AttachmentsDir);

        var todoFile = LoadFile<TodoFile>(TodosPath) ?? new TodoFile();
        Todos = todoFile.Todos;

        var expFile = LoadFile<ExpenseFile>(ExpensesPath) ?? new ExpenseFile();
        Expenses = expFile.Expenses;
        EndedSeries = new Dictionary<string, string>(expFile.EndedSeries, StringComparer.OrdinalIgnoreCase);

        Settings = LoadFile<AppSettings>(SettingsPath) ?? new AppSettings();
    }

    private static T? LoadFile<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOpts);
        }
        catch (Exception ex)
        {
            // Keep the broken file around so nothing is ever lost to a bad write/edit.
            try
            {
                File.Copy(path, path + ".bad-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), overwrite: true);
                Log($"Failed to parse {Path.GetFileName(path)}: {ex.Message}. Backed it up and started fresh.");
            }
            catch { /* best effort */ }
            return null;
        }
    }

    private static void SaveFile<T>(string path, T data)
    {
        Directory.CreateDirectory(DataDir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, JsonOpts));
        File.Move(tmp, path, overwrite: true);
    }

    public void SaveTodos(bool notify = true)
    {
        SaveFile(TodosPath, new TodoFile { Todos = Todos });
        if (notify) Changed?.Invoke();
    }

    public void SaveExpenses(bool notify = true)
    {
        SaveFile(ExpensesPath, new ExpenseFile { Expenses = Expenses, EndedSeries = EndedSeries });
        if (notify) Changed?.Invoke();
    }

    public void SaveSettings(bool notify = true)
    {
        SaveFile(SettingsPath, Settings);
        if (notify) Changed?.Invoke();
    }

    public string AttachmentDirFor(Expense e)
    {
        var dir = Path.Combine(AttachmentsDir, e.Id);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { /* never crash over logging */ }
    }
}
