using System;
using System.Timers;

namespace DDSStudyOS.App.Services;

public class PomodoroService
{
    private System.Timers.Timer _timer;
    private int _secondsRemaining;
    private int _totalSeconds; // Para calcular progresso
    private bool _isWorkMode = true;
    
    // Callback agora recebe: TimeString, ProgressValue (0-100), StatusString, IsWorkMode
    private Action<string, double, string, bool> _onTick;
    private Action _onComplete;

    public bool IsRunning { get; private set; }
    public string CurrentStatus => _isWorkMode ? "Foco" : "Pausa";
    public string TimeDisplay => $"{_secondsRemaining / 60:D2}:{_secondsRemaining % 60:D2}";
    
    public PomodoroService(Action<string, double, string, bool> onTick, Action onComplete)
    {
        _onTick = onTick;
        _onComplete = onComplete;
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += Timer_Elapsed;
    }

    public void StartWork(int minutes = 25)
    {
        _isWorkMode = true;
        Start(minutes);
    }

    public void StartBreak(int minutes = 5)
    {
        _isWorkMode = false;
        Start(minutes);
    }

    private void Start(int minutes)
    {
        _totalSeconds = minutes * 60;
        _secondsRemaining = _totalSeconds;
        IsRunning = true;
        _timer.Start();
        NotifyTick();
    }

    public void Pause()
    {
        _timer.Stop();
        IsRunning = false;
        // Notifica estado pausado (sem mudar tempo)
        NotifyTick();
    }

    public void Stop()
    {
        _timer.Stop();
        IsRunning = false;
        _secondsRemaining = 0;
        _totalSeconds = 1; // Evitar div/0
        NotifyTick();
    }

    private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (_secondsRemaining > 0)
        {
            _secondsRemaining--;
            NotifyTick();
        }
        else
        {
            _timer.Stop();
            IsRunning = false;
            _onComplete?.Invoke();
        }
    }

    private void NotifyTick()
    {
        double progress = 0;
        if (_totalSeconds > 0)
        {
            // Progresso inverso (barra cheia no começo, vazia no final? ou enchendo?)
            // Windows Taskbar geralmente mostra "quanto já foi feito".
            // Para Pomodoro, "tempo decorrido" é o progresso.
            int elapsed = _totalSeconds - _secondsRemaining;
            progress = (double)elapsed / _totalSeconds;
        }
        
        _onTick?.Invoke(TimeDisplay, progress, CurrentStatus, _isWorkMode);
    }
}
