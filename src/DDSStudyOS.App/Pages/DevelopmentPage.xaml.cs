using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using DDSStudyOS.App.Services;

namespace DDSStudyOS.App.Pages;

public sealed partial class DevelopmentPage : Page
{
    public DevelopmentPage()
    {
        this.InitializeComponent();
        InitializeRoadmapHeader();
    }

    private void InitializeRoadmapHeader()
    {
        CurrentVersionText.Text = $"Versao atual: {AppReleaseInfo.BetaVersionDisplay}";
        NextUpdateTitleText.Text = $"O que esperar da próxima atualização (meta: v{GetNextTargetVersion()})";
    }

    private static string GetNextTargetVersion()
    {
        var current = AppReleaseInfo.Version;
        var nextMinor = current.Minor + 1;
        return $"{current.Major}.{nextMinor}.0-beta";
    }

    private void Email_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var url = "mailto:erikalellis.dev@gmail.com";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void Site_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var url = "http://177.71.165.60/";
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

