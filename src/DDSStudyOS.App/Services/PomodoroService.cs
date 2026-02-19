using System;
using System.Timers;

namespace DDSStudyOS.App.Services;

public class PomodoroService
{
    private System.Timers.Timer _timer;
    private int _secondsRemaining;
    private int _totalSeconds;
    private bool _isWorkMode = true;
    
    private Action<string, double, string, bool> _onTick;
    private Action _onComplete;

    public bool IsRunning { get; private set; }
    public bool IsWorkMode => _isWorkMode;
    public bool HasActiveSession => _secondsRemaining > 0 && _totalSeconds > 0;
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
        minutes = Math.Clamp(minutes, 1, 180);
        _totalSeconds = minutes * 60;
        _secondsRemaining = _totalSeconds;
        IsRunning = true;
        _timer.Start();
        NotifyTick();
    }

    public void Resume()
    {
        if (!HasActiveSession)
        {
            return;
        }

        IsRunning = true;
        _timer.Start();
        NotifyTick();
    }

    public void Pause()
    {
        _timer.Stop();
        IsRunning = false;
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
            int elapsed = _totalSeconds - _secondsRemaining;
            progress = (double)elapsed / _totalSeconds;
        }
        
        _onTick?.Invoke(TimeDisplay, progress, CurrentStatus, _isWorkMode);
    }
}
