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
    private Scp603Kernel _kernel;
    private readonly List<SceneWindow> _sceneWindows = new();
    private string _selectedSceneId = SceneCatalog.DieId;
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

        PopulateSceneMenu();
        UpdateProfileText();
        SceneTitle.Text = "ОКНА SCP-603 открываются как отдельные окна ОС";
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
            item.Click += (_, _) => SelectScene((string)item.Tag);
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
                OpenSceneWindow(evt.SceneId, (ulong)evt.Prime);
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

    private void SelectScene(string id)
    {
        _selectedSceneId = id;
        IScene scene = SceneCatalog.Create(id, _kernel.Options.Scenes);
        SceneTitle.Text = $"Выбрана сцена: {scene.Name} [{scene.Id}]";
        SceneHint.Text = HintFor(id) + " Она будет открыта отдельным окном.";
    }

    private void OpenSelectedScene() => OpenSceneWindow(_selectedSceneId, (ulong)DateTime.UtcNow.Ticks);

    private void OpenSceneWindow(string id, ulong seed)
    {
        try
        {
            var window = new SceneWindow(id, _kernel.Profile, seed, _kernel);
            window.Closed += (_, _) => _sceneWindows.Remove(window);
            _sceneWindows.Add(window);
            window.Show();
            window.Activate();
            SceneTitle.Text = $"Открыто отдельное окно: {window.Scene.Name} [{window.Scene.Id}]";
            SceneHint.Text = HintFor(id) + " Управляется в отдельном окне.";
            AppendLog($"Открыто отдельное аномальное окно {window.Scene.Id}, seed={seed:x16}.");
        }
        catch (Exception ex)
        {
            AppendLog("Не удалось открыть сцену: " + ex.Message);
        }
    }

    private void CloseLastScene()
    {
        if (_sceneWindows.Count > 0)
        {
            _sceneWindows[^1].Close();
        }
    }

    private void OnOpenSelectedSceneClick(object sender, RoutedEventArgs e) => OpenSelectedScene();

    private void OnCloseLastSceneClick(object sender, RoutedEventArgs e) => CloseLastScene();

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

    protected override void OnClosed(EventArgs e)
    {
        _primePump.Stop();
        foreach (SceneWindow window in _sceneWindows.ToArray())
        {
            window.Close();
        }
        _kernel.Dispose();
        base.OnClosed(e);
    }
}
