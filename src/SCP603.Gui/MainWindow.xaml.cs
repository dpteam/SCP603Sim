using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Scp603.Core.Kernel;
using Scp603.Core.Morphik;
using Scp603.Core.Scenes;

namespace Scp603.Gui;

public partial class MainWindow : Window
{
    private const int SceneWidth = 720;
    private const int SceneHeight = 440;
    private const int MaxLogLines = 4000;

    private readonly ConcurrentQueue<string> _primeLines = new();
    private readonly DispatcherTimer _primePump;
    private readonly DispatcherTimer _sceneTimer;
    private Scp603Kernel _kernel;
    private IScene? _scene;
    private ulong _sceneSeed;
    private DateTime _lastTick = DateTime.UtcNow;
    private int _primeCount;
    private int _windowCount;
    private string _lastPrimeFormatted = string.Empty;

    public MainWindow()
    {
        InitializeComponent();

        _kernel = CreateKernel();
        _kernel.PrimeGenerated += OnPrimeGenerated;
        _kernel.WindowOpened += OnWindowOpened;
        _kernel.VersionEmitted += OnVersionEmitted;
        _kernel.Log += OnKernelLog;

        _primePump = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _primePump.Tick += (_, _) => FlushPrimeLog();
        _primePump.Start();

        _sceneTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _sceneTimer.Tick += (_, _) => TickScene();
        _sceneTimer.Start();

        PopulateSceneMenu();
        UpdateProfileText();
        ShowScene(SceneCatalog.DieId);
        AppendLog("SCP-603: симулятор готов. «Объект → Запустить», чтобы включить основной поток.");
    }

    private Scp603Kernel CreateKernel()
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SCP603Sim");

