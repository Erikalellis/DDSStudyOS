using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Windows.Storage.Pickers;

namespace DDSStudyOS.App.Pages;

public sealed partial class CoursesPage : Page
{
    private readonly DatabaseService _db = new();
    private CourseRepository? _repo;
    private List<Course> _cache = new();
    private Course? _selectedCourse;
    private bool _hasLoaded;

    public CoursesPage()
    {
        this.InitializeComponent();
        Loaded += CoursesPage_Loaded;
    }

    private async void CoursesPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_hasLoaded) return;
        _hasLoaded = true;

        try
        {
            await _db.EnsureCreatedAsync();
            _repo = new CourseRepository(_db);
            await ReloadAsync();

            await ApplyPendingSelectionAsync();

            var pendingAction = AppState.PendingCoursesAction;
            AppState.PendingCoursesAction = null;

            if (string.Equals(pendingAction, "new", StringComparison.OrdinalIgnoreCase))
            {
                StartNewCourse();
            }
            else if (CoursesList.SelectedItem is Course c)
            {
                ShowDetails(c);
            }
            else
            {
                ShowEmptyState();
            }
        }
        catch (Exception ex)
        {
            MsgText.Text = "Erro ao carregar: " + ex.Message;
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ApplyPendingSelectionAsync();
    }

    private async System.Threading.Tasks.Task ReloadAsync(long? reselectId = null)
    {
        if (_repo is null) return;
        _cache = await _repo.ListAsync();
        FilterList(SearchBox.Text);

        if (reselectId.HasValue)
        {
            var item = _cache.FirstOrDefault(c => c.Id == reselectId.Value);
            if (item != null)
            {
                CoursesList.SelectedItem = item;
            }
        }
    }

    private void FilterList(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            CoursesList.ItemsSource = _cache;
        }
        else
        {
            CoursesList.ItemsSource = _cache.Where(c => 
                c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                (c.Platform?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterList(SearchBox.Text);
    }

    private async void ExportFavorites_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null)
        {
            return;
        }

        try
        {
            var favorites = await _repo.ListFavoritesAsync();
            if (favorites.Count == 0)
            {
                await ShowInfoDialogAsync("Favoritos", "Este perfil ainda não possui favoritos para exportar.");
                return;
            }

            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"dds-favoritos-{DateTime.Now:yyyyMMdd-HHmm}"
            };
            picker.FileTypeChoices.Add("Arquivo JSON", new List<string> { ".json" });

            var window = AppState.MainWindow;
            if (window is null)
            {
                await ShowInfoDialogAsync("Favoritos", "Janela principal não encontrada para abrir o seletor de arquivo.");
                return;
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file is null || string.IsNullOrWhiteSpace(file.Path))
            {
                return;
            }

            var payload = new FavoritesSyncPayload
            {
                Version = 1,
                ProfileKey = UserProfileService.GetCurrentProfileKey(),
                ExportedAtUtc = DateTimeOffset.UtcNow.ToString("o"),
                Favorites = favorites
                    .Select(c => new FavoriteSyncItem
                    {
                        CourseId = c.Id,
                        Name = c.Name,
                        Platform = c.Platform,
                        Url = c.Url
                    })
                    .ToList()
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(file.Path, json);
            SetFeedbackMessage($"Favoritos exportados com sucesso ({favorites.Count} curso(s)).");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao exportar favoritos por perfil.", ex);
            await ShowInfoDialogAsync("Favoritos", "Não foi possível exportar os favoritos do perfil.");
        }
    }

    private async void ImportFavorites_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null)
        {
            return;
        }

        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".json");

            var window = AppState.MainWindow;
            if (window is null)
            {
                await ShowInfoDialogAsync("Favoritos", "Janela principal não encontrada para abrir o seletor de arquivo.");
                return;
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file is null || string.IsNullOrWhiteSpace(file.Path))
            {
                return;
            }

            var json = await File.ReadAllTextAsync(file.Path);
            var payload = JsonSerializer.Deserialize<FavoritesSyncPayload>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload is null || payload.Favorites is null || payload.Favorites.Count == 0)
            {
                await ShowInfoDialogAsync("Favoritos", "Arquivo inválido ou sem favoritos para importar.");
                return;
            }

            var importMode = await ConfirmImportModeAsync();
            if (!importMode.HasValue)
            {
                return;
            }

            var replaceExisting = importMode.Value;
            var courses = await _repo.ListAsync();

            var byId = courses.ToDictionary(c => c.Id, c => c);
            var byUrl = courses
                .Where(c => !string.IsNullOrWhiteSpace(c.Url))
                .GroupBy(c => NormalizeUrlForMatch(c.Url))
                .ToDictionary(g => g.Key, g => g.First());
            var byNameAndPlatform = courses
                .GroupBy(c => BuildNamePlatformKey(c.Name, c.Platform))
                .ToDictionary(g => g.Key, g => g.First());

            var matchedIds = new HashSet<long>();
            var unmatchedCount = 0;

            foreach (var favorite in payload.Favorites)
            {
                Course? match = null;

                if (favorite.CourseId > 0 && byId.TryGetValue(favorite.CourseId, out var byIdMatch))
                {
                    match = byIdMatch;
                }

                if (match is null && !string.IsNullOrWhiteSpace(favorite.Url))
                {
                    byUrl.TryGetValue(NormalizeUrlForMatch(favorite.Url), out match);
                }

                if (match is null)
                {
                    byNameAndPlatform.TryGetValue(
                        BuildNamePlatformKey(favorite.Name, favorite.Platform),
                        out match);
                }

                if (match is null)
                {
                    unmatchedCount++;
                    continue;
                }

                matchedIds.Add(match.Id);
            }

            if (matchedIds.Count == 0)
            {
                await ShowInfoDialogAsync("Favoritos", "Nenhum favorito do arquivo corresponde aos cursos deste perfil.");
                return;
            }

            await _repo.SyncFavoritesAsync(matchedIds, replaceExisting);

            var selectedId = _selectedCourse?.Id;
            await ReloadAsync(selectedId);

            if (selectedId.HasValue)
            {
                _selectedCourse = _cache.FirstOrDefault(c => c.Id == selectedId.Value);
                if (_selectedCourse is not null)
                {
                    CoursesList.SelectedItem = _selectedCourse;
                    ShowDetails(_selectedCourse);
                }
            }

            var mode = replaceExisting ? "substituindo" : "mesclando";
            SetFeedbackMessage($"Importação concluída ({mode}): {matchedIds.Count} favorito(s) aplicado(s), {unmatchedCount} sem correspondência.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao importar favoritos por perfil.", ex);
            await ShowInfoDialogAsync("Favoritos", "Não foi possível importar o arquivo de favoritos.");
        }
    }

    private async void ClearProfileHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Limpar histórico de estudo",
            Content = "Deseja limpar o histórico de último acesso deste perfil? Isso não remove cursos nem favoritos.",
            PrimaryButtonText = "Limpar histórico",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            var affectedRows = await _repo.ClearHistoryAsync();

            var selectedId = _selectedCourse?.Id;
            await ReloadAsync(selectedId);

            if (selectedId.HasValue)
            {
                _selectedCourse = _cache.FirstOrDefault(c => c.Id == selectedId.Value);
                if (_selectedCourse is not null)
                {
                    CoursesList.SelectedItem = _selectedCourse;
                    ShowDetails(_selectedCourse);
                }
            }

            SetFeedbackMessage($"Histórico do perfil limpo com sucesso ({affectedRows} registro(s)).");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao limpar histórico por perfil.", ex);
            await ShowInfoDialogAsync("Histórico", "Não foi possível limpar o histórico deste perfil.");
        }
    }

    private void CoursesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CoursesList.SelectedItem is Course c)
        {
            _selectedCourse = c;
            ShowDetails(c);
        }
        else
        {
            _selectedCourse = null;
            if (EditorScroll.Visibility != Visibility.Visible)
            {
                ShowEmptyState();
            }
        }
    }

    private void NewCourse_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        StartNewCourse();
    }

    private void CoursesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not Course c)
            return;

        CoursesList.SelectedItem = c;
        ShowDetails(c);
    }

    private async void CoursesList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (CoursesList.SelectedItem is not Course c)
            return;

        if (string.IsNullOrWhiteSpace(c.Url))
        {
            DetailsMsgText.Text = "Esse curso não possui link para abrir. Edite o curso e adicione o link.";
            return;
        }

        await OpenCourseAsync(c);
    }

    private void DetailsEdit_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_selectedCourse is null)
        {
            ShowEmptyState();
            return;
        }

        FillForm(_selectedCourse);
        FormTitle.Text = $"Editando: {_selectedCourse.Name}";
        SaveBtn.Content = "Salvar Alterações";
        MsgText.Text = "";

        ShowEditor();
        NameBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    private async void DetailsOpen_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_selectedCourse is null)
        {
            ShowEmptyState();
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedCourse.Url))
        {
            DetailsMsgText.Text = "Adicione um link para abrir o curso no navegador.";
            return;
        }

        await OpenCourseAsync(_selectedCourse);
    }

    private async void DetailsDelete_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_repo is null || _selectedCourse is null)
            return;

        var dialog = new ContentDialog
        {
            Title = "Excluir curso",
            Content = $"Deseja excluir o curso “{_selectedCourse.Name}”?",
            PrimaryButtonText = "Excluir",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        await _repo.DeleteAsync(_selectedCourse.Id);
        _selectedCourse = null;
        CoursesList.SelectedIndex = -1;

        await ReloadAsync();
        ShowEmptyState();
    }

    private async void DetailsFavorite_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_repo is null || _selectedCourse is null)
            return;

        _selectedCourse.IsFavorite = !_selectedCourse.IsFavorite;
        await _repo.SetFavoriteAsync(_selectedCourse.Id, _selectedCourse.IsFavorite);

        var selectedId = _selectedCourse.Id;
        await ReloadAsync(reselectId: selectedId);
        _selectedCourse = _cache.FirstOrDefault(c => c.Id == selectedId);

        if (_selectedCourse is not null)
        {
            ShowDetails(_selectedCourse);
            DetailsMsgText.Text = _selectedCourse.IsFavorite
                ? "Curso marcado como favorito."
                : "Curso removido dos favoritos.";
        }
    }

    private void CancelEdit_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        MsgText.Text = "";

        if (_selectedCourse != null)
        {
            ShowDetails(_selectedCourse);
        }
        else
        {
            ShowEmptyState();
        }
    }

    private void FillForm(Course c)
    {
        FormTitle.Text = $"Editando: {c.Name}";
        NameBox.Text = c.Name;
        PlatformBox.Text = c.Platform ?? "";
        UrlBox.Text = c.Url ?? "";
        UsernameBox.Text = c.Username ?? "";
        PasswordBox.Password = "";
        NotesBox.Text = c.Notes ?? "";

        var status = c.Status?.ToLowerInvariant() ?? "fazendo";
        // Map status tags
        foreach (ComboBoxItem item in StatusBox.Items)
        {
            if (item.Tag?.ToString() == status)
            {
                StatusBox.SelectedItem = item;
                break;
            }
        }
    }

    private void ClearForm()
    {
        FormTitle.Text = "Novo Curso";
        NameBox.Text = "";
        PlatformBox.Text = "";
        UrlBox.Text = "";
        UsernameBox.Text = "";
        PasswordBox.Password = "";
        NotesBox.Text = "";
        StatusBox.SelectedIndex = 0;
        MsgText.Text = "";
    }

    private async void Save_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_repo is null) return;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MsgText.Text = "Nome do curso é obrigatório.";
            return;
        }

        var pwd = PasswordBox.Password;
        byte[]? blob = null;

        if (_selectedCourse != null)
        {
            // Edição: vazio mantém a senha existente.
            blob = string.IsNullOrWhiteSpace(pwd)
                ? _selectedCourse.PasswordBlob
                : DpapiProtector.ProtectString(pwd);
        }
        else if (!string.IsNullOrWhiteSpace(pwd))
        {
            blob = DpapiProtector.ProtectString(pwd);
        }

        var statusItem = StatusBox.SelectedItem as ComboBoxItem;
        var status = statusItem?.Tag?.ToString() ?? "fazendo";

        if (_selectedCourse == null)
        {
            // Create
            var newCourse = new Course
            {
                Name = NameBox.Text.Trim(),
                Platform = NullIfEmpty(PlatformBox.Text),
                Url = NullIfEmpty(UrlBox.Text),
                Username = NullIfEmpty(UsernameBox.Text),
                PasswordBlob = blob,
                IsFavorite = false,
                Status = status,
                Notes = NullIfEmpty(NotesBox.Text)
            };
            var newId = await _repo.CreateAsync(newCourse);

            SearchBox.Text = ""; // garante que o curso apareca na lista mesmo que exista filtro
            await ReloadAsync(reselectId: newId);

            _selectedCourse = _cache.FirstOrDefault(c => c.Id == newId);
            if (_selectedCourse != null)
            {
                ShowDetails(_selectedCourse);
                DetailsMsgText.Text = "Curso criado com sucesso!";
            }

            return;
        }
        else
        {
            // Update
            _selectedCourse.Name = NameBox.Text.Trim();
            _selectedCourse.Platform = NullIfEmpty(PlatformBox.Text);
            _selectedCourse.Url = NullIfEmpty(UrlBox.Text);
            _selectedCourse.Username = NullIfEmpty(UsernameBox.Text);
            _selectedCourse.PasswordBlob = blob;
            _selectedCourse.Status = status;
            _selectedCourse.Notes = NullIfEmpty(NotesBox.Text);
            
            await _repo.UpdateAsync(_selectedCourse);

            var updatedId = _selectedCourse.Id;
            await ReloadAsync(reselectId: updatedId);

            _selectedCourse = _cache.FirstOrDefault(c => c.Id == updatedId);
            if (_selectedCourse != null)
            {
                ShowDetails(_selectedCourse);
                DetailsMsgText.Text = "Curso atualizado com sucesso!";
            }

            return;
        }
    }

    private void ShowEmptyState()
    {
        EmptyStateCard.Visibility = Visibility.Visible;
        DetailsCard.Visibility = Visibility.Collapsed;
        EditorScroll.Visibility = Visibility.Collapsed;

        DetailsMsgText.Text = "";
        MsgText.Text = "";
    }

    private void ShowDetails(Course c)
    {
        EmptyStateCard.Visibility = Visibility.Collapsed;
        DetailsCard.Visibility = Visibility.Visible;
        EditorScroll.Visibility = Visibility.Collapsed;

        DetailsTitleText.Text = c.Name;
        DetailsPlatformText.Text = string.IsNullOrWhiteSpace(c.Platform) ? "Curso Online" : c.Platform;
        DetailsStatusText.Text = StatusToDisplay(c.Status);
        DetailsUsernameText.Text = string.IsNullOrWhiteSpace(c.Username) ? "-" : c.Username;
        DetailsUrlText.Text = string.IsNullOrWhiteSpace(c.Url) ? "-" : c.Url;
        DetailsNotesText.Text = string.IsNullOrWhiteSpace(c.Notes) ? "-" : c.Notes;

        DetailsOpenBtn.IsEnabled = !string.IsNullOrWhiteSpace(c.Url);
        DetailsFavoriteBtn.Content = c.IsFavorite ? "Desfavoritar" : "Favoritar";
        DetailsMsgText.Text = "";
    }

    private void ShowEditor()
    {
        EmptyStateCard.Visibility = Visibility.Collapsed;
        DetailsCard.Visibility = Visibility.Collapsed;
        EditorScroll.Visibility = Visibility.Visible;

        DetailsMsgText.Text = "";
    }

    private void StartNewCourse()
    {
        CoursesList.SelectedIndex = -1;
        _selectedCourse = null;

        ClearForm();
        FormTitle.Text = "Novo Curso";
        SaveBtn.Content = "Salvar Curso";
        MsgText.Text = "";

        ShowEditor();
        NameBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    private async System.Threading.Tasks.Task OpenCourseAsync(Course c)
    {
        if (_repo is null)
            return;

        if (string.IsNullOrWhiteSpace(c.Url))
            return;

        try
        {
            await _repo.UpdateLastAccessedAsync(c.Id);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Falha ao atualizar 'ultimo acesso' do curso. Motivo: {ex.Message}");
        }

        AppState.PendingBrowserUrl = c.Url;
        AppState.CurrentCourseId = c.Id;
        AppState.PendingCourseSelectionId = c.Id;
        AppState.BrowserReturnTag = "courses";
        NavigateToTag("browser");
    }

    private async System.Threading.Tasks.Task ApplyPendingSelectionAsync()
    {
        if (!AppState.PendingCourseSelectionId.HasValue)
        {
            return;
        }

        var pendingId = AppState.PendingCourseSelectionId.Value;
        AppState.PendingCourseSelectionId = null;

        if (!_hasLoaded || _repo is null)
        {
            AppState.PendingCourseSelectionId = pendingId;
            return;
        }

        if (_cache.Count == 0)
        {
            await ReloadAsync();
        }

        var selected = _cache.FirstOrDefault(c => c.Id == pendingId);
        if (selected is null)
        {
            await ReloadAsync(reselectId: pendingId);
            selected = _cache.FirstOrDefault(c => c.Id == pendingId);
        }

        if (selected is null)
        {
            return;
        }

        _selectedCourse = selected;
        CoursesList.SelectedItem = selected;
        CoursesList.ScrollIntoView(selected);
        ShowDetails(selected);
    }

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
            "settings" => typeof(SettingsPage),
            "dev" => typeof(DevelopmentPage),
            _ => typeof(DashboardPage)
        };

        if (Frame?.CurrentSourcePageType != pageType)
        {
            Frame?.Navigate(pageType);
        }
    }

    private static string StatusToDisplay(string? status)
    {
        return (status ?? "fazendo").Trim().ToLowerInvariant() switch
        {
            "concluido" => "Concluído",
            "pausado" => "Pausado",
            _ => "Em andamento"
        };
    }

    private static string? NullIfEmpty(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return s.Trim();
    }

    private async System.Threading.Tasks.Task<bool?> ConfirmImportModeAsync()
    {
        var replaceToggle = new CheckBox
        {
            Content = "Substituir favoritos atuais deste perfil",
            IsChecked = false
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "Importar favoritos deste arquivo para o perfil atual."
        });
        panel.Children.Add(replaceToggle);

        var dialog = new ContentDialog
        {
            Title = "Importar favoritos",
            Content = panel,
            PrimaryButtonText = "Importar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return replaceToggle.IsChecked == true;
    }

    private async System.Threading.Tasks.Task ShowInfoDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void SetFeedbackMessage(string message)
    {
        if (EditorScroll.Visibility == Visibility.Visible)
        {
            MsgText.Text = message;
            return;
        }

        DetailsMsgText.Text = message;
    }

    private static string NormalizeUrlForMatch(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var raw = url.Trim();
        if (Uri.TryCreate(raw, UriKind.Absolute, out var parsed))
        {
            var host = parsed.Host.Trim().ToLowerInvariant();
            var path = (parsed.AbsolutePath ?? string.Empty).TrimEnd('/').ToLowerInvariant();
            return $"{host}{path}";
        }

        return raw.TrimEnd('/').ToLowerInvariant();
    }

    private static string BuildNamePlatformKey(string? name, string? platform)
        => $"{NormalizeTextForKey(name)}|{NormalizeTextForKey(platform)}";

    private static string NormalizeTextForKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToLowerInvariant();
    }

    private sealed class FavoritesSyncPayload
    {
        public int Version { get; set; } = 1;
        public string ProfileKey { get; set; } = string.Empty;
        public string ExportedAtUtc { get; set; } = DateTimeOffset.UtcNow.ToString("o");
        public List<FavoriteSyncItem> Favorites { get; set; } = new();
    }

    private sealed class FavoriteSyncItem
    {
        public long CourseId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Platform { get; set; }
        public string? Url { get; set; }
    }
}
