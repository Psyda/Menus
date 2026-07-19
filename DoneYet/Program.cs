using DoneYet.App;
using DoneYet.Data;

namespace DoneYet;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Single instance: a second launch just tells the running one to show itself.
        using var mutex = new Mutex(initiallyOwned: true, @"Local\DoneYet.SingleInstance", out bool createdNew);
        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\DoneYet.ShowSignal");
        if (!createdNew)
        {
            showSignal.Set();
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // A background nag app must never die loudly: log and keep running.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Store.Log("UI exception: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Store.Log("Fatal: " + e.ExceptionObject);

        Application.Run(new TrayAppContext(showSignal));
        GC.KeepAlive(mutex);
    }
}
