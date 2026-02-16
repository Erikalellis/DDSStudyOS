using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;

namespace DDSStudyOS.App.Services;

public static class WindowHandle
{
    [DllImport("Microsoft.ui.xaml.dll", EntryPoint = "WindowNative_GetWindowHandle")]
    private static extern IntPtr WindowNative_GetWindowHandle(IntPtr window);

    public static IntPtr GetWindowHandle(Window window)
        => WinRT.Interop.WindowNative.GetWindowHandle(window);
}
