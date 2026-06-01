using System.Windows.Forms;

namespace CodexNotificationBooster.Tray;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\CodexNotificationBooster.Tray";

    [STAThread]
    private static void Main()
    {
        using var singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: SingleInstanceMutexName,
            createdNew: out var isFirstInstance);

        if (!isFirstInstance)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
