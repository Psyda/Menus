# DoneYet

A tiny always-running Windows tray app for brains that lose track of deadlines.
Todos that **never disappear until you check *and confirm* them**, a glanceable
desktop widget, reminders that get more persistent as deadlines approach (but are
**always dismissible** — nothing ever pops over your work or steals focus), and a
zero-friction expense inbox that tells you at tax time which recurring invoices
you forgot to save.

Native WinForms (.NET 8). No Electron. Idles at ~0% CPU and a few dozen MB of RAM.

---

## Getting it

**Option A — download:** every push builds `DoneYet.exe` on GitHub Actions →
repo **Actions** tab → latest *Build DoneYet* run → artifact `DoneYet-win-x64`.
It's a single self-contained exe; put it anywhere and run it.

**Option B — build yourself** (needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)):

```powershell
cd DoneYet
.\publish.ps1        # -> dist\win-x64\DoneYet.exe
```

First run: you get a tray icon and the desktop widget. In **Settings → General**
tick **Start with Windows** so it's actually always running.

> Windows SmartScreen may warn on an unsigned exe — "More info → Run anyway".

## The widget

A borderless dark panel listing your todos, sorted by urgency, color-coded:

- 🔴 overdue · 🟠 due within 24 h · 🟡 within 3 days · 🔵 within 7 days · 🟢 later · ⚪ no deadline

Three modes (right-click the widget):

- **Desktop** *(default)* — glued **behind** your windows, like a live sticky note
  on the wallpaper. Always there when you glance at the desktop, never in the way.
- **Normal** — a regular floating window.
- **Always on top** — for deadline-day panic.

Drag it by its header, resize by the side edges, set opacity, or make it
click-through. Hover a row: click the circle to complete (it asks you to
*confirm* — the one rule), or the `z` to snooze. Double-click to edit.
The tray icon shows a live count, colored by the worst urgency.

## Reminders (the nagging system)

Two layers, both aggregated into **one** dismissible Windows notification at a
time — never a toast storm, never a modal popup:

1. **Baseline nag** while anything is open: every 4 h / twice daily (8 & 4) /
   daily / every 3 days / weekly / custom every N hours.
2. **Escalation** as a deadline closes in — within 7 days: daily · within
   3 days: every 6 h · within 24 h: every 3 h · overdue: every 3 h (all
   configurable). Item snooze, snooze-all, and mute are one click on the tray.

**Quiet hours** (default 22:00–08:00) hold everything until morning.

**Sounds:** each urgency tier gets its own sound so your ears learn the
difference between "someday" and "you are in danger". Four defaults are
generated on first run; drop your own `.wav`/`.mp3` into
`%APPDATA%\DoneYet\Sounds` and they show up in Settings. Mutable, disableable.

**Recurring todos** — internet bill (monthly, anchored to the day-of-month so
"the 31st" survives February), meds refill (every N days), house/business tax
(yearly). Confirming a cycle rolls the deadline forward; it never vanishes.

Petty mode is on by default: overdue items earn a rotating snarky remark —
*inside the app only*. Turn it off in Settings if the sass stops being motivating.

## Expenses (the lazy accounting inbox)

Tray → **Add expense…**: description, date, amount in **CAD or USD**, and a tax
category picked from a list with plain-language examples (Software & subscriptions —
"Adobe, Figma, hosting, domains"; Meals (50%) — "client lunch"; …loosely CRA
T2125-shaped, with an "Other / ask accountant" escape hatch). Attach invoice
files, **paste screenshots straight from the clipboard** (Win+Shift+S → Paste image),
and/or a link. Files are copied into the app's data folder so they're still there
in April.

**Recurring invoices:** tick *Recurring* and give it a series name (e.g. "Adobe").
Because USD subscriptions land at a different CAD amount every month, months are
matched by series name, not amount. The **Missing invoices** tab walks every month
from the series' first entry to today and lists the gaps — your tax-time
shopping list of invoices to hunt down. Cancelled something? *Mark series ended*
and it stops counting. The tab title shows the total, and the baseline reminder
mentions it occasionally.

**Export CSV** per year for your accountant, attachments folder one click away.

## Where your data lives

`%APPDATA%\DoneYet\` — `todos.json`, `expenses.json`, `settings.json`
(human-readable), `Attachments\`, `Sounds\`. Back up that folder and you have
everything. Corrupt files are backed up as `.bad-*`, never silently discarded.

## Notes

- Single instance: launching the exe again just opens the running app's manager.
- Exit fully via tray → *Exit DoneYet*. Closing the manager window only hides it.
- The category list is bookkeeping convenience, **not tax advice** — CRA wants
  amounts in CAD (Bank of Canada rate on the transaction date) when you file;
  the app records what you actually paid, which is exactly what your accountant
  wants to see alongside the invoice.
