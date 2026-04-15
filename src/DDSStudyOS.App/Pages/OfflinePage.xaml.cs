using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using DDSStudyOS.App.Services;

namespace DDSStudyOS.App.Pages;

public sealed partial class OfflinePage : Page
{
    public OfflinePage()
    {
        this.InitializeComponent();
        Loaded += OfflinePage_Loaded;
    }

    private void OfflinePage_Loaded(object sender, RoutedEventArgs e)
    {
        ReloadAsync();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ReloadAsync();
    }

    // ── Reload ────────────────────────────────────────────────────────────────

    private void ReloadAsync()
    {
        try
        {
            LoadingRing.IsActive = true;
            CoursesStackPanel.Children.Clear();

            var items = OfflineCourseCache.GetAllCached();

            // Badge de contagem
            CountBadgeText.Text = items.Count.ToString();

            // Tamanho do cache
            var bytes = OfflineCourseCache.GetTotalCacheSizeBytes();
            CacheSizeText.Text = FormatBytes(bytes);

            // Estado vazio
            var isEmpty = items.Count == 0;
            EmptyStatePanel.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            ListScrollViewer.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
            ClearAllButton.IsEnabled = !isEmpty;

            if (!isEmpty)
            {
                foreach (var meta in items)
                {
                    CoursesStackPanel.Children.Add(BuildCard(meta));
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"OfflinePage: falha ao carregar lista de cache. {ex.Message}");
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    // ── Card builder ──────────────────────────────────────────────────────────

    private Border BuildCard(OfflineCourseMeta meta)
    {
        // Badges
        var badgeStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        if (meta.HasHtml)
        {
            badgeStack.Children.Add(MakeBadge("HTML", "#4A7C3F", "#88B87A"));
        }

        if (meta.HasVideo)
        {
            badgeStack.Children.Add(MakeBadge("Vídeo", "#4A3F7C", "#8A7AB8"));
        }

        var cachedDate = meta.CachedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

        // Left column: info
        var infoStack = new StackPanel { Spacing = 4 };
        infoStack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(meta.Title) ? meta.Url : meta.Title,
            FontSize = 15,
            FontWeight = new Windows.UI.Text.FontWeight(600),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
        });
        infoStack.Children.Add(new TextBlock
        {
            Text = $"Salvo em {cachedDate}",
            FontSize = 12,
            Opacity = 0.55,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
        });
        infoStack.Children.Add(badgeStack);

        // Right column: buttons
        var playBtn = new Button
        {
            Content = "▶ Assistir offline",
            Tag = meta,
            Margin = new Thickness(0, 0, 8, 0),
            IsEnabled = meta.HasHtml
        };
        playBtn.Click += OpenOffline_Click;

        var deleteBtn = new Button { Content = "🗑 Remover", Tag = meta };
        deleteBtn.Click += Delete_Click;

        var btnStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        btnStack.Children.Add(playBtn);
        btnStack.Children.Add(deleteBtn);

        // Row grid
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(infoStack, 0);
        Grid.SetColumn(btnStack, 1);
        grid.Children.Add(infoStack);
        grid.Children.Add(btnStack);

        return new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x1D, 0x22, 0x36)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x2D, 0x34, 0x4A)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = grid
        };
    }

    private static Border MakeBadge(string label, string bgHex, string fgHex)
    {
        return new Border
        {
            Background = new SolidColorBrush(ParseColor(bgHex, 0x50)),
            BorderBrush = new SolidColorBrush(ParseColor(fgHex, 0x99)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = new SolidColorBrush(ParseColor(fgHex, 0xFF))
            }
        };
    }

    private static Windows.UI.Color ParseColor(string hex, byte alpha)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6 &&
            byte.TryParse(hex[0..2], System.Globalization.NumberStyles.HexNumber, null, out var r) &&
            byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g) &&
            byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return Windows.UI.Color.FromArgb(alpha, r, g, b);
        }

        return Windows.UI.Color.FromArgb(alpha, 0x80, 0x80, 0x80);
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void OpenOffline_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not OfflineCourseMeta meta) return;
        if (!meta.HasHtml) return;

        AppState.PendingBrowserUrl = "file:///" + meta.LocalHtmlPath!.Replace('\\', '/');
        NavigateToTag("browser");
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not OfflineCourseMeta meta) return;
        try
        {
            OfflineCourseCache.DeleteCache(meta.Url);
            ReloadAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"OfflinePage: erro ao remover cache. {ex.Message}");
        }
    }

    private async void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new ContentDialog
            {
                Title = "Limpar tudo",
                Content = "Remover todos os cursos salvos offline? Esta ação não pode ser desfeita.",
                PrimaryButtonText = "Remover",
                CloseButtonText = "Cancelar",
                XamlRoot = this.XamlRoot
            };

            var result = await dlg.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var items = OfflineCourseCache.GetAllCached();
            foreach (var meta in items)
            {
                OfflineCourseCache.DeleteCache(meta.Url);
            }

            ReloadAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"OfflinePage: erro ao limpar tudo. {ex.Message}");
        }
    }

    private void GoToBrowser_Click(object sender, RoutedEventArgs e)
    {
        NavigateToTag("browser");
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private void NavigateToTag(string tag)
    {
        if (AppState.RequestNavigateTag is { } navigate)
        {
            navigate(tag);
            return;
        }

        var pageType = tag switch
        {
            "dashboard" => typeof(DashboardPage),
            "courses" => typeof(CoursesPage),
            "materials" => typeof(MaterialsPage),
            "agenda" => typeof(AgendaPage),
            "browser" => typeof(BrowserPage),
            "offline" => typeof(OfflinePage),
            "settings" => typeof(SettingsPage),
            "dev" => typeof(DevelopmentPage),
            _ => typeof(DashboardPage)
        };

        if (Frame?.CurrentSourcePageType != pageType)
        {
            Frame?.Navigate(pageType);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return string.Empty;
        if (bytes < 1_048_576) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1_073_741_824) return $"{bytes / 1_048_576.0:F1} MB";
        return $"{bytes / 1_073_741_824.0:F1} GB";
    }
}