        var options = new KernelOptions
        {
            BaseDirectory = baseDir,
            StartVersion = 1,
            SourceOnly = false,
            EnableSourceForge = true,
            ForgeTargetVersions = 8,
            ForgeStepDelayMs = 60,
            PrimeDelayMs = 0,
            JournalToFile = true,
            Scenes = new SceneHostOptions
            {
                SearchDirectories = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
                    Path.Combine(baseDir, "images"),
                },
            },
        };
        Directory.CreateDirectory(options.OutputDirectory);
        Directory.CreateDirectory(Path.Combine(baseDir, "images"));
        return new Scp603Kernel(options);
    }

    private void PopulateSceneMenu()
    {
        foreach (string id in SceneCatalog.AllIds)
        {
            var item = new System.Windows.Controls.MenuItem
            {
                Header = $"{id} — {SceneCatalog.NameOf(id)}",
                Tag = id,
            };
            item.Click += (_, _) => ShowScene((string)item.Tag);
            ScenesMenu.Items.Add(item);
        }
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(DelayBox.Text, out int delay) && delay >= 0)
        {
            _kernel.Options.PrimeDelayMs = delay;
        }

        if (!_kernel.IsRunning)
        {
            _kernel.Start();
            StatusText.Text = "SCP-603 запущена.";
            AppendLog("Объект запущен. Не допускается работа SCP-603 без присмотра.");
        }
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        _kernel.Stop();
        StatusText.Text = "SCP-603 остановлена.";
        AppendLog("Процесс завершён средствами ОС. Память Якова стёрта.");
    }

    private void OnGenerateVersionClick(object sender, RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<VersionEvent> emitted = _kernel.GenerateSources(1);
            AppendLog($"Выведена версия {emitted[0].Version} → {Path.GetFileName(emitted[0].SourcePath)}");
            UpdateProfileText();
        }
        catch (Exception ex)
        {
            AppendLog("Ошибка генерации: " + ex.Message);
        }
    }

    private void OnCompileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Файлы на Морфике для компиляции и линковки",
            Multiselect = true,
            Filter = "Морфик (*.morphik)|*.morphik|Все файлы (*.*)|*.*",
            InitialDirectory = _kernel.Options.SourceDirectory,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            MegaprimeArtifact artifact = _kernel.CompileFiles(dialog.FileNames);
            AppendLog($"Слинковано {dialog.FileNames.Length} файл(ов), megaprime: {artifact.Bytecode.Instructions.Length} инструкций.");
            AppendLog("Артефакт: " + Path.Combine(_kernel.Options.OutputDirectory, "megaprime"));

            var host = new RecordingMorphikHost { StopAfterEmits = 40 };
            new MorphikVm { StepLimit = 2_000_000 }.Run(artifact.Bytecode, host);
            string numbers = string.Join(" ", host.Emitted.Take(20).Select(Convert.ToInt64));
            AppendLog("Пробный запуск megaprime: " + numbers + " …");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка компиляции", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "Симулятор SCP-603.\n\n" +
            "Объект №: SCP-603\nКласс объекта: Безопасный\n\n" +
            "Это учебная и этичная имитация: все задокументированные эффекты воспроизводятся " +
            "только как текст, картинки и записи в журнале. Никакого воздействия на людей, " +
            "устройства или сеть не производится.\n\n" +
            "Реализовано: генерация простых чисел, самоизменяющийся язык «Морфик», компилятор и " +
            "линковщик в файл megaprime, а также описанные в документах сцены.",
            "SCP-603 — о симуляции",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // ------------------------------------------------------------------ primes

    private void OnPrimeGenerated(PrimeEvent evt)
    {
        _primeCount++;
        string line = $"{evt.SequenceNumber,8}   {evt.Formatted}";
        _primeLines.Enqueue(line);
        _lastPrimeFormatted = evt.Formatted;
    }

    private void OnWindowOpened(WindowEvent evt)
    {
        _windowCount = evt.WindowNumber;
        _primeLines.Enqueue($"          >>> ПРОСТОЕ ЧИСЛО {evt.Prime}: окно №{evt.WindowNumber} — {evt.SceneName} [{evt.SceneId}]");

        Dispatcher.BeginInvoke(() =>
        {
            WindowHint.Text = $"Последнее окно: №{evt.WindowNumber} на числе {evt.Prime} — {evt.SceneName} [{evt.SceneId}]";
            if (AutoWindowCheck.IsChecked == true)
            {
                ShowScene(evt.SceneId, (ulong)evt.Prime);
            }
        });
    }

    private void OnVersionEmitted(VersionEvent evt)
    {
        Dispatcher.BeginInvoke(() =>
        {
            AppendLog($"Новая версия {evt.Version} сохранена в {Path.GetFileName(evt.SourcePath)} (дайджест {evt.Digest:x16}).");
            UpdateProfileText();
        });
    }

    private void OnKernelLog(string line)
    {
        Dispatcher.BeginInvoke(() => AppendLog(line));
    }

    private void FlushPrimeLog()
    {
        if (_primeLines.IsEmpty)
        {
            return;
        }

        var sb = new StringBuilder();
        int added = 0;
        while (_primeLines.TryDequeue(out string? line) && added < 200)
        {
            sb.AppendLine(line);
            added++;
        }

        PrimeLog.AppendText(sb.ToString());

        // Trim from the top when the buffer grows too large.
        int lineCount = PrimeLog.LineCount;
        if (lineCount > MaxLogLines)
        {
            int cut = PrimeLog.GetCharacterIndexFromLineIndex(lineCount - MaxLogLines);
            if (cut > 0)
            {
                PrimeLog.Text = PrimeLog.Text[cut..];
            }
        }

        if (PrimeLog.IsVisible)
        {
            PrimeLog.ScrollToEnd();
        }

        StatusText.Text = $"Простых чисел: {_primeCount}   окон: {_windowCount}   версия {_kernel.LatestVersion}";
    }

    private void AppendLog(string text)
    {
        PrimeLog.AppendText("• " + text + Environment.NewLine);
        PrimeLog.ScrollToEnd();
    }

    private void UpdateProfileText()
    {
        MorphikProfile p = _kernel.Profile;
        VersionStatus.Text = $"версия {p.Version} · морфик {p.Digest():x8}";
        ProfileText.Text =
            $"Версия объекта : {p.Version}\n" +
            $"Язык самокопии : {p.Describe()}\n" +
            $"Сцены версии   : {string.Join(", ", _kernel.SceneSet.Sequence)}\n" +
            $"Каталог версий : {_kernel.Options.SourceDirectory}";
    }

    // ------------------------------------------------------------------ scenes

    private void ShowScene(string id, ulong? seed = null)
    {
        try
        {
            _scene = SceneCatalog.Create(id, _kernel.Options.Scenes);
            _sceneSeed = seed ?? (ulong)DateTime.UtcNow.Ticks;
            _scene.Start(_kernel.Profile, _sceneSeed);
            SceneTitle.Text = $"ОКНО SCP-603 — {_scene.Name} [{_scene.Id}]";
            SceneHint.Text = HintFor(id);
            InputBox.Clear();
            RenderScene();
        }
        catch (Exception ex)
        {
            AppendLog("Не удалось открыть сцену: " + ex.Message);
        }
    }

    private static string HintFor(string id) => id switch
    {
        SceneCatalog.DieId => "Введите команду (например: оглядеться, идти на север) и нажмите Enter.",
        SceneCatalog.JacobId => "Введите фразу и нажмите Enter. Ответ появится над треугольником.",
        SceneCatalog.Jacob508Id => "Работает только кнопка «Enter»: один диалог — одно нажатие.",
        SceneCatalog.RequiresInputId => "Введите до 13 символов и нажмите Enter.",
        SceneCatalog.IsThisId => "Отвечайте «да» или «нет» и нажимайте Enter.",
        SceneCatalog.TamagotchiId => "Щёлкайте мышью по кнопкам 1–3 в правом нижнем углу.",
        SceneCatalog.TellerId => "Сцена сама ищет изображение и пишет повествование.",
        SceneCatalog.PrinterId => "Сцена ищет «принтер»; документ сохраняется в каталог вывода.",
        _ => "Наблюдение. Сцена развивается самостоятельно.",
    };

    private void TickScene()
    {
        if (_scene is null)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        double delta = (now - _lastTick).TotalSeconds;
        _lastTick = now;
        delta = Math.Clamp(delta, 0, 0.2);

        try
        {
            _scene.Update(delta);

            if (_scene is ISceneEffects effects)
            {
                foreach (SceneEffect effect in effects.DrainEffects())
                {
                    _kernel.ApplyEffect(effect);
                    AppendLog($"[сцена {_scene.Id}] {effect.Kind}: {Truncate(effect.Payload, 120)}");
                }
            }

            RenderScene();

            if (_scene.Status is SceneStatus.Finished or SceneStatus.Crashed)
            {
                SceneTitle.Text = $"ОКНО SCP-603 — {_scene.Name} [{_scene.Id}] — {(_scene.Status == SceneStatus.Crashed ? "АВАРИЙНОЕ ЗАВЕРШЕНИЕ" : "завершено")}";
            }
        }
        catch (Exception ex)
        {
            AppendLog("Ошибка сцены: " + ex.Message);
        }
    }

    private void RenderScene()
    {
        if (_scene is null)
        {
            return;
        }

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            var canvas = new WpfCanvas(dc, SceneWidth, SceneHeight);
            _scene.Draw(canvas);
        }

        var bitmap = new RenderTargetBitmap(SceneWidth, SceneHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        SceneImage.Source = bitmap;
    }

    private void SendSceneInput(SceneInput input)
    {
        if (_scene is null)
        {
            return;
        }

        try
        {
            _scene.OnInput(input);
            RenderScene();
        }
        catch (Exception ex)
        {
            AppendLog("Ошибка ввода: " + ex.Message);
        }
    }

    private void OnSendClick(object sender, RoutedEventArgs e) => SubmitInput();

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SubmitInput();
            e.Handled = true;
        }
        else if (e.Key == Key.Back && InputBox.Text.Length == 0 && _scene is not null)
        {
            SendSceneInput(SceneInput.Key_("Backspace"));
            e.Handled = true;
        }
    }

    private void SubmitInput()
    {
        string text = InputBox.Text;
        if (text.Length == 0)
        {
            SendSceneInput(SceneInput.Text_("\n"));
        }
        else
        {
            SendSceneInput(SceneInput.Text_(text));
        }

        InputBox.Clear();
    }

    private void OnEnterKeyClick(object sender, RoutedEventArgs e)
        => SendSceneInput(SceneInput.Key_("Enter"));

    private void OnSceneMouseDown(object sender, MouseButtonEventArgs e)
    {
        Point p = e.GetPosition(SceneImage);
        Point scene = MapToScene(p);
        SendSceneInput(SceneInput.Click((int)scene.X, (int)scene.Y));
    }

    private Point MapToScene(Point imagePoint)
    {
        double imageW = SceneImage.ActualWidth;
        double imageH = SceneImage.ActualHeight;
        if (imageW <= 0 || imageH <= 0)
        {
            return new Point(0, 0);
        }

        // Image uses Stretch=Uniform, so it is letterboxed inside its box.
        double scale = Math.Min(imageW / SceneWidth, imageH / SceneHeight);
        double shownW = SceneWidth * scale;
        double shownH = SceneHeight * scale;
        double offsetX = (imageW - shownW) / 2;
        double offsetY = (imageH - shownH) / 2;

        double x = (imagePoint.X - offsetX) / scale;
        double y = (imagePoint.Y - offsetY) / scale;
        return new Point(
            Math.Clamp(x, 0, SceneWidth - 1),
            Math.Clamp(y, 0, SceneHeight - 1));
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";

    protected override void OnClosed(EventArgs e)
    {
        _primePump.Stop();
        _sceneTimer.Stop();
        _kernel.Dispose();
        base.OnClosed(e);
    }
}
