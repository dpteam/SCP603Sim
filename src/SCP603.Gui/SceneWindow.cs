using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Scp603.Core.Kernel;
using Scp603.Core.Morphik;
using Scp603.Core.Scenes;

namespace Scp603.Gui;

/// <summary>
/// A real SCP-603 scene window. The observation room does not embed the anomalous window:
/// the object creates a separate top-level window with its own title, lifetime, focus and
/// input stream, just as described in the file. The scene itself remains UI-independent.
/// </summary>
public sealed class SceneWindow : Window
{
    private const int RenderWidth = 720;
    private const int RenderHeight = 440;

    private readonly IScene _scene;
    private readonly Scp603Kernel _kernel;
    private readonly Image _image;
    private readonly DispatcherTimer _timer;
    private DateTime _lastTick = DateTime.UtcNow;

    public SceneWindow(string sceneId, MorphikProfile profile, ulong seed, Scp603Kernel kernel)
    {
        _kernel = kernel;
        _scene = SceneCatalog.Create(sceneId, kernel.Options.Scenes);
        _scene.Start(profile, seed);

        Title = _scene.Title;
        Width = SceneWidth(sceneId);
        Height = SceneHeight(sceneId);
        MinWidth = 320;
        MinHeight = 240;
        Background = Brushes.Black;
        ShowInTaskbar = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ResizeMode = ResizeMode.CanResize;
        Focusable = true;

        TrySetIcon();

        _image = new Image
        {
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true,
            Focusable = false,
        };
        Content = _image;

        PreviewKeyDown += OnPreviewKeyDown;
        TextCompositionManager.AddTextInputHandler(this, OnTextInput);
        MouseDown += OnMouseDown;
        Closed += OnClosed;

        // A real scene is an independent window with its own animation clock. 50 ms also
        // keeps the old-school visuals responsive without making the main observation room busy.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Render();

        // Deterministic placement makes repeated runs reproducible while still looking like
        // independently spawned windows rather than a panel of tabs.
        int screenIndex = (int)(seed % 7);
        Left = 70 + (screenIndex * 42);
        Top = 60 + ((screenIndex * 31) % 180);
    }

    public string SceneId => _scene.Id;

    public IScene Scene => _scene;

    private static int SceneWidth(string id) => id switch
    {
        SceneCatalog.IsThisId => 520,
        SceneCatalog.Jacob508Id => 760,
        SceneCatalog.JacobId => 760,
        SceneCatalog.DieId => 760,
        _ => 760,
    };

    private static int SceneHeight(string id) => id switch
    {
        SceneCatalog.IsThisId => 460,
        SceneCatalog.Jacob508Id => 560,
        SceneCatalog.JacobId => 620,
        SceneCatalog.DieId => 560,
        _ => 540,
    };

    private void Tick()
    {
        DateTime now = DateTime.UtcNow;
        double delta = Math.Clamp((now - _lastTick).TotalSeconds, 0, 0.25);
        _lastTick = now;

        try
        {
            _scene.Update(delta);
            if (_scene is ISceneEffects effects)
            {
                foreach (SceneEffect effect in effects.DrainEffects())
                {
                    _kernel.ApplyEffect(effect);
                }
            }

            Render();
            if (_scene.Status is SceneStatus.Finished or SceneStatus.Crashed)
            {
                // Keep the final frame visible. The operator can close it like an ordinary
                // OS window, which is the safe analogue of terminating SCP-603.
                _timer.Stop();
            }
        }
        catch (Exception ex)
        {
            _kernel.Journal.Write($"Окно {SceneId}: ошибка кадра: {ex.Message}");
            _timer.Stop();
        }
    }

    private void Render()
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            _scene.Draw(new WpfCanvas(dc, RenderWidth, RenderHeight));
        }

        var bitmap = new RenderTargetBitmap(RenderWidth, RenderHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        _image.Source = bitmap;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _scene.OnInput(SceneInput.Key_("Enter"));
            Render();
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            _scene.OnInput(SceneInput.Key_("Backspace"));
            Render();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text))
        {
            _scene.OnInput(SceneInput.Text_(e.Text));
            Render();
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Point p = e.GetPosition(_image);
        double scale = Math.Min(_image.ActualWidth / RenderWidth, _image.ActualHeight / RenderHeight);
        if (scale <= 0)
        {
            return;
        }

        double shownWidth = RenderWidth * scale;
        double shownHeight = RenderHeight * scale;
        int x = (int)Math.Clamp((p.X - ((_image.ActualWidth - shownWidth) / 2)) / scale, 0, RenderWidth - 1);
        int y = (int)Math.Clamp((p.Y - ((_image.ActualHeight - shownHeight) / 2)) / scale, 0, RenderHeight - 1);
        _scene.OnInput(SceneInput.Click(x, y));
        Focus();
        Render();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _scene.Stop();
        TextCompositionManager.RemoveTextInputHandler(this, OnTextInput);
    }

    private void TrySetIcon()
    {
        try
        {
            Icon = new BitmapImage(new Uri(
                "pack://application:,,,/SCP603.Gui;component/Assets/scp603.ico",
                UriKind.Absolute));
        }
        catch
        {
            // An icon is cosmetic; a missing packaged icon must not prevent a scene from opening.
        }
    }
}
