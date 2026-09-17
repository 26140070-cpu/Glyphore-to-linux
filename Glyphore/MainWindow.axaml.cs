using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Glyphore;

public sealed partial class MainWindow : Window
{
    private readonly AppData _data = AppData.Load();
    private GlyphoreScene _scene = GlyphoreScene.CreateDefault();
    private bool _loading;
    private string? _scenePath;

    private SceneEffectLayer Active => _scene.EnsureActiveLayer(new EffectSettings());

    public MainWindow()
    {
        InitializeComponent();
        EffectBox.ItemsSource = _data.Presets.Keys.Order();
        CharsetBox.ItemsSource = _data.Charsets.Keys.Order();
        PaletteBox.ItemsSource = _data.Palettes.Keys.Order();
        BlendBox.ItemsSource = Enum.GetValues<LayerBlendMode>();
        TitleFontBox.ItemsSource = AsciiTitleGenerator.Fonts;
        LoadSceneIntoUi();
    }

    private void LoadSceneIntoUi()
    {
        _loading = true;
        var layer = Active;
        SceneNameBox.Text = _scene.Name;
        EffectBox.SelectedItem = layer.Effect;
        UpdatePresetItems();
        PresetBox.SelectedItem = layer.Preset;
        TitleTextBox.Text = layer.TitleText;
        TitleFontBox.SelectedItem = layer.TitlePrefab;
        CharsetBox.SelectedItem = _scene.CharsetName;
        PaletteBox.SelectedItem = _scene.PaletteName;
        WidthBox.Value = _scene.Width;
        HeightBox.Value = _scene.Height;
        ColorBox.IsChecked = _scene.ColorEnabled;
        LayersBox.ItemsSource = _scene.Layers;
        LayersBox.SelectedItem = layer;
        OpacityBox.Value = (decimal)layer.Opacity;
        SeedBox.Value = layer.Seed;
        BlendBox.SelectedItem = layer.BlendMode;
        VisibleBox.IsChecked = layer.Visible;
        MasksBox.ItemsSource = layer.Masks;
        RotationBox.Value = (decimal)_scene.Transform.Rotation;
        ScaleBox.Value = (decimal)_scene.Transform.Scale;
        PostEnabledBox.IsChecked = _scene.PostProcess.Enabled;
        ExposureBox.Value = (decimal)_scene.PostProcess.Exposure;
        ContrastBox.Value = (decimal)_scene.PostProcess.Contrast;
        VignetteBox.Value = (decimal)_scene.PostProcess.Vignette;
        ScanlinesBox.Value = (decimal)_scene.PostProcess.Scanlines;
        _loading = false;
        RefreshPreview();
    }

