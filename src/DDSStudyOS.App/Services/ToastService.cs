using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;

namespace DDSStudyOS.App.Services;

public static class ToastService
{
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized) return;

        // Registra o app para toasts (WinAppSDK)
        AppNotificationManager.Default.Register();
        _initialized = true;
    }

    public static void ShowReminderToast(string title, string body)
    {
        EnsureInitialized();

        var notification = new AppNotificationBuilder()
            .AddText(title)
            .AddText(body)
            .BuildNotification();

        AppNotificationManager.Default.Show(notification);
    }
}
