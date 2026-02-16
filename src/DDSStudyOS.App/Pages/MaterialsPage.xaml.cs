using DDSStudyOS.App.Models;
using DDSStudyOS.App.Services;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.Storage.Pickers;

namespace DDSStudyOS.App.Pages;

public sealed partial class MaterialsPage : Page
{
    private readonly DatabaseService _db = new();
    private CourseRepository? _courses;
    private MaterialRepository? _materials;

    private List<Course> _courseCache = new();
    private List<MaterialItem> _materialCache = new();

    public MaterialsPage()
    {
        this.InitializeComponent();
        Loaded += MaterialsPage_Loaded;
    }

    private async void MaterialsPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            await _db.EnsureCreatedAsync();
            _courses = new CourseRepository(_db);
            _materials = new MaterialRepository(_db);

            await LoadCoursesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MsgText.Text = "Erro: " + ex.Message;
        }
    }

    private async System.Threading.Tasks.Task LoadCoursesAsync()
    {
        if (_courses is null) return;

        _courseCache = await _courses.ListAsync();

        CourseCombo.Items.Clear();
        CourseCombo.Items.Add(new ComboBoxItem { Content = "(Sem vínculo)", Tag = null });

        foreach (var c in _courseCache.OrderBy(c => c.Name))
            CourseCombo.Items.Add(new ComboBoxItem { Content = $"#{c.Id} — {c.Name}", Tag = c.Id });

        CourseCombo.SelectedIndex = 0;
    }

    private long? SelectedCourseId()
    {
        var item = CourseCombo.SelectedItem as ComboBoxItem;
        return item?.Tag as long?;
    }

    private async System.Threading.Tasks.Task ReloadAsync(long? courseId = null)
    {
        if (_materials is null) return;
        _materialCache = await _materials.ListAsync(courseId);

        MaterialsList.ItemsSource = _materialCache.Select(m => $"#{m.Id} — {m.FileName} ({m.FileType}) [{m.StorageMode}]").ToList();
        MsgText.Text = $"Carregado: {_materialCache.Count} material(is).";
    }

    private MaterialItem? GetSelectedMaterial()
    {
        var idx = MaterialsList.SelectedIndex;
        if (idx < 0 || idx >= _materialCache.Count) return null;
        return _materialCache[idx];
    }

    private void FillForm(MaterialItem m)
    {
        FileNameBox.Text = m.FileName;
        FilePathBox.Text = m.FilePath;
        FileTypeBox.Text = m.FileType ?? "";
        ManagedCopyCheck.IsChecked = string.Equals(m.StorageMode, MaterialStorageService.ModeManagedCopy, StringComparison.OrdinalIgnoreCase);

        // Seleciona o curso no combo
        if (m.CourseId is null)
        {
            CourseCombo.SelectedIndex = 0;
        }
        else
        {
            for (int i = 0; i < CourseCombo.Items.Count; i++)
            {
                if (CourseCombo.Items[i] is ComboBoxItem it && (it.Tag as long?) == m.CourseId)
                {
                    CourseCombo.SelectedIndex = i;
                    break;
                }
            }
        }
    }

    private void ClearForm()
    {
        FileNameBox.Text = "";
        FilePathBox.Text = "";
        FileTypeBox.Text = "";
        ManagedCopyCheck.IsChecked = false;
        CourseCombo.SelectedIndex = 0;
        MaterialsList.SelectedIndex = -1;
    }

    private async void Browse_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");

            var window = AppState.MainWindow;
            if (window is null)
            {
                MsgText.Text = "Janela principal não encontrada para abrir o File Picker.";
                return;
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            FilePathBox.Text = file.Path;
            if (string.IsNullOrWhiteSpace(FileNameBox.Text))
                FileNameBox.Text = file.Name;
        }
        catch (Exception ex)
        {
            MsgText.Text = "Erro no File Picker: " + ex.Message;
        }
    }

    private async void Create_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_materials is null) return;
        if (string.IsNullOrWhiteSpace(FileNameBox.Text) || string.IsNullOrWhiteSpace(FilePathBox.Text))
        {
            MsgText.Text = "Nome e caminho do arquivo são obrigatórios.";
            return;
        }

        if (!TryResolveStorage(
            sourcePath: FilePathBox.Text.Trim(),
            displayName: FileNameBox.Text.Trim(),
            managedCopyRequested: ManagedCopyCheck.IsChecked == true,
            currentItem: null,
            out var storedPath,
            out var storageMode,
            out var error))
        {
            MsgText.Text = error;
            return;
        }

        var item = new MaterialItem
        {
            CourseId = SelectedCourseId(),
            FileName = FileNameBox.Text.Trim(),
            FilePath = storedPath,
            FileType = NullIfEmpty(FileTypeBox.Text),
            StorageMode = storageMode
        };

        var id = await _materials.CreateAsync(item);
        MsgText.Text = $"Criado material #{id}.";
        ClearForm();
        await ReloadAsync();
    }

    private async void Update_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_materials is null) return;
        var selected = GetSelectedMaterial();
        if (selected is null)
        {
            MsgText.Text = "Selecione um material para atualizar.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FileNameBox.Text) || string.IsNullOrWhiteSpace(FilePathBox.Text))
        {
            MsgText.Text = "Nome e caminho do arquivo são obrigatórios.";
            return;
        }

        if (!TryResolveStorage(
            sourcePath: FilePathBox.Text.Trim(),
            displayName: FileNameBox.Text.Trim(),
            managedCopyRequested: ManagedCopyCheck.IsChecked == true,
            currentItem: selected,
            out var storedPath,
            out var storageMode,
            out var error))
        {
            MsgText.Text = error;
            return;
        }

        selected.CourseId = SelectedCourseId();
        selected.FileName = FileNameBox.Text.Trim();
        selected.FilePath = storedPath;
        selected.FileType = NullIfEmpty(FileTypeBox.Text);
        selected.StorageMode = storageMode;

        await _materials.UpdateAsync(selected);
        MsgText.Text = $"Atualizado material #{selected.Id}.";
        await ReloadAsync();
    }

    private async void Delete_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_materials is null) return;
        var selected = GetSelectedMaterial();
        if (selected is null)
        {
            MsgText.Text = "Selecione um material para excluir.";
            return;
        }

        await _materials.DeleteAsync(selected.Id);
        MsgText.Text = $"Excluído material #{selected.Id}.";
        ClearForm();
        await ReloadAsync();
    }

    private void Open_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var selected = GetSelectedMaterial();
        if (selected is null)
        {
            MsgText.Text = "Selecione um material para abrir.";
            return;
        }

        try
        {
            if (MaterialStorageService.IsWebUrl(selected.FilePath))
            {
                var uriInfo = new ProcessStartInfo(selected.FilePath)
                {
                    UseShellExecute = true
                };
                Process.Start(uriInfo);
                MsgText.Text = "Abrindo link no navegador padrão...";
                return;
            }

            if (!File.Exists(selected.FilePath))
            {
                MsgText.Text = "Arquivo não encontrado no caminho salvo. Atualize o material ou use cópia gerenciada.";
                return;
            }

            var psi = new ProcessStartInfo(selected.FilePath)
            {
                UseShellExecute = true
            };
            Process.Start(psi);
            MsgText.Text = "Abrindo arquivo...";
        }
        catch (Exception ex)
        {
            MsgText.Text = "Erro ao abrir arquivo: " + ex.Message;
        }
    }

    private async void Reload_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await ReloadAsync();

    private async void FilterByCourse_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await ReloadAsync(SelectedCourseId());

    private async void ShowAll_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await ReloadAsync(null);

    private void Clear_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => ClearForm();

    private void MaterialsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = GetSelectedMaterial();
        if (selected is null) return;
        FillForm(selected);
        MsgText.Text = $"Selecionado material #{selected.Id}.";
    }

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool TryResolveStorage(
        string sourcePath,
        string displayName,
        bool managedCopyRequested,
        MaterialItem? currentItem,
        out string storedPath,
        out string storageMode,
        out string error)
    {
        storedPath = sourcePath;
        storageMode = MaterialStorageService.ModeReference;
        error = string.Empty;

        if (MaterialStorageService.IsWebUrl(sourcePath))
        {
            storedPath = sourcePath;
            storageMode = MaterialStorageService.ModeWebLink;
            return true;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            error = "Informe o caminho local do arquivo ou uma URL.";
            return false;
        }

        if (!File.Exists(sourcePath))
        {
            error = "Arquivo local não encontrado. Verifique o caminho informado.";
            return false;
        }

        if (managedCopyRequested)
        {
            var isSameManagedFile =
                currentItem != null
                && string.Equals(currentItem.FilePath, sourcePath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(currentItem.StorageMode, MaterialStorageService.ModeManagedCopy, StringComparison.OrdinalIgnoreCase)
                && File.Exists(sourcePath);

            if (isSameManagedFile || MaterialStorageService.IsInsideManagedStorage(sourcePath))
            {
                storedPath = sourcePath;
                storageMode = MaterialStorageService.ModeManagedCopy;
                return true;
            }

            try
            {
                storedPath = MaterialStorageService.EnsureManagedCopy(sourcePath, displayName);
                storageMode = MaterialStorageService.ModeManagedCopy;
                return true;
            }
            catch (Exception ex)
            {
                error = "Falha ao copiar para a biblioteca interna: " + ex.Message;
                return false;
            }
        }

        storedPath = sourcePath;
        storageMode = MaterialStorageService.ModeReference;
        return true;
    }
}
