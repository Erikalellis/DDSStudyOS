using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DDSStudyOS.App.Services;

public static class ToastService
{
    private static bool _initialized;
    private static bool _unavailable;
    private const int AppModelErrorNoPackage = 15700;
    private const int ErrorInsufficientBuffer = 122;

    public static void EnsureInitialized()
    {
        if (_initialized || _unavailable) return;

        if (!HasPackageIdentity())
        {
            _unavailable = true;
            AppLogger.Warn("ToastService: app sem identidade de pacote; usando fallback in-app para lembretes.");
            return;
        }

        if (!AppNotificationManager.IsSupported())
        {
            _unavailable = true;
            AppLogger.Warn("ToastService: AppNotification nao suportado neste ambiente.");
            return;
        }

        try
        {
            // Registra o app para toasts (WinAppSDK)
            AppNotificationManager.Default.Register();
            _initialized = true;
        }
        catch (Exception ex)
        {
            _unavailable = true;
            AppLogger.Warn($"ToastService: falha ao registrar notificacoes nativas. Motivo: {ex.Message}");
        }
    }

    public static void ShowReminderToast(string title, string body)
    {
        EnsureInitialized();
        if (!_initialized)
        {
            throw new InvalidOperationException("Toast nativo indisponivel para este ambiente.");
        }

        var notification = new AppNotificationBuilder()
            .AddText(title)
            .AddText(body)
            .BuildNotification();

        AppNotificationManager.Default.Show(notification);
    }

    private static bool HasPackageIdentity()
    {
        int length = 0;
        var result = GetCurrentPackageFullName(ref length, null);

        return result switch
        {
            ErrorInsufficientBuffer => true,
            AppModelErrorNoPackage => false,
            _ => false
        };
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);
}
