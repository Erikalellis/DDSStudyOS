using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace DDSStudyOS.App.Pages;

public sealed partial class DevelopmentPage : Page
{
    public DevelopmentPage()
    {
        this.InitializeComponent();
    }

    private void Email_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var url = "mailto:erikalellis.dev@gmail.com";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void Site_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var url = "https://177.71.165.60/";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
