using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DDSStudyOS.App;

public partial class App : Application
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    private const uint MbOk = 0x00000000;
    private const uint MbIconError = 0x00000010;

    public App()
    {
        Services.WebView2RuntimeChecker.EnsureUserDataFolderConfigured();
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Services.WebView2RuntimeChecker.EnsureUserDataFolderConfigured();

        var window = new MainWindow();
        Services.AppState.MainWindow = window;
        window.Activate();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Services.AppLogger.Error("Exceção não tratada na UI.", e.Exception);

        if (IsRecoverableUiException(e.Exception))
        {
            // Erros transitórios/cancelamento: mantém sessão viva.
            e.Handled = true;
            return;
        }

        // Erro crítico: não mascarar. Exibe aviso e deixa o app encerrar.
        TryShowFatalErrorMessage(e.Exception);
        e.Handled = false;
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Services.AppLogger.Error("Exceção não tratada no domínio da aplicação.", ex);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Services.AppLogger.Error("Task não observada.", e.Exception);
        e.SetObserved();
    }

    private static bool IsRecoverableUiException(Exception? ex)
    {
        if (ex is null)
        {
            return false;
        }

        if (ex is OperationCanceledException || ex is TaskCanceledException)
        {
            return true;
        }

        // HRESULT 0x800704C7: operação cancelada pelo usuário.
        if (ex.HResult == unchecked((int)0x800704C7))
        {
            return true;
        }

        return false;
    }

    private static void TryShowFatalErrorMessage(Exception ex)
    {
        try
        {
            var message =
                "O DDS StudyOS encontrou um erro crítico e será fechado." + Environment.NewLine +
                "Detalhes: " + ex.GetType().Name + Environment.NewLine +
                "Mensagem: " + ex.Message + Environment.NewLine + Environment.NewLine +
                "Consulte o log para diagnóstico.";

            _ = MessageBoxW(IntPtr.Zero, message, "DDS StudyOS - Erro Crítico", MbOk | MbIconError);
        }
        catch
        {
            // Não propaga erro da própria rotina de aviso.
        }
    }
}