    private void SceneChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _scene.Name = string.IsNullOrWhiteSpace(SceneNameBox.Text) ? "Untitled" : SceneNameBox.Text.Trim();
        _scene.Width = Math.Max(8, (int)(WidthBox.Value ?? 80));
        _scene.Height = Math.Max(4, (int)(HeightBox.Value ?? 25));
        _scene.ColorEnabled = ColorBox.IsChecked == true;
        _scene.CharsetName = CharsetBox.SelectedItem as string ?? "Classic";
        _scene.Charset = _data.Charsets.GetValueOrDefault(_scene.CharsetName, " .:-=+*#%@");
        _scene.PaletteName = PaletteBox.SelectedItem as string ?? "Plasma";
        _scene.PaletteStops = _data.Palettes.GetValueOrDefault(_scene.PaletteName, ["#101020", "#ffffff"]);
        Active.Effect = EffectBox.SelectedItem as string ?? "Plasma";
        UpdatePresetItems();
        RefreshPreview();
    }

    private void UpdatePresetItems()
    {
        var effect = EffectBox.SelectedItem as string ?? Active.Effect;
        PresetBox.ItemsSource = _data.Presets.GetValueOrDefault(effect)?.Keys.Order() ?? Enumerable.Empty<string>();
    }

    private void PresetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || PresetBox.SelectedItem is not string preset) return;
        var layer = Active;
        layer.Preset = preset;
        layer.Values = _data.Presets.GetValueOrDefault(layer.Effect)?.GetValueOrDefault(preset)?.Where(pair => pair.Value.ValueKind == System.Text.Json.JsonValueKind.Number).ToDictionary(pair => pair.Key, pair => pair.Value.GetDouble(), StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        RefreshPreview();
    }

    private void TitleChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Active.TitleText = TitleTextBox.Text ?? "";
        Active.TitlePrefab = TitleFontBox.SelectedItem as string ?? "Standard";
        RefreshPreview();
    }

    private void LayerSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || LayersBox.SelectedItem is not SceneEffectLayer layer) return;
        _scene.ActiveLayerId = layer.Id;
        LoadSceneIntoUi();
    }

    private void LayerChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var layer = Active;
        layer.Opacity = (double)Math.Clamp(OpacityBox.Value ?? 1, 0, 1);
        layer.Seed = Math.Max(0, (int)(SeedBox.Value ?? 1337));
        layer.BlendMode = BlendBox.SelectedItem is LayerBlendMode blend ? blend : LayerBlendMode.Normal;
        layer.Visible = VisibleBox.IsChecked == true;
        RefreshPreview();
    }

    private void TransformChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _scene.Transform.Rotation = (double)(RotationBox.Value ?? 0);
        _scene.Transform.Scale = (double)(ScaleBox.Value ?? 1);
        RefreshPreview();
    }

    private void PostChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _scene.PostProcess.Enabled = PostEnabledBox.IsChecked == true;
        _scene.PostProcess.Exposure = (double)(ExposureBox.Value ?? 0);
        _scene.PostProcess.Contrast = (double)(ContrastBox.Value ?? 1);
        _scene.PostProcess.Vignette = (double)(VignetteBox.Value ?? 0);
        _scene.PostProcess.Scanlines = (double)(ScanlinesBox.Value ?? 0);
        RefreshPreview();
    }

    private void AddMask(object? sender, RoutedEventArgs e)
    {
        Active.Masks.Add(new SceneLayerMask { Name = $"Mask {Active.Masks.Count + 1}" });
        LoadSceneIntoUi();
    }

    private void RemoveMask(object? sender, RoutedEventArgs e)
    {
        if (MasksBox.SelectedItem is not SceneLayerMask mask) return;
        Active.Masks.Remove(mask);
        LoadSceneIntoUi();
    }

    private void AddLayer(object? sender, RoutedEventArgs e)
    {
        var layer = new SceneEffectLayer { Name = $"Layer {_scene.Layers.Count + 1}", Effect = Active.Effect };
        _scene.Layers.Add(layer);
        _scene.ActiveLayerId = layer.Id;
        LoadSceneIntoUi();
    }

    private void RemoveLayer(object? sender, RoutedEventArgs e)
    {
        if (_scene.Layers.Count == 1) return;
        _scene.Layers.Remove(Active);
        _scene.ActiveLayerId = _scene.Layers[0].Id;
        LoadSceneIntoUi();
    }

    private void NewScene(object? sender, RoutedEventArgs e)
    {
        _scene = GlyphoreScene.CreateDefault();
        _scenePath = null;
        LoadSceneIntoUi();
    }

    private async void OpenScene(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Abrir escena", AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("Glyphoré scene") { Patterns = ["*.glyphore"] }] });
        if (files.Count == 0) return;
        try
        {
            _scenePath = files[0].TryGetLocalPath();
            if (_scenePath is null) throw new InvalidOperationException("El proveedor no expone una ruta local.");
            _scene = SceneFile.Load(_scenePath);
            LoadSceneIntoUi();
            StatusText.Text = "Escena cargada";
        }
        catch (Exception ex) { await ShowError(ex.Message); }
    }

    private async void SaveScene(object? sender, RoutedEventArgs e)
    {
        if (_scenePath is null)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Guardar escena", SuggestedFileName = _scene.Name + ".glyphore", DefaultExtension = "glyphore", FileTypeChoices = [new FilePickerFileType("Glyphoré scene") { Patterns = ["*.glyphore"] }] });
            _scenePath = file?.TryGetLocalPath();
        }
        if (_scenePath is null) return;
        try { SceneFile.Save(_scenePath, _scene); StatusText.Text = "Escena guardada"; } catch (Exception ex) { await ShowError(ex.Message); }
    }

    private async void ExportText(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Exportar", SuggestedFileName = _scene.Name + ".txt", DefaultExtension = "txt", FileTypeChoices = [new FilePickerFileType("Texto, HTML, ANSI, JSON, C# o PowerShell") { Patterns = ["*.txt", "*.html", "*.ans", "*.json", "*.cs", "*.ps1"] }] });
        var path = file?.TryGetLocalPath();
        if (path is null) return;
        try { ExportService.Save(path, _scene, Preview.RenderText()); StatusText.Text = "Exportación terminada"; } catch (Exception ex) { await ShowError(ex.Message); }
    }

    private async void CopyText(object? sender, RoutedEventArgs e)
    {
        await Clipboard!.SetTextAsync(Preview.RenderText());
        StatusText.Text = "Preview copiado al portapapeles";
    }

    private void RefreshPreview()
    {
        Preview.Scene = _scene;
        TextPreview.Text = Preview.RenderText();
    }

    private async Task ShowError(string message) => await new Window { Title = "Glyphoré", Width = 440, Height = 160, Content = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(20) } }.ShowDialog(this);
}
