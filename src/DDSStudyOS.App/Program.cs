using Microsoft.UI.Xaml;
using System;
using WinRT;

namespace DDSStudyOS.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(_ =>
        {
            var app = new App();
        });
    }
}
