using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

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

    private async void CoursesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not Course c)
            return;

        CoursesList.SelectedItem = c;
        ShowDetails(c);

        if (string.IsNullOrWhiteSpace(c.Url))
        {
            DetailsMsgText.Text = "Esse curso ainda não tem link. Clique em “Editar” para adicionar.";
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
        AppState.RequestNavigateTag?.Invoke("browser");
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
}
