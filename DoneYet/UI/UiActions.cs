using System.Diagnostics;
using DoneYet.Data;
using DoneYet.Models;
using DoneYet.Services;

namespace DoneYet.UI;

/// <summary>Shared user actions so the widget, manager and tray all behave identically.</summary>
public static class UiActions
{
    /// <summary>
    /// The one rule of DoneYet: nothing leaves the list without an explicit confirm.
    /// Recurring items roll forward to the next cycle instead of disappearing.
    /// </summary>
    public static bool ConfirmComplete(IWin32Window? owner, Store store, TodoItem t)
    {
        var msg = Petty.ConfirmPrompt(t.Title, store.Settings.PettyMode);
        if (t.IsRecurring && t.Deadline.HasValue)
            msg += $"\n\nRecurring ({RecurrenceService.Describe(t)}) — it will roll to the next cycle.";

        if (MessageBox.Show(owner, msg, "Done yet?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return false;

        if (t.IsRecurring && t.Deadline.HasValue)
        {
            RecurrenceService.CompleteCycle(t, DateTime.Now);
        }
        else
        {
            t.CompletedAt = DateTime.Now;
            t.SnoozedUntil = null;
        }
        store.SaveTodos();
        return true;
    }

    public static void Snooze(Store store, TodoItem t, TimeSpan span)
    {
        t.SnoozedUntil = DateTime.Now + span;
        store.SaveTodos();
    }

    public static void SnoozeUntilTomorrowMorning(Store store, TodoItem t)
    {
        t.SnoozedUntil = DateTime.Today.AddDays(1).AddHours(8);
        store.SaveTodos();
    }

    public static void Unsnooze(Store store, TodoItem t)
    {
        t.SnoozedUntil = null;
        store.SaveTodos();
    }

    public static void DeleteTodo(IWin32Window? owner, Store store, TodoItem t)
    {
        if (MessageBox.Show(owner, $"Delete \"{t.Title}\" without completing it?\nIt will NOT be archived — it just vanishes.",
                "Delete todo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        store.Todos.Remove(t);
        store.SaveTodos();
    }

    public static void AddSnoozeMenu(ToolStripMenuItem parent, Store store, TodoItem t)
    {
        parent.DropDownItems.Add("1 hour", null, (_, _) => Snooze(store, t, TimeSpan.FromHours(1)));
        parent.DropDownItems.Add("4 hours", null, (_, _) => Snooze(store, t, TimeSpan.FromHours(4)));
        parent.DropDownItems.Add("Tomorrow 8 AM", null, (_, _) => SnoozeUntilTomorrowMorning(store, t));
        parent.DropDownItems.Add("3 days", null, (_, _) => Snooze(store, t, TimeSpan.FromDays(3)));
        if (t.IsSnoozed)
        {
            parent.DropDownItems.Add(new ToolStripSeparator());
            parent.DropDownItems.Add("Wake up now", null, (_, _) => Unsnooze(store, t));
        }
    }

    public static void OpenExternal(IWin32Window? owner, string pathOrUrl)
    {
        try
        {
            Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, "Couldn't open: " + pathOrUrl + "\n\n" + ex.Message, "DoneYet",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
