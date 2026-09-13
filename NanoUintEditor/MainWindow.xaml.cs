using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using NanoUint.Rendering;

namespace NanoUintEditor;

public partial class MainWindow : Window
{

    private readonly ObservableCollection<SceneObject> _objects = new();
    private SceneObject? _selected;
    private string _sceneName = "NewScreen";
    private string? _savePath;
    private bool _ready;
    private bool _dirty;
    private readonly UndoManager _undo = new();

    private static readonly string[] OnClickTemplates =
    {
        "PlayBGM(\"\");", "StopBGM();", "ShowBackground(\"\");", "CrossfadeBackground(\"\", 1f);",
        "SetText(\"\");", "SetSpeaker(\"\");", "ShowChoices(new[] { \"\", \"\" });", "Flash(0.5f);",
        "SaveGame();", "LoadGame();", "NextLine();", "JumpTo(\"\");",
    };
    private readonly List<SceneObject> _multiSelection = new();
    private bool _isMarquee;
    private Point _marqueeStart;
    private Rectangle? _marqueeRect;

    private bool _isDragging;
    private bool _isResizing;
    private Point _dragStart;
    private double _dragStartX, _dragStartY, _dragStartW, _dragStartH;
    private string _resizeHandle = "";
    private const double HandleSize = 8;
    private Rectangle? _selRect;
    private readonly List<Rectangle> _handles = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => { _ready = true; BuildAssetTree(); DrawGrid(); UpdateStatus(); RefreshRecentCombo(); };
        KeyDown += Window_KeyDown;
    }

    private SceneObject CreateObject(string name, double x, double y, double w = 300, double h = 200)
    {
        var obj = new SceneObject
        {
            Name = name, X = x, Y = y, Width = w, Height = h,
            SortingOrder = _objects.Count,
        };
        obj.Components.Add(new ComponentData { Type = "Transform", Props = new() {
            ["X"] = x, ["Y"] = y, ["Width"] = w, ["Height"] = h, ["Opacity"] = 1.0, ["SortingOrder"] = obj.SortingOrder } });
        return obj;
    }

    private void AddObjectWithComponent(string componentType)
    {
        RecordUndo();
        var def = ComponentRegistry.All[componentType];
        var obj = CreateObject($"GameObject", 760, 440,
            componentType == "TextRenderer" ? 500 : 300,
            componentType == "TextRenderer" ? 80 : 200);
        obj.Name = componentType;
        obj.Components.Add(new ComponentData { Type = componentType, Props = new(def.Defaults) });
        _objects.Add(obj);
        SyncTransformFromData(obj);
        RenderObject(obj);
        SelectObject(obj);
        UpdateHierarchy();
        UpdateStatus();
        MarkDirty();
    }

    private void SyncTransformFromData(SceneObject obj)
    {
        var t = obj.GetComponent("Transform");
        if (t == null) return;
        obj.X = (t.Props.GetValueOrDefault("X") as double?) ?? obj.X;
        obj.Y = (t.Props.GetValueOrDefault("Y") as double?) ?? obj.Y;
        obj.Width = (t.Props.GetValueOrDefault("Width") as double?) ?? obj.Width;
        obj.Height = (t.Props.GetValueOrDefault("Height") as double?) ?? obj.Height;
        obj.Opacity = (t.Props.GetValueOrDefault("Opacity") as double?) ?? obj.Opacity;
        var so = t.Props.GetValueOrDefault("SortingOrder") as double?;
        obj.SortingOrder = so.HasValue ? (int)so.Value : obj.SortingOrder;
    }

    private void SyncTransformToData(SceneObject obj)
    {
        var t = obj.GetComponent("Transform");
        if (t == null) return;
        t.Props["X"] = obj.X;
        t.Props["Y"] = obj.Y;
        t.Props["Width"] = obj.Width;
        t.Props["Height"] = obj.Height;
        t.Props["Opacity"] = obj.Opacity;
        t.Props["SortingOrder"] = (double)obj.SortingOrder;
    }

    private void RenderObject(SceneObject obj)
    {
        if (obj.Visual != null) SceneCanvas.Children.Remove(obj.Visual);
        FrameworkElement? visual = null;

        if (obj.HasComponent("SpriteRenderer"))
        {
            var props = obj.GetComponent("SpriteRenderer")!.Props;
            var img = new Image { Width = obj.Width, Height = obj.Height, Stretch = Stretch.Uniform, Opacity = obj.Opacity };
            var path = props.GetValueOrDefault("sprite") as string;
            if (!string.IsNullOrEmpty(path))
            {
                img.Source = AssetCache.LoadImage(path);
            }
            if (img.Source == null)
            {
                var colorStr = props.GetValueOrDefault("color") as string ?? "#FFFFFF";
                var c = EditorUtils.ParseMediaColor(colorStr);
                visual = new Rectangle { Width = obj.Width, Height = obj.Height, Fill = new SolidColorBrush(c), Stroke = new SolidColorBrush(Color.FromRgb(100, 100, 120)), StrokeThickness = 1, Opacity = obj.Opacity };
                SceneCanvas.Children.Add(visual); Canvas.SetLeft(visual, SceneModel.WorldX(_objects, obj)); Canvas.SetTop(visual, SceneModel.WorldY(_objects, obj)); Canvas.SetZIndex(visual, obj.SortingOrder);
                obj.Visual = visual;
                return;
            }
            visual = img;
        }
        else if (obj.HasComponent("BackgroundRenderer"))
        {
            var img = new Image { Width = 1920, Height = 1080, Stretch = Stretch.UniformToFill, Opacity = obj.Opacity };
            var path = obj.GetComponent("BackgroundRenderer")!.Props.GetValueOrDefault("sprite") as string;
            if (!string.IsNullOrEmpty(path))
            {
                img.Source = AssetCache.LoadImage(path);
            }
            if (img.Source == null) { img.Source = null; /* show as black */ }
            visual = img;
            obj.X = 0; obj.Y = 0; obj.Width = 1920; obj.Height = 1080;
            SyncTransformToData(obj);
        }
        else if (obj.HasComponent("SpriteButton"))
        {
            var props = obj.GetComponent("SpriteButton")!.Props;
            var img = new Image { Width = obj.Width, Height = obj.Height, Stretch = Stretch.Uniform, Opacity = obj.Opacity };
            var path = props.GetValueOrDefault("sprite") as string;
            if (!string.IsNullOrEmpty(path))
            {
                img.Source = AssetCache.LoadImage(path);
            }
            if (img.Source == null)
            {
                var btnRect = new Rectangle { Width = obj.Width, Height = obj.Height, Fill = new SolidColorBrush(Color.FromRgb(60, 80, 120)), Stroke = new SolidColorBrush(Color.FromRgb(100, 120, 160)), StrokeThickness = 1.5, RadiusX = 6, RadiusY = 6, Opacity = obj.Opacity };
                SceneCanvas.Children.Add(btnRect); Canvas.SetLeft(btnRect, obj.X); Canvas.SetTop(btnRect, obj.Y); Canvas.SetZIndex(btnRect, obj.SortingOrder);
                obj.Visual = btnRect;
                return;
            }
            visual = img;
        }
        else if (obj.HasComponent("PassiveButton"))
        {
            var colorStr = obj.GetComponent("PassiveButton")!.Props.GetValueOrDefault("color") as string ?? "#FFFFFF";
            var c = EditorUtils.ParseMediaColor(colorStr);
            var rect = new Rectangle { Width = obj.Width, Height = obj.Height, Fill = new SolidColorBrush(c) { Opacity = 0.3 }, Stroke = new SolidColorBrush(Color.FromRgb(160, 160, 180)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 2 }, Opacity = obj.Opacity };
            SceneCanvas.Children.Add(rect); Canvas.SetLeft(rect, obj.X); Canvas.SetTop(rect, obj.Y); Canvas.SetZIndex(rect, obj.SortingOrder);
            obj.Visual = rect;
            return;
        }
        else if (obj.HasComponent("TextRenderer"))
        {
            var props = obj.GetComponent("TextRenderer")!.Props;
            var tb = new TextBlock { Text = props.GetValueOrDefault("text") as string ?? "", FontSize = (props.GetValueOrDefault("fontSize") as double?) ?? 24, Foreground = EditorUtils.ParseColorBrush(props.GetValueOrDefault("color") as string ?? "#FFFFFF"), TextAlignment = (props.GetValueOrDefault("alignment") as string) switch { "Center" => TextAlignment.Center, "Right" => TextAlignment.Right, _ => TextAlignment.Left }, TextWrapping = TextWrapping.Wrap, Width = obj.Width, Opacity = obj.Opacity };
            visual = tb;
        }
        else if (obj.HasComponent("SDFTextRenderer"))
        {
            var props = obj.GetComponent("SDFTextRenderer")!.Props;
            var tb = new TextBlock { Text = props.GetValueOrDefault("text") as string ?? "", FontSize = (props.GetValueOrDefault("fontSize") as double?) ?? 24, Foreground = EditorUtils.ParseColorBrush(props.GetValueOrDefault("color") as string ?? "#FFFFFF"), TextWrapping = TextWrapping.Wrap, Width = obj.Width, Opacity = obj.Opacity, FontWeight = FontWeights.Bold };
            visual = tb;
        }
        else if (obj.HasComponent("DialogueBox"))
        {
            var props = obj.GetComponent("DialogueBox")!.Props;
            var speaker = props.GetValueOrDefault("speakerName") as string ?? "";
            var text = props.GetValueOrDefault("text") as string ?? "";
            var panel = new StackPanel { Width = obj.Width, Opacity = obj.Opacity };
            panel.Children.Add(new TextBlock { Text = speaker, FontSize = 18, FontWeight = FontWeights.Bold, Foreground = EditorUtils.ParseColorBrush("#3EBFBF"), Margin = new Thickness(0, 0, 0, 4) });
            panel.Children.Add(new TextBlock { Text = text, FontSize = 16, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
            visual = panel;
        }
        else if (obj.HasComponent("ChoiceGroup"))
        {
            var opts = (obj.GetComponent("ChoiceGroup")!.Props.GetValueOrDefault("options") as string ?? "").Split('|');
            var panel = new StackPanel { Opacity = obj.Opacity };
            foreach (var opt in opts)
                panel.Children.Add(new TextBlock { Text = $"▸ {opt}", FontSize = 18, Foreground = EditorUtils.ParseColorBrush("#E5C04B"), Margin = new Thickness(0, 4, 0, 4) });
            visual = panel;
        }
        else if (obj.HasComponent("FlashOverlay"))
        {
            var props = obj.GetComponent("FlashOverlay")!.Props;
            var colorStr = props.GetValueOrDefault("color") as string ?? "#000000";
            var alpha = (props.GetValueOrDefault("opacity") as double?) ?? 0.5;
            var c = EditorUtils.ParseMediaColor(colorStr);
            var rect = new Rectangle { Width = 1920, Height = 1080, Fill = new SolidColorBrush(c) { Opacity = alpha }, Opacity = obj.Opacity };
            SceneCanvas.Children.Add(rect); Canvas.SetLeft(rect, 0); Canvas.SetTop(rect, 0); Canvas.SetZIndex(rect, obj.SortingOrder);
            obj.X = 0; obj.Y = 0; obj.Width = 1920; obj.Height = 1080; SyncTransformToData(obj);
            obj.Visual = rect;
            return;
        }
        else if (obj.HasComponent("AdvanceIndicator"))
        {
            var path = obj.GetComponent("AdvanceIndicator")!.Props.GetValueOrDefault("sprite") as string;
            var img = new Image { Width = obj.Width, Height = obj.Height, Stretch = Stretch.Uniform, Opacity = obj.Opacity };
            if (!string.IsNullOrEmpty(path)) { img.Source = AssetCache.LoadImage(path); }
            visual = img;
        }
        else
        {
            var rect = new Rectangle { Width = obj.Width, Height = obj.Height, Fill = new SolidColorBrush(Color.FromRgb(50, 50, 60)), Stroke = new SolidColorBrush(Color.FromRgb(100, 100, 120)), StrokeThickness = 1, Opacity = obj.Opacity };
            SceneCanvas.Children.Add(rect); Canvas.SetLeft(rect, obj.X); Canvas.SetTop(rect, obj.Y); Canvas.SetZIndex(rect, obj.SortingOrder);
            obj.Visual = rect;
            return;
        }

        if (visual != null)
        {
            SceneCanvas.Children.Add(visual); Canvas.SetLeft(visual, SceneModel.WorldX(_objects, obj)); Canvas.SetTop(visual, SceneModel.WorldY(_objects, obj)); Canvas.SetZIndex(visual, obj.SortingOrder);
            obj.Visual = visual;
        }
    }

    private void RefreshAll()
    {
        SceneCanvas.Children.Clear(); SceneCanvas.Children.Add(GridOverlay); _selRect = null; _handles.Clear();
        foreach (var obj in _objects.OrderBy(o => o.SortingOrder)) RenderObject(obj);
        if (_selected != null) ShowSelection(_selected);
    }

    private void SelectObject(SceneObject? obj)
    {
        _selected = obj;
        _multiSelection.Clear();
        if (obj != null) _multiSelection.Add(obj);
        ShowSelection(obj); UpdateInspector(obj); UpdateHierarchySelection();
    }

    private void ToggleMultiSelect(SceneObject obj)
    {
        if (_multiSelection.Contains(obj)) _multiSelection.Remove(obj);
        else _multiSelection.Add(obj);
        _selected = _multiSelection.Count > 0 ? _multiSelection[^1] : null;
        ShowSelection(_selected);
        UpdateInspector(_selected);
        UpdateHierarchySelection();
    }

    private void ShowSelection(SceneObject? obj)
    {
        if (_selRect != null) { SceneCanvas.Children.Remove(_selRect); _selRect = null; }
        foreach (var h in _handles) SceneCanvas.Children.Remove(h);
        _handles.Clear();

        foreach (var extra in _multiSelection.Where(o => o != obj))
        {
            var ex = SceneModel.WorldX(_objects, extra); var ey = SceneModel.WorldY(_objects, extra);
            var rect = new Rectangle { Width = extra.Width + 4, Height = extra.Height + 4, Stroke = new SolidColorBrush(Color.FromRgb(176, 102, 255)), StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 3, 2 }, Fill = Brushes.Transparent, IsHitTestVisible = false };
            SceneCanvas.Children.Add(rect); Canvas.SetLeft(rect, ex - 2); Canvas.SetTop(rect, ey - 2); Canvas.SetZIndex(rect, 9998);
        }

        if (obj == null) return;

        var wx = SceneModel.WorldX(_objects, obj); var wy = SceneModel.WorldY(_objects, obj);
        _selRect = new Rectangle { Width = obj.Width + 4, Height = obj.Height + 4, Stroke = new SolidColorBrush(Color.FromRgb(74, 140, 255)), StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 2 }, Fill = Brushes.Transparent, IsHitTestVisible = false };
        SceneCanvas.Children.Add(_selRect); Canvas.SetLeft(_selRect, wx - 2); Canvas.SetTop(_selRect, wy - 2); Canvas.SetZIndex(_selRect, 9999);
        AddHandle(obj, "nw", wx - 4, wy - 4, Cursors.SizeNWSE);
        AddHandle(obj, "ne", wx + obj.Width - 4, wy - 4, Cursors.SizeNESW);
        AddHandle(obj, "sw", wx - 4, wy + obj.Height - 4, Cursors.SizeNESW);
        AddHandle(obj, "se", wx + obj.Width - 4, wy + obj.Height - 4, Cursors.SizeNWSE);
    }

    private void AddHandle(SceneObject obj, string tag, double x, double y, Cursor cursor)
    {
        var h = new Rectangle { Width = HandleSize, Height = HandleSize, Fill = Brushes.White, Stroke = new SolidColorBrush(Color.FromRgb(74, 140, 255)), StrokeThickness = 1, Cursor = cursor, Tag = tag };
        _handles.Add(h); SceneCanvas.Children.Add(h); Canvas.SetLeft(h, x); Canvas.SetTop(h, y); Canvas.SetZIndex(h, 10000);
    }

    private void Scene_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(SceneCanvas);

        // Check resize handles first (they are on top)
        if (e.OriginalSource is Rectangle rect && rect.Tag is string tag && tag.Length == 2)
        {
            if (_selected == null) return;
            _isResizing = true;
            _isDragging = false;
            _resizeHandle = tag;
            _dragStart = pos;
            _dragStartX = SceneModel.WorldX(_objects, _selected); _dragStartY = SceneModel.WorldY(_objects, _selected);
            _dragStartW = _selected.Width; _dragStartH = _selected.Height;
            SceneCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }

        SceneObject? hit = null;
        for (int i = _objects.Count - 1; i >= 0; i--) { var o = _objects[i]; var wx = SceneModel.WorldX(_objects, o); var wy = SceneModel.WorldY(_objects, o); if (pos.X >= wx && pos.X <= wx + o.Width && pos.Y >= wy && pos.Y <= wy + o.Height) { hit = o; break; } }
        if (hit != null)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { ToggleMultiSelect(hit); e.Handled = true; return; }
            SelectObject(hit); RecordUndo(); _isDragging = true; _isResizing = false; _dragStart = pos; _dragStartX = SceneModel.WorldX(_objects, hit); _dragStartY = SceneModel.WorldY(_objects, hit); SceneCanvas.CaptureMouse();
        }
        else
        {
            SelectObject(null);
            _isMarquee = true; _isDragging = false; _isResizing = false; _marqueeStart = pos;
            _marqueeRect = new Rectangle { Stroke = new SolidColorBrush(Color.FromRgb(74, 140, 255)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 2 }, Fill = new SolidColorBrush(Color.FromArgb(30, 74, 140, 255)), IsHitTestVisible = false };
            SceneCanvas.Children.Add(_marqueeRect); Canvas.SetZIndex(_marqueeRect, 9997);
            SceneCanvas.CaptureMouse();
        }
    }

    private void Scene_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(SceneCanvas);

        if (_isMarquee && _marqueeRect != null)
        {
            var x = Math.Min(pos.X, _marqueeStart.X); var y = Math.Min(pos.Y, _marqueeStart.Y);
            _marqueeRect.Width = Math.Abs(pos.X - _marqueeStart.X);
            _marqueeRect.Height = Math.Abs(pos.Y - _marqueeStart.Y);
            Canvas.SetLeft(_marqueeRect, x); Canvas.SetTop(_marqueeRect, y);
            return;
        }

        if (_isResizing && _selected != null)
        {
            var dx = pos.X - _dragStart.X; var dy = pos.Y - _dragStart.Y;
            var parent = _selected.ParentId != null ? _objects.FirstOrDefault(o => o.Id == _selected.ParentId) : null;
            var pwx = parent != null ? SceneModel.WorldX(_objects, parent) : 0;
            var pwy = parent != null ? SceneModel.WorldY(_objects, parent) : 0;
            switch (_resizeHandle)
            {
                case "se":
                    _selected.Width = Math.Max(20, _dragStartW + dx); _selected.Height = Math.Max(20, _dragStartH + dy); break;
                case "sw":
                    _selected.Width = Math.Max(20, _dragStartW - dx); _selected.Height = Math.Max(20, _dragStartH + dy);
                    _selected.X = (_dragStartX + dx) - pwx; break;
                case "ne":
                    _selected.Width = Math.Max(20, _dragStartW + dx); _selected.Height = Math.Max(20, _dragStartH - dy);
                    _selected.Y = (_dragStartY + dy) - pwy; break;
                case "nw":
                    _selected.Width = Math.Max(20, _dragStartW - dx); _selected.Height = Math.Max(20, _dragStartH - dy);
                    _selected.X = (_dragStartX + dx) - pwx; _selected.Y = (_dragStartY + dy) - pwy; break;
            }
            SyncTransformToData(_selected);
            UpdateVisualPosition(_selected); ShowSelection(_selected);
            return;
        }

        if (_isDragging && _selected != null)
        {
            var dx = pos.X - _dragStart.X; var dy = pos.Y - _dragStart.Y;
            double newWorldX, newWorldY;
            if (GridToggle.IsChecked == true) { const int g = 32; newWorldX = Math.Round((_dragStartX + dx) / g) * g; newWorldY = Math.Round((_dragStartY + dy) / g) * g; }
            else { newWorldX = _dragStartX + dx; newWorldY = _dragStartY + dy; }
            // Convert world to local (subtract parent world position)
            var parent = _selected.ParentId != null ? _objects.FirstOrDefault(o => o.Id == _selected.ParentId) : null;
            var oldLocalX = _selected.X; var oldLocalY = _selected.Y;
            _selected.X = newWorldX - (parent != null ? SceneModel.WorldX(_objects, parent) : 0);
            _selected.Y = newWorldY - (parent != null ? SceneModel.WorldY(_objects, parent) : 0);
            SyncTransformToData(_selected);
            UpdateVisualPosition(_selected); ShowSelection(_selected);
        }
    }

    private void Scene_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isMarquee)
        {
            _isMarquee = false;
            if (_marqueeRect != null) { SceneCanvas.Children.Remove(_marqueeRect); _marqueeRect = null; }
            var x0 = Math.Min(e.GetPosition(SceneCanvas).X, _marqueeStart.X); var y0 = Math.Min(e.GetPosition(SceneCanvas).Y, _marqueeStart.Y);
            var x1 = Math.Max(e.GetPosition(SceneCanvas).X, _marqueeStart.X); var y1 = Math.Max(e.GetPosition(SceneCanvas).Y, _marqueeStart.Y);
            _multiSelection.Clear();
            foreach (var o in _objects)
            {
                var wx = SceneModel.WorldX(_objects, o); var wy = SceneModel.WorldY(_objects, o);
                if (wx + o.Width >= x0 && wx <= x1 && wy + o.Height >= y0 && wy <= y1) _multiSelection.Add(o);
            }
            _selected = _multiSelection.Count > 0 ? _multiSelection[^1] : null;
            ShowSelection(_selected); UpdateInspector(_selected); UpdateHierarchySelection();
            return;
        }
        if (_isDragging || _isResizing) MarkDirty();
        if (_isResizing && _selected != null)
            ApplySizeToPpu(_selected);
        _isDragging = false; _isResizing = false;
        SceneCanvas.ReleaseMouseCapture();
        if (_selected != null) UpdateInspector(_selected);
    }

    private void Scene_Wheel(object sender, MouseWheelEventArgs e) { var idx = ZoomCombo.SelectedIndex; if (e.Delta > 0 && idx < ZoomCombo.Items.Count - 1) ZoomCombo.SelectedIndex = idx + 1; else if (e.Delta < 0 && idx > 0) ZoomCombo.SelectedIndex = idx - 1; }

    private void Scene_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.Copy; e.Handled = true; return; }
        e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Scene_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            var pos = e.GetPosition(SceneCanvas);
            RecordUndo();
            double dx = 0;
            foreach (var file in files.Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)))
            {
                string rel = file;
                try
                {
                    if (System.IO.Directory.Exists(AssetCache.ResourcesRoot))
                    {
                        var name = System.IO.Path.GetFileName(file);
                        var dest = System.IO.Path.Combine(AssetCache.ResourcesRoot, name);
                        if (!System.IO.File.Exists(dest)) System.IO.File.Copy(file, dest);
                        rel = name;
                    }
                }
                catch { rel = file; } // Fall back to the absolute path if copying fails
                var obj = CreateObject(System.IO.Path.GetFileNameWithoutExtension(file), pos.X - 150 + dx, pos.Y - 100);
                obj.Components.Add(new ComponentData { Type = "SpriteRenderer", Props = new() { ["sprite"] = rel, ["color"] = "#FFFFFF", ["pixelsPerUnit"] = 100.0 } });
                _objects.Add(obj); ApplyPpuToSize(obj); RenderObject(obj); SelectObject(obj); UpdateHierarchy(); UpdateStatus(); MarkDirty();
                dx += 60;
            }
            BuildAssetTree();
            return;
        }
        if (e.Data.GetData(DataFormats.StringFormat) is string tplPath && tplPath.StartsWith("template:"))
        {
            var objs = InstantiateTemplate(tplPath["template:".Length..], e.GetPosition(SceneCanvas).X, e.GetPosition(SceneCanvas).Y);
            if (objs == null) return;
            RecordUndo();
            foreach (var o in objs) { _objects.Add(o); RenderObject(o); }
            SelectObject(objs[^1]); UpdateHierarchy(); UpdateStatus(); MarkDirty();
            return;
        }
        if (e.Data.GetData(DataFormats.StringFormat) is string path)
        {
            RecordUndo();
            var pos = e.GetPosition(SceneCanvas);
            var obj = CreateObject(System.IO.Path.GetFileNameWithoutExtension(path), pos.X - 150, pos.Y - 100);
            obj.Components.Add(new ComponentData { Type = "SpriteRenderer", Props = new() { ["sprite"] = path, ["color"] = "#FFFFFF", ["pixelsPerUnit"] = 100.0 } });
            _objects.Add(obj); ApplyPpuToSize(obj); RenderObject(obj); SelectObject(obj); UpdateHierarchy(); UpdateStatus(); MarkDirty();
        }
    }

    private void DrawGrid()
    {
        GridOverlay.Children.Clear();
        if (GridToggle.IsChecked != true) return;
        var minor = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
        var major = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
        for (int x = 0; x <= 1920; x += 32) GridOverlay.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = 1080, Stroke = (x % 128 == 0) ? major : minor, StrokeThickness = (x % 128 == 0) ? 1 : 0.5 });
        for (int y = 0; y <= 1080; y += 32) GridOverlay.Children.Add(new Line { X1 = 0, Y1 = y, X2 = 1920, Y2 = y, Stroke = (y % 128 == 0) ? major : minor, StrokeThickness = (y % 128 == 0) ? 1 : 0.5 });
    }

    private void Grid_Changed(object sender, RoutedEventArgs e) { if (_ready) DrawGrid(); }

    private void UpdateHierarchy()
    {
        var selId = (_selected?.Id) ?? (HierarchyTree.SelectedItem as TreeViewItem)?.Tag as string;
        HierarchyTree.Items.Clear();
        var roots = _objects.Where(o => o.ParentId == null).OrderBy(o => o.SortingOrder);
        foreach (var root in roots)
            HierarchyTree.Items.Add(BuildHierarchyNode(root));
        if (selId != null)
            SelectInTree(HierarchyTree.Items, selId);
    }

    private TreeViewItem BuildHierarchyNode(SceneObject obj)
    {
        var icons = string.Join("", obj.Components.Where(c => ComponentRegistry.All.ContainsKey(c.Type)).Select(c => ComponentRegistry.All[c.Type].Icon));
        var item = new TreeViewItem { Header = $"{icons} {obj.Name}", Tag = obj.Id, IsExpanded = true };
        item.AllowDrop = true;
        item.DragOver += (s, e) => { if (e.Data.GetDataPresent(typeof(TreeViewItem))) { e.Effects = DragDropEffects.Move; e.Handled = true; } };
        item.Drop += (s, e) =>
        {
            if (e.Data.GetData(typeof(TreeViewItem)) is TreeViewItem dragged && dragged.Tag is string childId && childId != obj.Id && !SceneModel.IsDescendant(_objects, obj.Id, childId))
            {
                RecordUndo();
                var child = _objects.FirstOrDefault(o => o.Id == childId);
                if (child != null)
                {
                    // Convert child's world position to local relative to new parent
                    var oldWorldX = SceneModel.WorldX(_objects, child); var oldWorldY = SceneModel.WorldY(_objects, child);
                    child.ParentId = obj.Id;
                    child.X = oldWorldX - SceneModel.WorldX(_objects, obj);
                    child.Y = oldWorldY - SceneModel.WorldY(_objects, obj);
                    RenderObject(child); ShowSelection(child);
                    UpdateHierarchy(); UpdateInspector(_selected); UpdateStatus();
                }
            }
        };
        foreach (var child in SceneModel.GetChildren(_objects, obj.Id).OrderBy(o => o.SortingOrder))
            item.Items.Add(BuildHierarchyNode(child));
        return item;
    }

    private bool SelectInTree(ItemCollection items, string id)
    {
        foreach (TreeViewItem item in items)
        {
            if ((item.Tag as string) == id) { item.IsSelected = true; return true; }
            if (SelectInTree(item.Items, id)) return true;
        }
        return false;
    }

    private void UpdateHierarchySelection()
    {
        if (_selected == null) return;
        foreach (TreeViewItem item in HierarchyTree.Items) item.IsSelected = (item.Tag as string) == _selected.Id;
    }

    private void Hierarchy_Selected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem item) return;
        var obj = _objects.FirstOrDefault(o => o.Id == (item.Tag as string));
        if (obj != null && obj != _selected) SelectObject(obj);
    }

    private void BuildAssetTree()
    {
        AssetTree.Items.Clear();
        if (System.IO.Directory.Exists(TemplateDir()))
        {
            var tplNode = new TreeViewItem { Header = "📦 模板", IsExpanded = false };
            foreach (var f in System.IO.Directory.GetFiles(TemplateDir(), "*.json"))
                tplNode.Items.Add(CreateTemplateNode(f));
            if (tplNode.Items.Count > 0) AssetTree.Items.Add(tplNode);
        }
        if (!System.IO.Directory.Exists(AssetCache.ResourcesRoot)) return;
        foreach (var dir in System.IO.Directory.GetDirectories(AssetCache.ResourcesRoot))
        {
            var node = BuildAssetDirNode(dir);
            if (node != null) AssetTree.Items.Add(node);
        }
        foreach (var file in System.IO.Directory.GetFiles(AssetCache.ResourcesRoot, "*.png"))
        {
            var rel = System.IO.Path.GetRelativePath(AssetCache.ResourcesRoot, file).Replace('\\', '/');
            AssetTree.Items.Add(CreateAssetFileNode(rel));
        }
    }

    private TreeViewItem? BuildAssetDirNode(string dirPath)
    {
        var subdirs = System.IO.Directory.GetDirectories(dirPath);
        var files = System.IO.Directory.GetFiles(dirPath, "*.png");
        if (subdirs.Length == 0 && files.Length == 0) return null;

        var node = new TreeViewItem { Header = $"📁 {System.IO.Path.GetFileName(dirPath)}", IsExpanded = false };

        foreach (var sub in subdirs.OrderBy(d => d))
        {
            var child = BuildAssetDirNode(sub);
            if (child != null) node.Items.Add(child);
        }
        foreach (var file in files.OrderBy(f => f))
        {
            var rel = System.IO.Path.GetRelativePath(AssetCache.ResourcesRoot, file).Replace('\\', '/');
            node.Items.Add(CreateAssetFileNode(rel));
        }
        return node;
    }

    private TreeViewItem CreateTemplateNode(string file)
    {
        var item = new TreeViewItem { Header = $"📦 {System.IO.Path.GetFileNameWithoutExtension(file)}", Tag = "template:" + file };
        item.PreviewMouseLeftButtonDown += (s, e) => { DragDrop.DoDragDrop(item, "template:" + file, DragDropEffects.Copy); e.Handled = true; };
        item.MouseDoubleClick += (_, _) =>
        {
            var objs = InstantiateTemplate(file, 760, 440);
            if (objs == null) { System.Windows.MessageBox.Show("模板加载失败", "模板", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            RecordUndo();
            foreach (var o in objs) { _objects.Add(o); RenderObject(o); }
            SelectObject(objs[^1]); UpdateHierarchy(); UpdateStatus(); MarkDirty();
        };
        return item;
    }

    private TreeViewItem CreateAssetFileNode(string relPath)
    {
        var fi = new TreeViewItem { Header = $"🖼 {System.IO.Path.GetFileName(relPath)}", Tag = relPath };
        fi.PreviewMouseLeftButtonDown += (s, e) => { DragDrop.DoDragDrop(fi, relPath, DragDropEffects.Copy); e.Handled = true; };
        fi.MouseDoubleClick += (_, _) =>
        {
            RecordUndo();
            var obj = CreateObject(System.IO.Path.GetFileNameWithoutExtension(relPath), 760, 440);
            obj.Components.Add(new ComponentData { Type = "SpriteRenderer", Props = new() { ["sprite"] = relPath, ["color"] = "#FFFFFF", ["pixelsPerUnit"] = 100.0 } });
            _objects.Add(obj); ApplyPpuToSize(obj); RenderObject(obj); SelectObject(obj); UpdateHierarchy(); UpdateStatus(); MarkDirty();
        };
        return fi;
    }

    private void UpdateInspector(SceneObject? obj)
    {
        InspectorContent.Children.Clear();
        InspectorContent.Visibility = Visibility.Collapsed;
        InspectorEmpty.Visibility = Visibility.Visible;
        if (obj == null) return;
        InspectorEmpty.Visibility = Visibility.Collapsed;
        InspectorContent.Visibility = Visibility.Visible;

        AddInspectorHeader($"🎯 {obj.Name}");
        AddInspectorField("Name", obj.Name, s => { obj.Name = s; UpdateHierarchy(); });
        var parentRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 8, 2) };
        parentRow.Children.Add(new TextBlock { Text = "Parent", Width = 80, FontSize = 10, Foreground = FindResource("Text2Brush") as Brush, VerticalAlignment = VerticalAlignment.Center });
        var parentCombo = new ComboBox { Width = 170, FontSize = 10, Background = FindResource("BgBrush") as Brush, Foreground = FindResource("TextBrush") as Brush, BorderBrush = FindResource("BorderBrush") as Brush };
        var parentIdx = 0;
        parentCombo.Items.Add("(none — root)");
        foreach (var o in _objects.Where(o2 => o2.Id != obj.Id))
        {
            parentCombo.Items.Add(o.Name + " [" + o.Id + "]");
            if (o.Id == obj.ParentId) parentIdx = parentCombo.Items.Count - 1;
        }
        parentCombo.SelectedIndex = parentIdx;
        parentCombo.SelectionChanged += (_, _) =>
        {
            RecordUndo();
            string? newParent = null;
            if (parentCombo.SelectedIndex > 0)
            {
                var target = parentCombo.SelectedIndex - 1; // Combo items exclude obj itself, so skip it when mapping the index
                var skip = 0;
                for (int i = 0; i < _objects.Count; i++)
                {
                    if (_objects[i].Id == obj.Id) { skip = 1; continue; }
                    if (i - skip == target) { newParent = _objects[i].Id; break; }
                }
            }
            if (newParent != null && SceneModel.IsDescendant(_objects, obj.Id, newParent)) { UpdateInspector(obj); return; }
            obj.ParentId = newParent;
            RenderObject(obj); ShowSelection(obj); UpdateHierarchy(); UpdateStatus(); MarkDirty();
        };
        parentRow.Children.Add(parentCombo); InspectorContent.Children.Add(parentRow);

        var transform = obj.GetComponent("Transform");
        if (transform != null)
            RenderComponentInInspector(obj, "Transform", transform, false);

        foreach (var comp in obj.Components)
        {
            if (comp.Type == "Transform") continue;
            RenderComponentInInspector(obj, comp.Type, comp, true);
        }

        AddInspectorHeader("➕ Add Component");
        var comboRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 4, 8, 4) };
        var combo = new ComboBox { Width = 200, FontSize = 10, Background = FindResource("BgBrush") as Brush, Foreground = FindResource("TextBrush") as Brush, BorderBrush = FindResource("BorderBrush") as Brush };
        combo.Items.Add("-- 选择组件 --");
        foreach (var c in ComponentRegistry.Addable) combo.Items.Add($"{ComponentRegistry.All[c].Icon} {c}");
        combo.SelectedIndex = 0;
        combo.DropDownClosed += (_, _) =>
        {
            if (combo.SelectedIndex > 0)
            {
                RecordUndo();
                var type = ComponentRegistry.Addable[combo.SelectedIndex - 1];
                var def = ComponentRegistry.All[type];
                obj.Components.Add(new ComponentData { Type = type, Props = new(def.Defaults) });
                combo.SelectedIndex = 0;
                RenderObject(obj); SelectObject(obj); UpdateHierarchy(); UpdateStatus(); MarkDirty();
            }
        };
        comboRow.Children.Add(combo);
        InspectorContent.Children.Add(comboRow);

    }

    private void RenderComponentInInspector(SceneObject obj, string type, ComponentData comp, bool removable)
    {
        if (!ComponentRegistry.All.TryGetValue(type, out var def)) return;
        var borderColor = (SolidColorBrush)new BrushConverter().ConvertFromString(def.Color)!;

        var header = new Border
        {
            Background = FindResource("Bg2Brush") as Brush,
            BorderBrush = FindResource("BorderBrush") as Brush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8, 4, 8, 4),
            Child = new StackPanel { Orientation = Orientation.Horizontal },
        };
        var headerStack = (StackPanel)header.Child;
        headerStack.Children.Add(new Rectangle { Width = 3, Height = 14, Fill = borderColor, RadiusX = 1, RadiusY = 1, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        headerStack.Children.Add(new TextBlock { Text = $"{def.Icon} {def.Name}", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = FindResource("TextBrush") as Brush, VerticalAlignment = VerticalAlignment.Center });
        if (removable)
        {
            var removeBtn = new Button { Content = "✕", FontSize = 10, Foreground = FindResource("Text3Brush") as Brush, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(4, 0, 4, 0), HorizontalAlignment = HorizontalAlignment.Right, Cursor = Cursors.Hand };
            removeBtn.Click += (_, _) =>
            {
                RecordUndo();
                obj.Components.Remove(comp);
                RenderObject(obj); SelectObject(obj); UpdateHierarchy(); UpdateStatus(); MarkDirty();
            };
            ((StackPanel)header.Child).Children.Add(removeBtn);
        }
        InspectorContent.Children.Add(header);

        foreach (var (key, val) in comp.Props)
        {
            if (key == "color" || key == "overlayColor")
                AddInspectorColor(key, val as string ?? "#FFFFFF", s => { comp.Props[key] = s; RenderObject(obj); SelectObject(obj); });
            else if (key == "sprite" || key == "hoverSprite" || key == "pressSprite" || key == "clip")
                AddInspectorField(key, val as string ?? "", s => { comp.Props[key] = s; RenderObject(obj); SelectObject(obj); });
            else if (key == "fontSize" || key == "thickness" || key == "opacity" || key == "volume" || key == "x1" || key == "y1" || key == "x2" || key == "y2")
                AddInspectorField(key, String.Format("{0:F1}", (val as double?) ?? 0), s => { if (double.TryParse(s, out var v)) { comp.Props[key] = v; RenderObject(obj); SelectObject(obj); } });
            else if (key == "onClick")
                AddInspectorCommandField(key, val?.ToString() ?? "", s => { comp.Props[key] = s; });
            else if (key == "pixelsPerUnit")
                AddInspectorField(key, String.Format("{0:F0}", (val as double?) ?? 100), s => { if (double.TryParse(s, out var v) && v > 0) { comp.Props[key] = v; ApplyPpuToSize(obj); RenderObject(obj); SelectObject(obj); } });
            else if (key == "loop" || key == "crossfade")
                AddInspectorBool(key, val is true, b => { comp.Props[key] = b; RenderObject(obj); SelectObject(obj); });
            else if (key == "X" || key == "Y" || key == "Width" || key == "Height" || key == "SortingOrder")
                AddInspectorField(key, String.Format("{0:F0}", (val as double?) ?? 0), s => { if (double.TryParse(s, out var v)) { comp.Props[key] = v; SyncTransformFromData(obj); UpdateVisualPosition(obj); ShowSelection(obj); } });
            else if (key == "Opacity")
                AddInspectorField(key, String.Format("{0:F2}", (val as double?) ?? 1), s => { if (double.TryParse(s, out var v)) { comp.Props[key] = Math.Clamp(v, 0, 1); SyncTransformFromData(obj); if (obj.Visual != null) obj.Visual.Opacity = obj.Opacity; } });
            else
                AddInspectorField(key, val?.ToString() ?? "", s => { comp.Props[key] = s; RenderObject(obj); SelectObject(obj); });
        }
    }

    private void AddInspectorHeader(string text)
    {
        InspectorContent.Children.Add(new Border { Background = FindResource("Bg2Brush") as Brush, BorderBrush = FindResource("BorderBrush") as Brush, BorderThickness = new Thickness(0, 1, 0, 1), Padding = new Thickness(8, 4, 8, 4), Child = new TextBlock { Text = text, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = FindResource("Text3Brush") as Brush } });
    }

    private void AddInspectorField(string label, string value, Action<string> onChange)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 8, 2) };
        row.Children.Add(new TextBlock { Text = label, Width = 80, FontSize = 10, Foreground = FindResource("Text2Brush") as Brush, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        var tb = new TextBox { Text = value, Width = 170, FontSize = 10, Background = FindResource("BgBrush") as Brush, Foreground = FindResource("TextBrush") as Brush, BorderBrush = FindResource("BorderBrush") as Brush, BorderThickness = new Thickness(1) };
        tb.TextChanged += (_, _) => { MarkDirty(); RecordUndo(); onChange(tb.Text); };
        row.Children.Add(tb); InspectorContent.Children.Add(row);
    }

    private void AddInspectorColor(string label, string value, Action<string> onChange)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 8, 2) };
        row.Children.Add(new TextBlock { Text = label, Width = 80, FontSize = 10, Foreground = FindResource("Text2Brush") as Brush, VerticalAlignment = VerticalAlignment.Center });
        var rect = new Rectangle { Width = 22, Height = 20, Fill = EditorUtils.ParseColorBrush(value), Stroke = FindResource("BorderBrush") as Brush, StrokeThickness = 1, RadiusX = 2, RadiusY = 2, Cursor = Cursors.Hand };
        rect.MouseDown += (_, _) => { var dlg = new System.Windows.Forms.ColorDialog { Color = EditorUtils.ParseDrawingColor(value) }; if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) { var hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}{dlg.Color.A:X2}"; MarkDirty(); RecordUndo(); onChange(hex); rect.Fill = EditorUtils.ParseColorBrush(hex); } };
        row.Children.Add(rect);
        var tb = new TextBox { Text = value, Width = 130, FontSize = 10, Margin = new Thickness(6, 0, 0, 0), Background = FindResource("BgBrush") as Brush, Foreground = FindResource("TextBrush") as Brush, BorderBrush = FindResource("BorderBrush") as Brush, BorderThickness = new Thickness(1) };
        tb.TextChanged += (_, _) => { MarkDirty(); RecordUndo(); onChange(tb.Text); rect.Fill = EditorUtils.ParseColorBrush(tb.Text); };
        row.Children.Add(tb); InspectorContent.Children.Add(row);
    }

    private void AddInspectorBool(string label, bool value, Action<bool> onChange)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 8, 2) };
        row.Children.Add(new TextBlock { Text = label, Width = 80, FontSize = 10, Foreground = FindResource("Text2Brush") as Brush, VerticalAlignment = VerticalAlignment.Center });
        var cb = new CheckBox { IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
        cb.Checked += (_, _) => { MarkDirty(); RecordUndo(); onChange(true); }; cb.Unchecked += (_, _) => { MarkDirty(); RecordUndo(); onChange(false); };
        row.Children.Add(cb); InspectorContent.Children.Add(row);
    }

    private void AddInspectorCommandField(string label, string value, Action<string> onChange)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 8, 2) };
        row.Children.Add(new TextBlock { Text = label, Width = 80, FontSize = 10, Foreground = FindResource("Text2Brush") as Brush, VerticalAlignment = VerticalAlignment.Center });
        var tb = new TextBox { Text = value, Width = 128, FontSize = 10, Background = FindResource("BgBrush") as Brush, Foreground = FindResource("TextBrush") as Brush, BorderBrush = FindResource("BorderBrush") as Brush, BorderThickness = new Thickness(1) };
        tb.TextChanged += (_, _) => { MarkDirty(); RecordUndo(); onChange(tb.Text); };
        var combo = new ComboBox { Width = 84, FontSize = 10, Background = FindResource("BgBrush") as Brush, Foreground = FindResource("TextBrush") as Brush, BorderBrush = FindResource("BorderBrush") as Brush };
        combo.Items.Add("命令…");
        foreach (var t in OnClickTemplates) combo.Items.Add(t.Split('(')[0]);
        combo.SelectedIndex = 0;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex <= 0) return;
            var tpl = OnClickTemplates[combo.SelectedIndex - 1];
            tb.Text = tpl;
            MarkDirty(); RecordUndo(); onChange(tpl);
            combo.SelectedIndex = 0;
        };
        row.Children.Add(tb); row.Children.Add(combo);
        InspectorContent.Children.Add(row);
    }

    private void UpdateVisualPosition(SceneObject obj)
    {
        if (obj.Visual != null) { Canvas.SetLeft(obj.Visual, SceneModel.WorldX(_objects, obj)); Canvas.SetTop(obj.Visual, SceneModel.WorldY(_objects, obj)); obj.Visual.Width = obj.Width; obj.Visual.Height = obj.Height; obj.Visual.Opacity = obj.Opacity; }
    }

    private void ApplyPpuToSize(SceneObject obj)
    {
        var sr = obj.GetComponent("SpriteRenderer");
        if (sr == null) return;
        var ppu = sr.Props.GetValueOrDefault("pixelsPerUnit") as double? ?? 100.0;
        if (ppu <= 0) return;
        var spritePath = sr.Props.GetValueOrDefault("sprite") as string;
        if (string.IsNullOrEmpty(spritePath)) return;
        var native = AssetCache.GetNativeImageSize(spritePath);
        if (native == null) return;
        obj.Width = native.Value.w * 100.0 / ppu;
        obj.Height = native.Value.h * 100.0 / ppu;
        SyncTransformToData(obj);
    }

    private void ApplySizeToPpu(SceneObject obj)
    {
        var sr = obj.GetComponent("SpriteRenderer");
        if (sr == null) return;
        var spritePath = sr.Props.GetValueOrDefault("sprite") as string;
        if (string.IsNullOrEmpty(spritePath)) return;
        var native = AssetCache.GetNativeImageSize(spritePath);
        if (native == null || obj.Width <= 0) return;
        sr.Props["pixelsPerUnit"] = Math.Round(native.Value.w * 100.0 / obj.Width);
    }

    private void MoveAllDescendants(SceneObject obj, double dx, double dy)
    {
        foreach (var child in SceneModel.GetChildren(_objects, obj.Id))
        {
            child.X += dx; child.Y += dy;
            UpdateVisualPosition(child); ShowSelection(child);
            MoveAllDescendants(child, dx, dy);
        }
    }

    private void MarkDirty() => _dirty = true;

    private void UpdateStatus() { StatusObjects.Text = $"{_objects.Count} 对象"; StatusFile.Text = (_dirty ? "* " : "") + (_savePath != null ? System.IO.Path.GetFileNameWithoutExtension(_savePath) : "未保存"); }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_dirty)
        {
            var r = System.Windows.MessageBox.Show("场景有未保存的修改，要保存吗？", "未保存的更改", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes) SaveToFile();
            else if (r == MessageBoxResult.Cancel) { e.Cancel = true; return; }
        }
        base.OnClosing(e);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && _selected != null) DeleteSelected();
        else if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control && _selected != null) DuplicateSelected();
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) SaveToFile();
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) NewScene();
        else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) Undo();
        else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control) Redo();
        else if (e.Key == Key.Left && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) AlignByTag("left");
        else if (e.Key == Key.Right && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) AlignByTag("right");
        else if (e.Key == Key.Up && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) AlignByTag("top");
        else if (e.Key == Key.Down && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) AlignByTag("bottom");
    }

    private void Delete_Executed(object sender, ExecutedRoutedEventArgs e) => DeleteSelected();
    private void DeleteSelected()
    {
        if (_selected == null && _multiSelection.Count == 0) return;
        RecordUndo();
        var targets = _multiSelection.Count > 0 ? _multiSelection.ToList() : new List<SceneObject> { _selected! };
        var doomed = new List<SceneObject>();
        foreach (var t in targets)
        {
            if (doomed.Contains(t)) continue;
            doomed.Add(t);
            SceneModel.CollectDescendants(_objects, t, doomed);
        }
        foreach (var o in doomed)
        {
            if (o.Visual != null) SceneCanvas.Children.Remove(o.Visual);
            _objects.Remove(o);
        }
        _multiSelection.Clear();
        SelectObject(null); RefreshAll(); UpdateHierarchy(); UpdateStatus(); MarkDirty();
    }

    private void DuplicateSelected()
    {
        if (_selected == null) return;
        RecordUndo();
        var clone = new SceneObject { Name = _selected.Name + " (Copy)", X = _selected.X + 20, Y = _selected.Y + 20, Width = _selected.Width, Height = _selected.Height, Opacity = _selected.Opacity, ParentId = _selected.ParentId, SortingOrder = _objects.Count, Components = _selected.Components.Select(c => new ComponentData { Type = c.Type, Props = new(c.Props) }).ToList() };
        _objects.Add(clone); RenderObject(clone); SelectObject(clone); UpdateHierarchy(); UpdateStatus(); MarkDirty();
    }

    private void New_Click(object sender, RoutedEventArgs e) => NewScene();
    private static string TemplateDir() => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saves", "templates");

    private void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "模板 JSON|*.json",
            FileName = _selected.Name + ".json",
            InitialDirectory = TemplateDir(),
        };
        if (dlg.ShowDialog() != true) return;
        var subtree = new List<SceneObject>();
        SceneModel.CollectDescendants(_objects, _selected, subtree);
        subtree.Add(_selected);
        var data = new
        {
            name = _selected.Name,
            rootId = _selected.Id,
            objects = subtree.Select(o => new
            {
                o.Id, o.Name,
                ParentId = o == _selected ? null : o.ParentId,
                o.X, o.Y, o.Width, o.Height, o.Opacity, o.SortingOrder,
                Components = o.Components.Select(c => new { c.Type, c.Props }),
            }),
        };
        try
        {
            var dir = System.IO.Path.GetDirectoryName(dlg.FileName);
            if (dir != null) System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            BuildAssetTree();
            System.Windows.MessageBox.Show($"模板已保存: {dlg.FileName}", "模板", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"保存模板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private List<SceneObject>? InstantiateTemplate(string file, double x, double y)
    {
        try
        {
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(file));
            var data = doc.RootElement;
            var idMap = new Dictionary<string, string>();
            var objs = new List<SceneObject>();
            if (!data.TryGetProperty("objects", out var objsJson)) return null;
            foreach (var j in objsJson.EnumerateArray())
            {
                var oldId = j.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
                var newId = Guid.NewGuid().ToString("N")[..6];
                idMap[oldId] = newId;
                var comps = new List<ComponentData>();
                if (j.TryGetProperty("components", out var compsJson))
                    foreach (var cj in compsJson.EnumerateArray())
                    {
                        var props = new Dictionary<string, object?>();
                        if (cj.TryGetProperty("props", out var propsJson))
                            foreach (var p in propsJson.EnumerateObject())
                                props[p.Name] = p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetDouble() : p.Value.ValueKind == JsonValueKind.True ? true : p.Value.ValueKind == JsonValueKind.False ? false : p.Value.GetString();
                        if (cj.TryGetProperty("type", out var t))
                            comps.Add(new ComponentData { Type = t.GetString() ?? "", Props = props });
                    }
                objs.Add(new SceneObject
                {
                    Id = newId,
                    Name = j.TryGetProperty("name", out var nm) ? nm.GetString() ?? "Template" : "Template",
                    ParentId = j.TryGetProperty("parentId", out var pid) && pid.ValueKind != JsonValueKind.Null ? pid.GetString() : null,
                    X = j.TryGetProperty("x", out var xv) ? xv.GetDouble() : 0,
                    Y = j.TryGetProperty("y", out var yv) ? yv.GetDouble() : 0,
                    Width = j.TryGetProperty("width", out var wv) ? wv.GetDouble() : 100,
                    Height = j.TryGetProperty("height", out var hv) ? hv.GetDouble() : 100,
                    Opacity = j.TryGetProperty("opacity", out var ov) ? ov.GetDouble() : 1,
                    SortingOrder = j.TryGetProperty("sortingOrder", out var sv) ? sv.GetInt32() : 0,
                    Components = comps,
                });
            }
            foreach (var o in objs)
                if (o.ParentId != null && idMap.TryGetValue(o.ParentId, out var np)) o.ParentId = np;
            var rootId = data.TryGetProperty("rootId", out var rv) ? rv.GetString() : null;
            var root = rootId != null && idMap.TryGetValue(rootId, out var rid) ? objs.FirstOrDefault(o => o.Id == rid) : objs.FirstOrDefault(o => o.ParentId == null);
            if (root != null) { root.X = x; root.Y = y; }
            return objs;
        }
        catch { return null; }
    }

    private void Align_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string mode) AlignByTag(mode);
    }

    private void AlignByTag(string mode)
    {
        var targets = _multiSelection.Count > 0 ? _multiSelection : (_selected != null ? new List<SceneObject> { _selected } : null);
        if (targets == null || targets.Count == 0) return;
        RecordUndo();

        switch (mode)
        {
            case "left":
            {
                var min = targets.Min(t => SceneModel.WorldX(_objects, t));
                foreach (var t in targets) t.X = min - (t.ParentId != null ? SceneModel.WorldX(_objects, _objects.First(p => p.Id == t.ParentId)) : 0);
                break;
            }
            case "centerX":
            {
                var cx = targets.Average(t => SceneModel.WorldX(_objects, t) + t.Width / 2);
                foreach (var t in targets) { var wx = SceneModel.WorldX(_objects, t); t.X += cx - (wx + t.Width / 2); }
                break;
            }
            case "right":
            {
                var max = targets.Max(t => SceneModel.WorldX(_objects, t) + t.Width);
                foreach (var t in targets) { var wx = SceneModel.WorldX(_objects, t); t.X += max - (wx + t.Width); }
                break;
            }
            case "top":
            {
                var min = targets.Min(t => SceneModel.WorldY(_objects, t));
                foreach (var t in targets) t.Y = min - (t.ParentId != null ? SceneModel.WorldY(_objects, _objects.First(p => p.Id == t.ParentId)) : 0);
                break;
            }
            case "centerY":
            {
                var cy = targets.Average(t => SceneModel.WorldY(_objects, t) + t.Height / 2);
                foreach (var t in targets) { var wy = SceneModel.WorldY(_objects, t); t.Y += cy - (wy + t.Height / 2); }
                break;
            }
            case "bottom":
            {
                var max = targets.Max(t => SceneModel.WorldY(_objects, t) + t.Height);
                foreach (var t in targets) { var wy = SceneModel.WorldY(_objects, t); t.Y += max - (wy + t.Height); }
                break;
            }
            case "distH":
            {
                var ordered = targets.OrderBy(t => SceneModel.WorldX(_objects, t)).ToList();
                if (ordered.Count < 3) break;
                var first = SceneModel.WorldX(_objects, ordered[0]);
                var last = SceneModel.WorldX(_objects, ordered[^1]) + ordered[^1].Width;
                var totalW = ordered.Sum(t => t.Width);
                var gap = (last - first - totalW) / (ordered.Count - 1);
                double cursor = first;
                foreach (var t in ordered)
                {
                    var wx = SceneModel.WorldX(_objects, t);
                    t.X += cursor - wx;
                    cursor += t.Width + gap;
                }
                break;
            }
            case "distV":
            {
                var ordered = targets.OrderBy(t => SceneModel.WorldY(_objects, t)).ToList();
                if (ordered.Count < 3) break;
                var first = SceneModel.WorldY(_objects, ordered[0]);
                var last = SceneModel.WorldY(_objects, ordered[^1]) + ordered[^1].Height;
                var totalH = ordered.Sum(t => t.Height);
                var gap = (last - first - totalH) / (ordered.Count - 1);
                double cursor = first;
                foreach (var t in ordered)
                {
                    var wy = SceneModel.WorldY(_objects, t);
                    t.Y += cursor - wy;
                    cursor += t.Height + gap;
                }
                break;
            }
            case "size":
            {
                var w = targets[0].Width; var h = targets[0].Height;
                foreach (var t in targets.Skip(1)) { t.Width = w; t.Height = h; }
                break;
            }
        }

        foreach (var t in targets) { RenderObject(t); UpdateVisualPosition(t); }
        ShowSelection(_selected); UpdateInspector(_selected);
        MarkDirty();
    }

    private static string RecentFile => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NanoUintEditor", "recent.json");

    private List<string> LoadRecent()
    {
        try { if (System.IO.File.Exists(RecentFile)) return JsonSerializer.Deserialize<List<string>>(System.IO.File.ReadAllText(RecentFile)) ?? new(); }
        catch { }
        return new();
    }

    private void SaveRecent(List<string> recent)
    {
        try { var dir = System.IO.Path.GetDirectoryName(RecentFile); if (dir != null) System.IO.Directory.CreateDirectory(dir); System.IO.File.WriteAllText(RecentFile, JsonSerializer.Serialize(recent)); }
        catch { }
    }

    private void AddRecent(string path)
    {
        var recent = LoadRecent();
        recent.Remove(path);
        recent.Insert(0, path);
        if (recent.Count > 8) recent = recent.Take(8).ToList();
        SaveRecent(recent);
        RefreshRecentCombo();
    }

    private void RefreshRecentCombo()
    {
        if (RecentCombo == null) return;
        RecentCombo.Items.Clear();
        RecentCombo.Items.Add("最近文件…");
        foreach (var p in LoadRecent())
        {
            var dir = System.IO.Path.GetDirectoryName(p);
            RecentCombo.Items.Add($"{System.IO.Path.GetFileNameWithoutExtension(p)}  [{System.IO.Path.GetFileName(dir)}]");
        }
        RecentCombo.SelectedIndex = 0;
    }

    private ScenePreviewHost? _previewHost;
    private Window? _previewWindow;

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_previewWindow != null) { _previewWindow.Activate(); return; }
        var win = new Window
        {
            Title = "▶ 预览 — " + SceneName.Text,
            Width = 960,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = FindResource("BgBrush") as Brush,
        };
        var canvas = new Canvas { Background = Brushes.Black, Width = 960, Height = 540 };
        win.Content = canvas;
        var host = new ScenePreviewHost(canvas, 1920, 1080);
        win.Closed += (_, _) => { host.Stop(); _previewWindow = null; _previewHost = null; };
        _previewWindow = win;
        _previewHost = host;
        try
        {
            SceneRuntime.Build(host, _objects);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"预览构建失败: {ex.Message}", "播放模式", MessageBoxButton.OK, MessageBoxImage.Error);
            host.Stop();
            return;
        }
        win.Show();
        host.Start();
    }

    private void RecentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || RecentCombo.SelectedIndex <= 0) return;
        var recent = LoadRecent();
        var idx = RecentCombo.SelectedIndex - 1;
        if (idx < recent.Count) LoadFromPath(recent[idx]);
        RecentCombo.SelectedIndex = 0;
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => Redo();

    private void RecordUndo() => _undo.Push(SceneModel.CloneObjects(_objects));

    private void Undo()
    {
        var snap = _undo.Undo(SceneModel.CloneObjects(_objects));
        if (snap != null) ApplySnapshot(snap);
    }

    private void Redo()
    {
        var snap = _undo.Redo(SceneModel.CloneObjects(_objects));
        if (snap != null) ApplySnapshot(snap);
    }

    private void ApplySnapshot(List<SceneObject> snap)
    {
        _objects.Clear();
        foreach (var o in snap) _objects.Add(o);
        RefreshAll(); SelectObject(null); UpdateHierarchy(); UpdateInspector(null); UpdateStatus();
        _dirty = true;
    }

    private void NewScene() { _objects.Clear(); SceneCanvas.Children.Clear(); SceneCanvas.Children.Add(GridOverlay); _selRect = null; _handles.Clear(); _selected = null; _savePath = null; _sceneName = "NewScreen"; SceneName.Text = _sceneName; _dirty = false; _undo.Clear(); UpdateHierarchy(); UpdateInspector(null); UpdateStatus(); }
    private void AddObject_Click(object sender, RoutedEventArgs e)
    {
        var obj = CreateObject("GameObject", 760, 440);
        obj.Components.Add(new ComponentData { Type = "SpriteRenderer", Props = new(ComponentRegistry.All["SpriteRenderer"].Defaults) });
        _objects.Add(obj); ApplyPpuToSize(obj); RenderObject(obj); SelectObject(obj); UpdateHierarchy(); UpdateStatus();
    }
    private void FitScreen_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        RecordUndo();
        _selected.ParentId = null;
        _selected.X = 0; _selected.Y = 0; _selected.Width = 1920; _selected.Height = 1080;
        SyncTransformToData(_selected);
        ApplySizeToPpu(_selected);
        RenderObject(_selected); SelectObject(_selected); UpdateHierarchy(); UpdateStatus();
    }
    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelected();
    private void Save_Executed(object sender, ExecutedRoutedEventArgs e) => SaveToFile();

    private void Zoom_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if (ZoomCombo.SelectedItem is not ComboBoxItem cbi) return;
        if (double.TryParse(cbi.Content.ToString()!.TrimEnd('%'), out var pct)) { SceneViewbox.Width = 1920 * (pct / 100.0); SceneViewbox.Height = 1080 * (pct / 100.0); UpdateStatus(); }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveToFile();
    private void Load_Click(object sender, RoutedEventArgs e) => LoadFromFile();

    private void SaveToFile()
    {
        try
        {
            _sceneName = SceneName.Text;
            var data = new { name = _sceneName, version = 3, objects = _objects.Select(o => new { o.Id, o.Name, o.ParentId, o.X, o.Y, o.Width, o.Height, o.Opacity, o.SortingOrder, Components = o.Components.Select(c => new { c.Type, c.Props }) }) };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            var path = _savePath ?? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saves", $"{_sceneName}.json");
            var dir = System.IO.Path.GetDirectoryName(path); if (dir != null) System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(path, json); _savePath = path; _dirty = false; UpdateStatus();
            AddRecent(path);
            System.Windows.MessageBox.Show($"Saved to {path}", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadFromFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON Files|*.json|All Files|*.*", InitialDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saves") };
        if (dlg.ShowDialog() != true) return;
        LoadFromPath(dlg.FileName);
    }

    private void LoadFromPath(string path)
    {
        try
        {
            var json = System.IO.File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement;
            if (data.TryGetProperty("version", out var ver) && ver.GetInt32() > 3)
            {
                System.Windows.MessageBox.Show($"场景文件版本 ({ver.GetInt32()}) 高于编辑器支持的版本 (3)，可能无法完整加载。", "版本不兼容", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            _sceneName = data.TryGetProperty("name", out var n) ? n.GetString() ?? "Loaded" : "Loaded";
            SceneName.Text = _sceneName; _savePath = path; _objects.Clear();
            if (data.TryGetProperty("objects", out var objs))
            {
                foreach (var j in objs.EnumerateArray())
                {
                    var comps = new List<ComponentData>();
                    if (j.TryGetProperty("components", out var compsJson))
                        foreach (var cj in compsJson.EnumerateArray())
                        {
                            var props = new Dictionary<string, object?>();
                            if (cj.TryGetProperty("props", out var propsJson))
                                foreach (var p in propsJson.EnumerateObject())
                                    props[p.Name] = p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetDouble() : p.Value.ValueKind == JsonValueKind.True ? true : p.Value.ValueKind == JsonValueKind.False ? false : p.Value.GetString();
                            if (cj.TryGetProperty("type", out var t))
                                comps.Add(new ComponentData { Type = t.GetString() ?? "", Props = props });
                        }
                    var obj = new SceneObject
                    {
                        Id = j.TryGetProperty("id", out var id) ? id.GetString() ?? Guid.NewGuid().ToString("N")[..6] : Guid.NewGuid().ToString("N")[..6],
                        Name = j.TryGetProperty("name", out var nm) ? nm.GetString() ?? "GameObject" : "GameObject",
                        ParentId = j.TryGetProperty("parentId", out var pid) && pid.ValueKind != JsonValueKind.Null ? pid.GetString() : null,
                        X = j.TryGetProperty("x", out var x) ? x.GetDouble() : 0,
                        Y = j.TryGetProperty("y", out var y) ? y.GetDouble() : 0,
                        Width = j.TryGetProperty("width", out var w) ? w.GetDouble() : 100,
                        Height = j.TryGetProperty("height", out var h) ? h.GetDouble() : 100,
                        Opacity = j.TryGetProperty("opacity", out var op) ? op.GetDouble() : 1,
                        SortingOrder = j.TryGetProperty("sortingOrder", out var so) ? so.GetInt32() : 0,
                        Components = comps,
                    };
                    _objects.Add(obj);
                }
            }
            SceneModel.ValidateHierarchy(_objects);
            RefreshAll(); SelectObject(null); UpdateHierarchy(); UpdateInspector(null); UpdateStatus();
            _dirty = false;
            AddRecent(path);
        }
        catch (Exception ex)
        {
            _objects.Clear();
            System.Windows.MessageBox.Show($"加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GenCode_Click(object sender, RoutedEventArgs e) => GenerateCode();

    private void GenerateCode()
    {
        var code = CodeGenerator.Generate(_objects, SceneName.Text, ComputePpu);

        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.Children.Add(new TextBlock { Text = "Generated Code — Ctrl+C to copy", FontSize = 11, Foreground = FindResource("Text3Brush") as Brush, Margin = new Thickness(0, 0, 0, 8) });
        Grid.SetRow(grid.Children[0], 0);
        var codeBox = new TextBox { Text = code, IsReadOnly = true, FontFamily = new FontFamily("Consolas, Cascadia Code"), FontSize = 11, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = FindResource("BgBrush") as Brush, Foreground = FindResource("TextBrush") as Brush, BorderBrush = FindResource("BorderBrush") as Brush, TextWrapping = TextWrapping.NoWrap, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        grid.Children.Add(codeBox);
        Grid.SetRow(codeBox, 1);
        new Window { Title = "Generated C# Code", Width = 700, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = FindResource("Bg2Brush") as Brush, Content = grid }.ShowDialog();
    }

    private float ComputePpu(string spritePath, double width)
    {
        var native = AssetCache.GetNativeImageSize(spritePath);
        if (native == null || width <= 0) return 100f;
        return (float)(native.Value.w * 100.0 / width);
    }

}
