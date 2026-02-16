using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DDSStudyOS.App.Services;

public sealed class ReminderNotificationService
{
    private readonly DatabaseService _db;
    private readonly ReminderRepository _repo;
    private DispatcherTimer? _timer;
    private bool _isStartupSweep = true;
    private bool _isProcessing;

    public ReminderNotificationService(DatabaseService db)
    {
        _db = db;
        _repo = new ReminderRepository(_db);
    }

    public void Start(Window window)
    {
        if (_timer != null) return;

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMinutes(1);
        _timer.Tick += async (_, __) => await ProcessNotificationCycleAsync(window);

        _timer.Start();
        _ = ProcessNotificationCycleAsync(window);
    }

    public void Stop()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer = null;
    }

    private async Task ProcessNotificationCycleAsync(Window window)
    {
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            await _db.EnsureCreatedAsync();
            var now = DateTimeOffset.Now;

            // Primeira varredura pega lembretes vencidos recentes para não perder eventos entre sessões.
            var from = _isStartupSweep ? now.AddDays(-7) : now.AddMinutes(-1);
            var to = now.AddMinutes(2);

            var hit = (await _repo.GetUnnotifiedDueAroundAsync(from, to, limit: 1))
                .OrderBy(r => r.DueAt)
                .FirstOrDefault();

            if (hit is null) return;

            try
            {
                ToastService.ShowReminderToast(
                    "Lembrete DDS StudyOS",
                    $"{hit.DueAt:dd/MM HH:mm} — {hit.Title}");
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Toast nativo indisponivel. Usando fallback in-app. Detalhe: {ex.Message}");

                var dlg = new ContentDialog
                {
                    Title = "Lembrete DDS StudyOS",
                    Content = $"{hit.DueAt:dd/MM HH:mm} — {hit.Title}",
                    CloseButtonText = "OK",
                    XamlRoot = window.Content.XamlRoot
                };

                await dlg.ShowAsync();
            }

            await _repo.MarkNotifiedAsync(hit.Id, DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Erro no ciclo de notificacao de lembretes.", ex);
        }
        finally
        {
            _isStartupSweep = false;
            _isProcessing = false;
        }
    }
}
