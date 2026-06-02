using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Rendering;
using Kapture.Models;
using SkiaSharp;

namespace Kapture.Views.Dialogs;

public enum AnnotationTool
{
    Pen,
    Rectangle,
    Ellipse,
    Arrow,
    Text,
    Highlight
}

public partial class ScreenshotEditorWindow : Window
{
    private readonly CaptureEntry _entry;
    private AnnotationTool _tool = AnnotationTool.Pen;
    private Color _currentColor = Color.Parse("#F03030");
    private IBrush _currentBrush = new SolidColorBrush(Color.Parse("#F03030"));
    private double _strokeThickness = 3;
    private bool _isDrawing;
    private Point _dragStart;
    // Pen tool — Path+StreamGeometry rebuilt each move (Polyline.Points.Add doesn't
    // trigger AffectsGeometry in Avalonia, so the control never redraws in-place)
    private Avalonia.Controls.Shapes.Path? _activePenPath;
    private readonly List<Point> _penPoints = new();
    private Shape? _previewShape;
    private readonly Stack<Control> _undoStack = new();
    private Button? _activeToolBtn;
    private Button? _activeColorBtn;
    private Button? _activeStrokeBtn;
    private TextBox? _liveTextBox;
    private Bitmap? _baseBitmap; // KV-014: owned full-res screenshot; disposed in OnClosed

    public ScreenshotEditorWindow() : this(new CaptureEntry()) { }

    public ScreenshotEditorWindow(CaptureEntry entry)
    {
        _entry = entry;
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        LoadImage();
        SetActiveToolBtn(BtnPen);
        SetActiveColorBtn(C1);
        SetActiveStrokeBtn(StrokeMedium);
    }

    private void LoadImage()
    {
        // Resolve-by-filename so a screenshot restored from another device (Phase 3 slice G) opens too.
        var imagePath = _entry.ScreenshotPath;
        if (imagePath == null)
        {
            StatusText.Text = "Screenshot file not found — nothing to edit.";
            return;
        }

        _baseBitmap = new Bitmap(imagePath);
        AnnotationCanvas.Width = _baseBitmap.PixelSize.Width;
        AnnotationCanvas.Height = _baseBitmap.PixelSize.Height;

        var img = new Image
        {
            Source = _baseBitmap,
            Width = _baseBitmap.PixelSize.Width,
            Height = _baseBitmap.PixelSize.Height,
            Stretch = Stretch.None,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(img, 0);
        Canvas.SetTop(img, 0);
        AnnotationCanvas.Children.Add(img);

        StatusText.Text = $"{_baseBitmap.PixelSize.Width} × {_baseBitmap.PixelSize.Height}";
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _baseBitmap?.Dispose(); // KV-014: release the full-res native bitmap surface
        _baseBitmap = null;
    }

    private void SetActiveToolBtn(Button btn)
    {
        if (_activeToolBtn != null)
            _activeToolBtn.Classes.Remove("active");
        btn.Classes.Add("active");
        _activeToolBtn = btn;
    }

    private void SetActiveColorBtn(Button btn)
    {
        if (_activeColorBtn != null)
        {
            _activeColorBtn.BorderThickness = new Thickness(0);
        }
        btn.BorderThickness = new Thickness(2);
        btn.BorderBrush = Brushes.White;
        _currentColor = Color.Parse((string)btn.Tag!);
        _currentBrush = new SolidColorBrush(_currentColor);
        _activeColorBtn = btn;
    }

    private void SetActiveStrokeBtn(Button btn)
    {
        if (_activeStrokeBtn != null)
            _activeStrokeBtn.Classes.Remove("active");
        btn.Classes.Add("active");
        _strokeThickness = double.Parse((string)btn.Tag!);
        _activeStrokeBtn = btn;
    }

    private void BtnPen_Click(object? sender, RoutedEventArgs e)
    {
        _tool = AnnotationTool.Pen;
        SetActiveToolBtn(sender as Button ?? BtnPen);
    }

    private void BtnRect_Click(object? sender, RoutedEventArgs e)
    {
        _tool = AnnotationTool.Rectangle;
        SetActiveToolBtn(sender as Button ?? BtnRect);
    }

    private void BtnEllipse_Click(object? sender, RoutedEventArgs e)
    {
        _tool = AnnotationTool.Ellipse;
        SetActiveToolBtn(sender as Button ?? BtnEllipse);
    }

    private void BtnArrow_Click(object? sender, RoutedEventArgs e)
    {
        _tool = AnnotationTool.Arrow;
        SetActiveToolBtn(sender as Button ?? BtnArrow);
    }

    private void BtnText_Click(object? sender, RoutedEventArgs e)
    {
        _tool = AnnotationTool.Text;
        SetActiveToolBtn(sender as Button ?? BtnText);
    }

    private void BtnHighlight_Click(object? sender, RoutedEventArgs e)
    {
        _tool = AnnotationTool.Highlight;
        SetActiveToolBtn(sender as Button ?? BtnHighlight);
    }

    private void ColorSwatch_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            SetActiveColorBtn(btn);
    }

