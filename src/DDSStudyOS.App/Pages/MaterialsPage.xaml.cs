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
    private const string FilterAllCoursesToken = "__all_courses__";
    private const string FilterNoCourseToken = "__no_course__";
    private const string FilterAllTypesToken = "__all_types__";
    private const string FilterOtherTypesToken = "__other_types__";

    private readonly DatabaseService _db = new();
    private CourseRepository? _courses;
    private MaterialRepository? _materials;
    private static readonly HashSet<string> TemporaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tmp",
        ".crdownload",
        ".part",
        ".partial",
        ".download"
    };

    private List<Course> _courseCache = new();
    private List<MaterialItem> _allMaterialCache = new();
    private List<MaterialItem> _visibleMaterialCache = new();
    private int _ignoredTemporaryCount;

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

            var removedTemporary = await _materials.DeleteTemporaryEntriesAsync();
            await LoadCoursesAsync();
            await ReloadAsync();

            if (removedTemporary > 0)
            {
                MsgText.Text = $"Removidos {removedTemporary} registro(s) temporário(s).";
            }
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
        RefreshCourseFilterCombo();
    }

    private long? SelectedCourseId()
    {
        var item = CourseCombo.SelectedItem as ComboBoxItem;
        return item?.Tag as long?;
    }

    private void RefreshCourseFilterCombo()
    {
        var previousToken = SelectedFilterCourseToken();

        FilterCourseCombo.Items.Clear();
        FilterCourseCombo.Items.Add(new ComboBoxItem { Content = "Todos os cursos", Tag = FilterAllCoursesToken });
        FilterCourseCombo.Items.Add(new ComboBoxItem { Content = "Sem vínculo", Tag = FilterNoCourseToken });

        foreach (var c in _courseCache.OrderBy(c => c.Name))
        {
            FilterCourseCombo.Items.Add(new ComboBoxItem
            {
                Content = $"#{c.Id} — {c.Name}",
                Tag = $"course:{c.Id}"
            });
        }

        SelectFilterComboItem(FilterCourseCombo, previousToken, FilterAllCoursesToken);
    }

    private void RefreshTypeFilterCombo()
    {
        var previousToken = SelectedFilterTypeToken();

        FilterTypeCombo.Items.Clear();
        FilterTypeCombo.Items.Add(new ComboBoxItem { Content = "Todos os tipos", Tag = FilterAllTypesToken });
        FilterTypeCombo.Items.Add(new ComboBoxItem { Content = "Outros (sem tipo)", Tag = FilterOtherTypesToken });

        var uniqueTypes = _allMaterialCache
            .Select(m => NormalizeMaterialType(m.FileType))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase);

        foreach (var type in uniqueTypes)
        {
            FilterTypeCombo.Items.Add(new ComboBoxItem
            {
                Content = type,
                Tag = $"type:{type}"
            });
        }

        SelectFilterComboItem(FilterTypeCombo, previousToken, FilterAllTypesToken);
    }

    private void ResetFilterControls()
    {
        SelectFilterComboItem(FilterCourseCombo, FilterAllCoursesToken, FilterAllCoursesToken);
        SelectFilterComboItem(FilterTypeCombo, FilterAllTypesToken, FilterAllTypesToken);
        FilterDateFromPicker.Date = null;
        FilterDateToPicker.Date = null;
    }

    private void ApplyActiveFilters()
    {
        IEnumerable<MaterialItem> filtered = _allMaterialCache;
        var selectedCourseToken = SelectedFilterCourseToken();

        if (string.Equals(selectedCourseToken, FilterNoCourseToken, StringComparison.Ordinal))
        {
            filtered = filtered.Where(m => m.CourseId is null);
        }
        else if (TryParseCourseToken(selectedCourseToken, out var selectedCourseId))
        {
            filtered = filtered.Where(m => m.CourseId == selectedCourseId);
        }

        var selectedTypeToken = SelectedFilterTypeToken();
        if (string.Equals(selectedTypeToken, FilterOtherTypesToken, StringComparison.Ordinal))
        {
            filtered = filtered.Where(m => string.IsNullOrWhiteSpace(m.FileType));
        }
        else if (TryParseTypeToken(selectedTypeToken, out var selectedType))
        {
            filtered = filtered.Where(m => string.Equals(NormalizeMaterialType(m.FileType), selectedType, StringComparison.OrdinalIgnoreCase));
        }

        var fromDate = FilterDateFromPicker.Date?.Date;
        var toDate = FilterDateToPicker.Date?.Date;
        if (fromDate.HasValue && toDate.HasValue && toDate.Value < fromDate.Value)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        if (fromDate.HasValue)
        {
            filtered = filtered.Where(m => m.CreatedAt.LocalDateTime.Date >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            filtered = filtered.Where(m => m.CreatedAt.LocalDateTime.Date <= toDate.Value);
        }

        _visibleMaterialCache = filtered.ToList();
        MaterialsList.ItemsSource = _visibleMaterialCache.Select(BuildMaterialListLabel).ToList();
        MaterialsList.SelectedIndex = -1;

        MsgText.Text = BuildLoadedStatusText(_visibleMaterialCache.Count, _allMaterialCache.Count, _ignoredTemporaryCount);
    }

    private string SelectedFilterCourseToken()
        => (FilterCourseCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? FilterAllCoursesToken;

    private string SelectedFilterTypeToken()
        => (FilterTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? FilterAllTypesToken;

    private static void SelectFilterComboItem(ComboBox comboBox, string token, string defaultToken)
    {
        var targetToken = string.IsNullOrWhiteSpace(token) ? defaultToken : token;
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag as string, targetToken, StringComparison.Ordinal))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag as string, defaultToken, StringComparison.Ordinal))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
    }

    private static bool TryParseCourseToken(string token, out long courseId)
    {
        courseId = 0;
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("course:", StringComparison.Ordinal))
        {
            return false;
        }

        return long.TryParse(token["course:".Length..], out courseId);
    }

    private static bool TryParseTypeToken(string token, out string materialType)
    {
        materialType = string.Empty;
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("type:", StringComparison.Ordinal))
        {
            return false;
        }

        materialType = token["type:".Length..];
        return !string.IsNullOrWhiteSpace(materialType);
    }

    private static string NormalizeMaterialType(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string BuildLoadedStatusText(int visibleCount, int totalCount, int temporaryIgnoredCount)
    {
        var filterInfo = visibleCount == totalCount
            ? $"Carregado: {visibleCount} material(is)."
            : $"Carregado: {visibleCount} de {totalCount} material(is) após filtros.";

        if (temporaryIgnoredCount <= 0)
        {
            return filterInfo;
        }

        return $"{filterInfo} Ignorados {temporaryIgnoredCount} temporário(s).";
    }

    private async System.Threading.Tasks.Task ReloadAsync()
    {
        if (_materials is null) return;
        var rawItems = await _materials.ListAsync();
        var temporaryItems = rawItems.Where(IsTemporaryMaterial).ToList();
        _ignoredTemporaryCount = temporaryItems.Count;
        _allMaterialCache = rawItems.Where(m => !IsTemporaryMaterial(m)).ToList();

        RefreshTypeFilterCombo();
        ApplyActiveFilters();
    }

    private MaterialItem? GetSelectedMaterial()
    {
        var idx = MaterialsList.SelectedIndex;
        if (idx < 0 || idx >= _visibleMaterialCache.Count) return null;
        return _visibleMaterialCache[idx];
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
        FilePathBox.Text = storedPath;
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
        FilePathBox.Text = storedPath;
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

    private void ApplyFilters_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => ApplyActiveFilters();

    private void ClearFilters_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ResetFilterControls();
        ApplyActiveFilters();
    }

    private void FilterByCourse_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var courseId = SelectedCourseId();
        var token = courseId is null ? FilterNoCourseToken : $"course:{courseId.Value}";
        SelectFilterComboItem(FilterCourseCombo, token, FilterAllCoursesToken);
        ApplyActiveFilters();
    }

    private void ShowAll_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ResetFilterControls();
        ApplyActiveFilters();
    }

    private void Clear_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => ClearForm();

    private void OpenFolder_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var selected = GetSelectedMaterial();
        if (selected is null)
        {
            MsgText.Text = "Selecione um material para abrir a pasta.";
            return;
        }

        try
        {
            var normalizedPath = MaterialStorageService.NormalizePathOrUrl(selected.FilePath);
            if (!MaterialStorageService.IsWebUrl(normalizedPath) && File.Exists(normalizedPath))
            {
                OpenInExplorerSelectFile(normalizedPath);
                MsgText.Text = "Abrindo pasta do material...";
                return;
            }

            var folder = ResolveFolderToOpen(selected, normalizedPath);
            if (string.IsNullOrWhiteSpace(folder))
            {
                MsgText.Text = "Não foi possível resolver uma pasta para este material.";
                return;
            }

            OpenInExplorerFolder(folder);
            MsgText.Text = MaterialStorageService.IsWebUrl(normalizedPath)
                ? "Material é URL. Abrindo pasta de fallback."
                : "Arquivo ausente. Abrindo pasta de fallback.";
        }
        catch (Exception ex)
        {
            MsgText.Text = "Erro ao abrir pasta do material: " + ex.Message;
        }
    }

    private void MaterialsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = GetSelectedMaterial();
        if (selected is null) return;
        FillForm(selected);
        MsgText.Text = $"Selecionado material #{selected.Id}.";
    }

    private static string ResolveFolderToOpen(MaterialItem item, string normalizedPath)
    {
        var parentFolder = Path.GetDirectoryName(normalizedPath);
        if (!string.IsNullOrWhiteSpace(parentFolder) && Directory.Exists(parentFolder))
        {
            return parentFolder;
        }

        if (string.Equals(item.StorageMode, MaterialStorageService.ModeManagedCopy, StringComparison.OrdinalIgnoreCase)
            || MaterialStorageService.IsInsideManagedStorage(normalizedPath))
        {
            var managedFolder = MaterialStorageService.GetManagedMaterialsFolder();
            Directory.CreateDirectory(managedFolder);
            return managedFolder;
        }

        var downloadsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        if (Directory.Exists(downloadsFolder))
        {
            return downloadsFolder;
        }

        var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documentsFolder))
        {
            return documentsFolder;
        }

        var fallbackRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DDSStudyOS");
        Directory.CreateDirectory(fallbackRoot);
        return fallbackRoot;
    }

    private static void OpenInExplorerSelectFile(string filePath)
    {
        var args = $"/select,\"{filePath}\"";
        var startInfo = new ProcessStartInfo("explorer.exe", args)
        {
            UseShellExecute = true
        };
        Process.Start(startInfo);
    }

    private static void OpenInExplorerFolder(string folderPath)
    {
        var args = $"\"{folderPath}\"";
        var startInfo = new ProcessStartInfo("explorer.exe", args)
        {
            UseShellExecute = true
        };
        Process.Start(startInfo);
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
        sourcePath = MaterialStorageService.NormalizePathOrUrl(sourcePath);
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

    private static string BuildMaterialListLabel(MaterialItem item)
    {
        var type = string.IsNullOrWhiteSpace(item.FileType) ? "Outros" : item.FileType;
        var mode = string.IsNullOrWhiteSpace(item.StorageMode) ? MaterialStorageService.ModeReference : item.StorageMode;
        var name = string.IsNullOrWhiteSpace(item.FileName) ? "(sem nome)" : item.FileName;
        var createdAt = item.CreatedAt.LocalDateTime.ToString("dd/MM/yyyy");

        string availability;
        if (MaterialStorageService.IsWebUrl(item.FilePath))
        {
            availability = " [url]";
        }
        else
        {
            availability = File.Exists(item.FilePath) ? string.Empty : " [arquivo ausente]";
        }

        return $"#{item.Id} — {name} ({type}) [{mode}] {createdAt}{availability}";
    }

    private static bool IsTemporaryMaterial(MaterialItem item)
    {
        static bool HasTemporaryExtension(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var ext = Path.GetExtension(value.Trim());
            return !string.IsNullOrWhiteSpace(ext) && TemporaryExtensions.Contains(ext);
        }

        return HasTemporaryExtension(item.FileName) || HasTemporaryExtension(item.FilePath);
    }
}
