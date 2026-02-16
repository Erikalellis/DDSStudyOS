using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
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

    public CoursesPage()
    {
        this.InitializeComponent();
        Loaded += CoursesPage_Loaded;
    }

    private async void CoursesPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            await _db.EnsureCreatedAsync();
            _repo = new CourseRepository(_db);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MsgText.Text = "Erro ao carregar: " + ex.Message;
        }
    }

    private async System.Threading.Tasks.Task ReloadAsync()
    {
        if (_repo is null) return;
        _cache = await _repo.ListAsync();
        FilterList(SearchBox.Text);
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
            FillForm(c);
            
            // Enable Actions
            DeleteBtn.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            OpenBtn.IsEnabled = !string.IsNullOrEmpty(c.Url);
            SaveBtn.Content = "Atualizar Curso";
        }
        else
        {
            _selectedCourse = null;
            // ClearForm(); // Don't clear immediately to allow new creation context
        }
    }

    private void NewCourse_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ClearForm();
        CoursesList.SelectedIndex = -1;
        _selectedCourse = null;
        SaveBtn.Content = "Salvar Novo Curso";
        DeleteBtn.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        OpenBtn.IsEnabled = false;
        NameBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
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
            await _repo.CreateAsync(newCourse);
            MsgText.Text = "Curso criado com sucesso!";
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
            MsgText.Text = "Curso atualizado com sucesso!";
        }

        await ReloadAsync();
        
        // Re-select if update
        if (_selectedCourse != null)
        {
            // Find updated item in list
            // Simple approach: clear selection to force refresh
            CoursesList.SelectedIndex = -1;
        }
        else
        {
            ClearForm();
        }
    }

    private async void Delete_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_repo is null || _selectedCourse is null) return;
        
        // Confirm dialog would be nice here, but MVP: just delete
        await _repo.DeleteAsync(_selectedCourse.Id);
        MsgText.Text = "Curso excluído.";
        ClearForm();
        await ReloadAsync();
    }

    private void OpenCourse_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var url = UrlBox.Text;
        if (string.IsNullOrWhiteSpace(url))
        {
            MsgText.Text = "Adicione um link para abrir.";
            return;
        }

        AppState.PendingBrowserUrl = url;
        if (_selectedCourse != null) AppState.CurrentCourseId = _selectedCourse.Id;
        
        AppState.RequestNavigateTag?.Invoke("browser");
    }

    private static string? NullIfEmpty(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return s.Trim();
    }
}