    private void StrokeBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            SetActiveStrokeBtn(btn);
    }

    private void Undo_Click(object? sender, RoutedEventArgs e)
    {
        if (_undoStack.TryPop(out var c))
            AnnotationCanvas.Children.Remove(c);
    }

    private void Clear_Click(object? sender, RoutedEventArgs e)
    {
        // Keep index 0 (base image), remove all annotations
        while (AnnotationCanvas.Children.Count > 1)
            AnnotationCanvas.Children.RemoveAt(AnnotationCanvas.Children.Count - 1);
        _undoStack.Clear();
    }

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        CommitLiveText();

        var pos = e.GetPosition(AnnotationCanvas);
        _dragStart = pos;
        _isDrawing = true;
        e.Pointer.Capture(AnnotationCanvas);

        switch (_tool)
        {
            case AnnotationTool.Pen:
                {
                    _penPoints.Clear();
                    _penPoints.Add(pos);
                    _activePenPath = new Avalonia.Controls.Shapes.Path
                    {
                        Stroke = _currentBrush,
                        StrokeThickness = _strokeThickness,
                        StrokeLineCap = PenLineCap.Round,
                        StrokeJoin = PenLineJoin.Round,
                        IsHitTestVisible = false,
                        Data = BuildPenGeometry(),
                    };
                    AnnotationCanvas.Children.Add(_activePenPath);
                    break;
                }
            case AnnotationTool.Rectangle:
                {
                    var rect = new Avalonia.Controls.Shapes.Rectangle
                    {
                        Stroke = _currentBrush,
                        Fill = Brushes.Transparent,
                        StrokeThickness = _strokeThickness,
                        Width = 0,
                        Height = 0,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(rect, pos.X);
                    Canvas.SetTop(rect, pos.Y);
                    AnnotationCanvas.Children.Add(rect);
                    _previewShape = rect;
                    break;
                }
            case AnnotationTool.Ellipse:
                {
                    var ellipse = new Avalonia.Controls.Shapes.Ellipse
                    {
                        Stroke = _currentBrush,
                        Fill = Brushes.Transparent,
                        StrokeThickness = _strokeThickness,
                        Width = 0,
                        Height = 0,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(ellipse, pos.X);
                    Canvas.SetTop(ellipse, pos.Y);
                    AnnotationCanvas.Children.Add(ellipse);
                    _previewShape = ellipse;
                    break;
                }
            case AnnotationTool.Arrow:
                {
                    var arrow = BuildArrowPath(_dragStart, pos);
                    AnnotationCanvas.Children.Add(arrow);
                    _previewShape = arrow;
                    break;
                }
            case AnnotationTool.Highlight:
                {
                    var r = _currentColor.R;
                    var g = _currentColor.G;
                    var b = _currentColor.B;
                    var highlight = new Avalonia.Controls.Shapes.Rectangle
                    {
                        Stroke = Brushes.Transparent,
                        Fill = new SolidColorBrush(Color.FromArgb(70, r, g, b)),
                        Width = 0,
                        Height = 0,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(highlight, pos.X);
                    Canvas.SetTop(highlight, pos.Y);
                    AnnotationCanvas.Children.Add(highlight);
                    _previewShape = highlight;
                    break;
                }
            case AnnotationTool.Text:
                {
                    PlaceTextBox(pos);
                    _isDrawing = false;
                    break;
                }
        }
    }

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(AnnotationCanvas);
        CoordText.Text = $"{(int)pos.X} , {(int)pos.Y}";

        if (!_isDrawing) return;

        switch (_tool)
        {
            case AnnotationTool.Pen:
                if (_activePenPath != null)
                {
                    _penPoints.Add(pos);
                    _activePenPath.Data = BuildPenGeometry(); // reassigning triggers redraw
                }
                break;

            case AnnotationTool.Rectangle:
            case AnnotationTool.Ellipse:
            case AnnotationTool.Highlight:
                {
                    if (_previewShape == null) break;
                    var x = Math.Min(_dragStart.X, pos.X);
                    var y = Math.Min(_dragStart.Y, pos.Y);
                    var w = Math.Abs(pos.X - _dragStart.X);
                    var h = Math.Abs(pos.Y - _dragStart.Y);
                    Canvas.SetLeft(_previewShape, x);
                    Canvas.SetTop(_previewShape, y);
                    _previewShape.Width = w;
                    _previewShape.Height = h;
                    break;
                }

            case AnnotationTool.Arrow:
                {
                    if (_previewShape != null)
                        AnnotationCanvas.Children.Remove(_previewShape);
                    _previewShape = BuildArrowPath(_dragStart, pos);
                    AnnotationCanvas.Children.Add(_previewShape);
                    break;
                }
        }
    }

    private void Canvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDrawing = false;
        e.Pointer.Capture(null);

        if (_activePenPath != null)
        {
            _undoStack.Push(_activePenPath);
            _activePenPath = null;
            _penPoints.Clear();
        }
        else if (_previewShape != null)
        {
            _undoStack.Push(_previewShape);
            _previewShape = null;
        }
    }

    // Rebuilds a StreamGeometry from _penPoints and assigns it to the active Path.
    // Reassigning .Data fires a property-change notification, which forces Avalonia to
    // re-render — unlike mutating Polyline.Points in-place, which is silently ignored.
    private StreamGeometry BuildPenGeometry()
    {
        var geo = new StreamGeometry();
        if (_penPoints.Count == 0) return geo;
        using var ctx = geo.Open();
        ctx.BeginFigure(_penPoints[0], false);
        for (var i = 1; i < _penPoints.Count; i++)
            ctx.LineTo(_penPoints[i]);
        ctx.EndFigure(false);
        return geo;
    }

    private Shape BuildArrowPath(Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        var angle = Math.Atan2(dy, dx);

        var arrowLen = Math.Clamp(len * 0.25, 8, 22);
        const double wingAngle = Math.PI / 6; // 30 degrees

        var wing1X = end.X - arrowLen * Math.Cos(angle - wingAngle);
        var wing1Y = end.Y - arrowLen * Math.Sin(angle - wingAngle);
        var wing2X = end.X - arrowLen * Math.Cos(angle + wingAngle);
        var wing2Y = end.Y - arrowLen * Math.Sin(angle + wingAngle);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            // Shaft
            ctx.BeginFigure(start, false);
            ctx.LineTo(end);
            ctx.EndFigure(false);

            // Arrowhead V
            ctx.BeginFigure(new Point(wing1X, wing1Y), false);
            ctx.LineTo(end);
            ctx.LineTo(new Point(wing2X, wing2Y));
            ctx.EndFigure(false);
        }

        return new Avalonia.Controls.Shapes.Path
        {
            Data = geo,
            Stroke = _currentBrush,
            StrokeThickness = _strokeThickness,
            StrokeLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        };
    }

    private void PlaceTextBox(Point pos)
    {
        var tb = new TextBox
        {
            Background = new SolidColorBrush(Color.Parse("#A0000000")),
            Foreground = _currentBrush,
            BorderBrush = _currentBrush,
            BorderThickness = new Thickness(1),
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            MinWidth = 80,
            IsHitTestVisible = true
        };
        Canvas.SetLeft(tb, pos.X);
        Canvas.SetTop(tb, pos.Y);
        AnnotationCanvas.Children.Add(tb);
        _liveTextBox = tb;

        tb.KeyDown += TextBox_KeyDown;
        tb.LostFocus += TextBox_LostFocus;

        Avalonia.Threading.Dispatcher.UIThread.Post(() => tb.Focus(),
            Avalonia.Threading.DispatcherPriority.Input);
    }

    private void TextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Escape)
        {
            CommitLiveText();
            e.Handled = true;
        }
    }

    private void TextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        CommitLiveText();
    }

    private void CommitLiveText()
    {
        if (_liveTextBox == null) return;
        var tb = _liveTextBox;
        _liveTextBox = null;

        tb.KeyDown -= TextBox_KeyDown;
        tb.LostFocus -= TextBox_LostFocus;

        if (string.IsNullOrWhiteSpace(tb.Text))
        {
            AnnotationCanvas.Children.Remove(tb);
            return;
        }

        var left = Canvas.GetLeft(tb);
        var top = Canvas.GetTop(tb);

        var textBlock = new TextBlock
        {
            Text = tb.Text,
            Foreground = _currentBrush,
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(textBlock, left);
        Canvas.SetTop(textBlock, top);

        AnnotationCanvas.Children.Remove(tb);
        AnnotationCanvas.Children.Add(textBlock);
        _undoStack.Push(textBlock);
    }

    private async void SaveAs_Click(object? sender, RoutedEventArgs e)
    {
        CommitLiveText();

        // KV-018: if the source image never loaded, the canvas size stays NaN; (int)NaN
        // is int.MinValue and would throw building the RenderTargetBitmap. Guard early.
        if (_baseBitmap == null || double.IsNaN(AnnotationCanvas.Width) || double.IsNaN(AnnotationCanvas.Height))
        {
            StatusText.Text = "Nothing to export — the screenshot did not load.";
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Annotated Screenshot",
            DefaultExtension = "png",
            SuggestedFileName = $"annotated_{System.IO.Path.GetFileNameWithoutExtension(_entry.Content)}",
            FileTypeChoices =
            [
                new FilePickerFileType("PNG Image") { Patterns = ["*.png"] },
                new FilePickerFileType("JPEG Image") { Patterns = ["*.jpg", "*.jpeg"] }
            ]
        });

        if (file == null) return;

        var w = (int)AnnotationCanvas.Width;
        var h = (int)AnnotationCanvas.Height;

        try
        {
            // KV-023: `using` guarantees the (full-canvas, ~33 MB) RenderTargetBitmap is
            // released even if render/encode/IO throws. The whole export is wrapped so a
            // failure surfaces in the status bar instead of crashing this async void handler.
            using var rtb = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
            rtb.Render(AnnotationCanvas);

            var fileName = file.Name.ToLowerInvariant();
            await using var stream = await file.OpenWriteAsync();

            if (fileName.EndsWith(".jpg") || fileName.EndsWith(".jpeg"))
            {
                using var ms = new System.IO.MemoryStream();
                rtb.Save(ms);
                ms.Position = 0;

                using var skData = SKData.Create(ms);
                using var skBitmap = SKBitmap.Decode(skData);
                using var skImage = SKImage.FromBitmap(skBitmap);
                using var encoded = skImage.Encode(SKEncodedImageFormat.Jpeg, 90);
                encoded.SaveTo(stream);
            }
            else
            {
                rtb.Save(stream);
            }

            StatusText.Text = "Exported.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Export failed: {ex.Message}";
        }
    }

    private void CloseBtn_Click(object? sender, RoutedEventArgs e) => Close();
}
