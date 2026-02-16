using System;
using System.Runtime.InteropServices;

namespace DDSStudyOS.App.Services;

public static class TaskbarService
{
    // Interface ITaskbarList3 para manipular a barra de tarefas
    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        void MarkFullscreenWindow(IntPtr hwnd, int fFullscreen);
        void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
        void SetProgressState(IntPtr hwnd, TbpFlag tbpFlags);
    }

    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarInstance { }

    private static ITaskbarList3? _taskbarList;

    public enum TbpFlag
    {
        TBPF_NOPROGRESS = 0,
        TBPF_INDETERMINATE = 0x1,
        TBPF_NORMAL = 0x2,
        TBPF_ERROR = 0x4,
        TBPF_PAUSED = 0x8
    }

    public static void Initialize()
    {
        if (_taskbarList == null)
        {
            _taskbarList = (ITaskbarList3)new TaskbarInstance();
            _taskbarList.HrInit();
        }
    }

    public static void SetState(IntPtr hwnd, TbpFlag state)
    {
        try
        {
            Initialize();
            _taskbarList?.SetProgressState(hwnd, state);
        }
        catch { /* Ignorar erros de COM em versões antigas do Windows */ }
    }

    public static void SetValue(IntPtr hwnd, int currentValue, int maximumValue)
    {
        try
        {
            Initialize();
            _taskbarList?.SetProgressValue(hwnd, (ulong)currentValue, (ulong)maximumValue);
        }
        catch { /* Ignorar */ }
    }
}