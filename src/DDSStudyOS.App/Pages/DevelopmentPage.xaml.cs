using Microsoft.UI.Xaml.Controls;
using System;
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

    private void Feedback_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var url = Services.SettingsService.FeedbackFormUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            url = "https://github.com/Erikalellis/DDSStudyOS/issues/new";
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }
}
