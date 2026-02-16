using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace DDSStudyOS.App;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        Services.AppState.MainWindow = window;
        window.Activate();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Services.AppLogger.Error("Exceção não tratada na UI.", e.Exception);
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
}
