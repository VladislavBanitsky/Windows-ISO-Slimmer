using Microsoft.UI;
using Microsoft.UI; // Для Win32Interop и WindowId
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics; // Для SizeInt32
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace IsoSlimmer;

public sealed partial class MainWindow : Window
{
    // Внутри класса MainWindow
    private StringBuilder _fullLog = new StringBuilder();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public string lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int flagsEx;
    }

    // Структура для SHBrowseForFolder
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BROWSEINFO
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public string pszDisplayName;
        public string lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    [Flags]
    public enum ExecutionState : uint
    {
        ES_SYSTEM_REQUIRED = 0x00000001,
        ES_CONTINUOUS = 0x80000000
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO lpbi);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SHGetPathFromIDList(IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszPath);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr ptr);

    // Флаги для BROWSEINFO
    private const uint BIF_RETURNONLYFSDIRS = 0x00000001; // Только папки файловой системы
    private const uint BIF_NEWDIALOGSTYLE = 0x00000040;   // Новый стиль диалога (создание папок)

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetOpenFileName(ref OpenFileName ofn);

    // Поле для хранения окна лога
    private LogWindow _logWindow;
    private void btnShowConsole_Click(object sender, RoutedEventArgs e)
    {
        if (_logWindow == null)
        {
            _logWindow = new LogWindow();
            _logWindow.AppendLog(_fullLog.ToString());  // Передаем накопленный лог в новое окно
            _logWindow.Closed += (s, args) => _logWindow = null; // Сбрасываем ссылку при закрытии
        }
        _logWindow.Activate();
    }

    // Вспомогательный метод для записи лога
    private void Log(string message)
    {
        // Пишем в отладочную консоль Visual Studio
        System.Diagnostics.Debug.WriteLine(message);
        _fullLog.AppendLine(message); // Копим лог в переменную класса

        // Пишем в наше окно, если оно открыто
        _logWindow?.AppendLog(message);
    }

    public MainWindow()
    {
        // Регистрируем провайдер кодировок для поддержки CP866
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        this.InitializeComponent();
        IntPtr hWnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

        // Задаем ширину и высоту (в логических пикселях)
        appWindow.ResizeClient(new SizeInt32(700, 500));

        // Установка иконки заголовка
        appWindow.SetIcon("itFlashka.ico");

        // Установка темы заголовка
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            appWindow.TitleBar.PreferredTheme = TitleBarTheme.Dark;
        }

        _logWindow = new LogWindow(); // Создаем сразу, чтобы лог писался
        _logWindow.Closed += (s, args) => _logWindow = null;
        this.Closed += (s, e) => Environment.Exit(0);
    }

    // Вспомогательный метод для вызова диалога
    private string ShowOpenFileDialog(string filter, string title)
    {
        var ofn = new OpenFileName();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        // Указываем владельца (окно приложения), чтобы диалог был модальным
        ofn.hwndOwner = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ofn.lpstrFilter = filter;
        ofn.lpstrFile = new string(new char[256]);
        ofn.nMaxFile = ofn.lpstrFile.Length;
        ofn.lpstrFileTitle = new string(new char[64]);
        ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
        ofn.lpstrTitle = title;
        ofn.Flags = 0x00080000 | 0x00001000 | 0x00000800; // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST

        if (GetOpenFileName(ref ofn))
            return ofn.lpstrFile;
        return null;
    }

    private string ShowFolderDialog(string title)
    {
        var bi = new BROWSEINFO
        {
            hwndOwner = WinRT.Interop.WindowNative.GetWindowHandle(this),
            lpszTitle = title,
            ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE
        };

        IntPtr pidl = SHBrowseForFolder(ref bi);
        if (pidl != IntPtr.Zero)
        {
            var path = new System.Text.StringBuilder(260);
            bool success = SHGetPathFromIDList(pidl, path);
            CoTaskMemFree(pidl);

            if (success)
                return path.ToString();
        }
        return null;
    }

    private async void btnBrowseIso_Click(object sender, RoutedEventArgs e)
    {
        string path = ShowOpenFileDialog("ISO files (*.iso)\0*.iso\0All files (*.*)\0*.*\0", "Выберите ISO");
        if (path != null)
        {
            txtIsoPath.Text = path;
            txtOutputName.Text = $"Win11_Slim.iso";
            // await LoadEditionsAsync(path);
        }
    }

    private async void btnAction_Click(object sender, RoutedEventArgs e)
    {
        if (pnlStep2.Visibility == Visibility.Collapsed)
        {
            if (string.IsNullOrWhiteSpace(txtIsoPath.Text) || string.IsNullOrWhiteSpace(txtWorkDir.Text))
            {
                await ShowDialogAsync("Пожалуйста, выберите ISO и рабочую папку.");
                return;
            }

            btnAction.IsEnabled = false;
            btnAction.Content = "Загрузка...";
            SetThreadExecutionState(ExecutionState.ES_CONTINUOUS | ExecutionState.ES_SYSTEM_REQUIRED);

            try
            {
                // 1. Установка ADK
                bool adkReady = await AdkInstaller.EnsureAdkAsync(
                    msg => DispatcherQueue.TryEnqueue(() => lblStatus.Text = msg));
                if (!adkReady)
                {
                    await ShowDialogAsync("Не удалось установить ADK.");
                    btnAction.Content = "Далее";
                    return;
                }

                // 2. Загрузка редакций
                await UpdateProgressAsync(0, "Загрузка списка редакций Windows...");
                await LoadEditionsFromIsoAsync(txtIsoPath.Text, txtWorkDir.Text);
                await UpdateProgressAsync(15, "Выберите настройки и нажмите Создать");

                // 3. Переключение интерфейса
                pnlStep1.Visibility = Visibility.Collapsed; // Скрываем первые два поля!
                pnlStep2.Visibility = Visibility.Visible;   // Показываем остальные
                btnAction.Content = "Создать";              // Меняем текст кнопки
            }
            catch (Exception ex)
            {
                await ShowDialogAsync($"Ошибка: {ex.Message}");
                btnAction.Content = "Далее";
            }
            finally
            {
                SetThreadExecutionState(ExecutionState.ES_CONTINUOUS);
                btnAction.IsEnabled = true;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(txtOutputName.Text))
            {
                await ShowDialogAsync("Укажите имя для готового ISO.");
                return;
            }
            // Показываем кнопку консоли перед стартом
            btnShowConsole.Visibility = Visibility.Visible;

            await RunCompressionAsync();
        }
    }

    private async void btnBrowseXml_Click(object sender, RoutedEventArgs e)
    {
        string path = ShowOpenFileDialog("XML files (*.xml)\0*.xml\0All files (*.*)\0*.*\0", "Выберите unattended.xml");
        if (path != null)
        {
            txtXmlPath.Text = path;
        }
    }

    private void btnBrowseWorkDir_Click(object sender, RoutedEventArgs e)
    {
        string path = ShowFolderDialog("Выберите рабочую папку");
        if (path != null)
        {
            txtWorkDir.Text = path;
        }
    }

    private async Task LoadEditionsFromIsoAsync(string isoPath, string workDir)
    {
        // 1. Очищаем рабочую папку и создаем заново
        if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
        Directory.CreateDirectory(workDir);

        // 2. Распаковка ISO (используем ваш ExtractIsoAsync)
        await UpdateProgressAsync(0, "Распаковка ISO...");
        bool extracted = await ExtractIsoAsync(isoPath, workDir);
        if (!extracted) throw new Exception("Не удалось распаковать ISO.");

        await UpdateProgressAsync(10, "Поиск файла образа...");
        string wimPath = Path.Combine(workDir, "sources", "install.wim");
        string esdPath = Path.Combine(workDir, "sources", "install.esd");
        string imagePath = File.Exists(wimPath) ? wimPath : (File.Exists(esdPath) ? esdPath : null);
        if (imagePath == null) throw new Exception("В ISO не найден install.wim или install.esd.");

        await UpdateProgressAsync(15, "Чтение списка редакций...");

        // 3. Вызов DISM для чтения редакций
        var dismPsi = new ProcessStartInfo
        {
            FileName = "dism.exe",
            Arguments = $"/Get-WimInfo /WimFile:\"{imagePath}\" /English",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.GetEncoding(866)
        };

        using var dismProc = Process.Start(dismPsi);
        string output = await dismProc.StandardOutput.ReadToEndAsync();
        await dismProc.WaitForExitAsync();

        if (dismProc.ExitCode != 0) throw new Exception("DISM не смог прочитать информацию об образе.");

        // 4. Парсинг
        cmbEdition.Items.Clear();
        var lines = output.Split('\n');
        int currentIndex = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var indexMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^Index\s*:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (indexMatch.Success) { currentIndex = int.Parse(indexMatch.Groups[1].Value); continue; }

            var nameMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^Name\s*:\s*(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (nameMatch.Success && currentIndex > 0)
            {
                cmbEdition.Items.Add($"{currentIndex} — {nameMatch.Groups[1].Value.Trim()}");
            }
        }

        if (cmbEdition.Items.Count == 0) throw new Exception("Не удалось найти редакции.");
        cmbEdition.SelectedIndex = 0;
        await UpdateProgressAsync(0, "Список редакций загружен");
    }

    private async Task<bool> RunDismProcessAsync(string args, int startProgress, int endProgress)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dism.exe",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.GetEncoding(866),
            StandardErrorEncoding = Encoding.GetEncoding(866)
        };

        using var proc = Process.Start(psi)!;

        // Читаем оба потока, но прогресс двигаем ТОЛЬКО из stdout.
        var readOut = ReadDismStreamAsync(proc.StandardOutput, startProgress, endProgress, updateProgress: true);
        var readErr = ReadDismStreamAsync(proc.StandardError, startProgress, endProgress, updateProgress: false);

        await Task.WhenAll(readOut, readErr);
        await proc.WaitForExitAsync();
        return proc.ExitCode == 0;
    }

    private async Task ReadDismStreamAsync(StreamReader reader, int startProgress, int endProgress, bool updateProgress)
    {
        var buffer = new StringBuilder();
        var chunk = new char[1024];

        while (true)
        {
            int n = await reader.ReadAsync(chunk, 0, chunk.Length);
            if (n == 0) break;

            for (int i = 0; i < n; i++)
            {
                char c = chunk[i];

                // DISM завершает прогресс-строку через \r, обычные — через \n
                if (c == '\r' || c == '\n')
                {
                    if (buffer.Length > 0)
                    {
                        ProcessDismLine(buffer.ToString(), startProgress, endProgress, updateProgress);
                        buffer.Clear();
                    }
                    continue;
                }

                buffer.Append(c);
            }
        }

        if (buffer.Length > 0)
            ProcessDismLine(buffer.ToString(), startProgress, endProgress, updateProgress);
    }

    private void ProcessDismLine(string line, int startProgress, int endProgress, bool updateProgress)
    {
        Log(line);   // в консоль/лог-окно — как было

        if (!updateProgress) return;

        // Ищем ВСЕ проценты в строке и берём последний (DISM может писать "30.0% -> 31.2%")
        var matches = System.Text.RegularExpressions.Regex.Matches(line, @"(\d+(?:[.,]\d+)?)\s*%");
        if (matches.Count == 0) return;

        string last = matches[^1].Groups[1].Value.Replace(',', '.');
        if (!double.TryParse(last, System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out double percent))
            return;

        if (percent < 0 || percent > 100) return;

        int overall = startProgress + (int)(percent / 100.0 * (endProgress - startProgress));

        // ВАЖНО: очередь у элемента progressBar, а не у Window
        var dq = progressBar.DispatcherQueue;
        dq.TryEnqueue(() =>
        {
            // защита от «отката» назад
            if (overall > progressBar.Value)
            {
                progressBar.Value = overall;
                lblProgressPercent.Text = $"{overall}%";
                lblStatus.Text = $"Экспорт редакции с ESD-сжатием ({percent:0.0}%)...";
            }
        });
    }

    private async Task<bool> BuildIsoAsync(string workDir, string outputIsoPath)
    {
        string oscdimg = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Windows Kits", "10", "Assessment and Deployment Kit",
            "Deployment Tools", "amd64", "Oscdimg", "oscdimg.exe");

        if (!File.Exists(oscdimg))
        {
            await ShowDialogAsync("oscdimg.exe не найден. Убедитесь, что ADK установлен корректно.");
            return false;
        }

        string etfsboot = Path.Combine(workDir, "boot", "etfsboot.com");
        string efisys = Path.Combine(workDir, "efi", "microsoft", "boot", "efisys.bin");

        if (!File.Exists(etfsboot) || !File.Exists(efisys))
        {
            await ShowDialogAsync("В папке не найдены загрузочные файлы (etfsboot.com или efisys.bin).");
            return false;
        }

        string args = $"-m -o -u2 -udfver102 " +
                      $"-bootdata:2#p0,e,b\"{etfsboot}\"#pEF,e,b\"{efisys}\" " +
                      $"\"{workDir}\" \"{outputIsoPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = oscdimg,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Добавляем кодировку, чтобы русские буквы не ломались (если есть)
            StandardOutputEncoding = Encoding.GetEncoding(866),
            StandardErrorEncoding = Encoding.GetEncoding(866)
        };

        using var proc = Process.Start(psi)!;

        // Читаем оба потока. Прогресс обычно идет в stdout.
        var readOut = ReadOscdimgStreamAsync(proc.StandardOutput, 92, 100);
        var readErr = ReadOscdimgStreamAsync(proc.StandardError, 92, 100);

        await Task.WhenAll(readOut, readErr);
        await proc.WaitForExitAsync();

        bool ok = proc.ExitCode == 0 && File.Exists(outputIsoPath);

        if (ok)
        {
            var dq = progressBar.DispatcherQueue;
            dq?.TryEnqueue(() =>
            {
                progressBar.Value = 100;
                lblProgressPercent.Text = "100%";
                lblStatus.Text = "Сборка ISO завершена (100%)";
            });
        }
        return ok;
    }

    private static long GetDirectorySize(string dir)
    {
        long total = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(f).Length; } catch { /* пропускаем недоступные */ }
            }
        }
        catch { }
        return total;
    }

    private async Task ReadOscdimgStreamAsync(StreamReader reader, int startProgress, int endProgress)
    {
        var buffer = new StringBuilder();
        var chunk = new char[1024];

        while (true)
        {
            int n = await reader.ReadAsync(chunk, 0, chunk.Length);
            if (n == 0) break;

            for (int i = 0; i < n; i++)
            {
                char c = chunk[i];

                // oscdimg обновляет строку через \r
                if (c == '\r' || c == '\n')
                {
                    if (buffer.Length > 0)
                    {
                        ProcessOscdimgLine(buffer.ToString(), startProgress, endProgress);
                        buffer.Clear();
                    }
                    continue;
                }

                buffer.Append(c);
            }
        }

        if (buffer.Length > 0)
            ProcessOscdimgLine(buffer.ToString(), startProgress, endProgress);
    }

    private void ProcessOscdimgLine(string line, int startProgress, int endProgress)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        // Пишем в лог, чтобы видеть в консоли
        Log(line);

        // Ищем проценты. Строка выглядит как "97% complete" или " 45% complete"
        var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)%\s*complete", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int percent))
        {
            if (percent < 0 || percent > 100) return;

            // Пересчитываем локальные 0-100% oscdimg в общий диапазон (92-100%)
            int overall = startProgress + (int)(percent / 100.0 * (endProgress - startProgress));

            var dq = progressBar.DispatcherQueue;
            dq?.TryEnqueue(() =>
            {
                if (overall > progressBar.Value)
                {
                    progressBar.Value = overall;
                    lblProgressPercent.Text = $"{overall}%";
                    lblStatus.Text = $"Сборка ISO (oscdimg, {percent}%)...";
                }
            });
        }
    }

    private async Task<bool> ExtractIsoAsync(string isoPath, string workDir)
    {
        // Ищем 7z.exe в стандартных местах
        string sevenZipPath = @"C:\Program Files\7-Zip\7z.exe";
        if (!File.Exists(sevenZipPath))
            sevenZipPath = @"C:\Program Files (x86)\7-Zip\7z.exe";

        if (!File.Exists(sevenZipPath))
        {
            await ShowDialogAsync("7-Zip не найден. Пожалуйста, установите его с сайта 7-zip.org.");
            return false;
        }

        var psi = new ProcessStartInfo
        {
            FileName = sevenZipPath,
            Arguments = $"x \"{isoPath}\" -o\"{workDir}\" -y -bsp1",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.GetEncoding(866), // Обязательно!
            StandardErrorEncoding = Encoding.GetEncoding(866)   // Обязательно!
        };

        using var proc = Process.Start(psi);
        // ВАЖНО: читаем ОБА потока параллельно и ЖДЁМ их завершения
        var readOut = Read7zProgressAsync(proc.StandardOutput, 0, 15, "Распаковка ISO");
        var readErr = proc.StandardError.ReadToEndAsync();

        await Task.WhenAll(readOut, readErr);
        await proc.WaitForExitAsync();

        string error = await readErr;
        if (!string.IsNullOrEmpty(error)) Log(error);

        return proc.ExitCode == 0;
    }

    private async Task Read7zProgressAsync(StreamReader reader, int startProgress, int endProgress, string stageName)
    {
        var buffer = new StringBuilder();
        var chunk = new char[1024];

        while (true)
        {
            int n = await reader.ReadAsync(chunk, 0, chunk.Length);
            if (n == 0) break;

            for (int i = 0; i < n; i++)
            {
                char c = chunk[i];

                // 7-Zip завершает строки как через \n, так и через \r (для прогресс-бара)
                if (c == '\r' || c == '\n')
                {
                    if (buffer.Length > 0)
                    {
                        Process7zLine(buffer.ToString(), startProgress, endProgress, stageName);
                        buffer.Clear();
                    }
                    continue;
                }

                buffer.Append(c);
            }
        }

        if (buffer.Length > 0)
            Process7zLine(buffer.ToString(), startProgress, endProgress, stageName);
    }

    private void Process7zLine(string line, int startProgress, int endProgress, string stageName)
    {
        // 1. Отправляем строку в лог (окно "Консоль" и Debug)
        // Игнорируем пустые строки и мусор
        if (!string.IsNullOrWhiteSpace(line))
        {
            Log(line);
        }

        // 2. Парсим проценты и обновляем прогресс-бар
        // 7-Zip выводит прогресс в формате " 45%" или " 45% 12 - file.txt"
        var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)%");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int percent))
        {
            if (percent < 0 || percent > 100) return;

            int overall = startProgress + (int)(percent / 100.0 * (endProgress - startProgress));

            var dq = progressBar.DispatcherQueue;
            dq?.TryEnqueue(() =>
            {
                if (overall > progressBar.Value) // защита от отката
                {
                    progressBar.Value = overall;
                    lblProgressPercent.Text = $"{overall}%";
                    lblStatus.Text = $"{stageName} ({percent}%)";
                }
            });
        }
    }

    private async Task RunCompressionAsync()
    {
        string workDir = txtWorkDir.Text;
        string isoPath = txtIsoPath.Text;
        string outputName = txtOutputName.Text;
        string compression = (cmbCompression.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "recovery";
        int index = cmbEdition.SelectedIndex + 1;

        // ЭТАП 2: Поиск образа (20-25%)
        await UpdateProgressAsync(20, "Поиск файла install.wim...");
        string wimPath = Path.Combine(workDir, "sources", "install.wim");
        string esdPath = Path.Combine(workDir, "sources", "install.esd");

        if (!File.Exists(wimPath) && !File.Exists(esdPath))
        {
            await ShowDialogAsync("В ISO не найден sources\\install.wim или install.esd.");
            return;
        }
        // Если там уже ESD, DISM не сможет сделать экспорт, но для теста предположим, что там WIM
        string sourceImage = File.Exists(wimPath) ? wimPath : esdPath;

        // ЭТАП 3: Экспорт с ESD-сжатием (25-85%)
        await UpdateProgressAsync(30, "Экспорт редакции с ESD-сжатием...");

        var dismArgs = $"/Export-Image /SourceImageFile:\"{sourceImage}\" /SourceIndex:{index} " +
                       $"/DestinationImageFile:\"{Path.Combine(workDir, "sources", "install_new.esd")}\" " +
                       $"/Compress:{compression} /English";

        bool exportSuccess = await RunDismProcessAsync(dismArgs, 30, 85);
        if (!exportSuccess)
        {
            await ShowDialogAsync("Ошибка при экспорте образа через DISM. Проверьте права администратора.");
            return;
        }

        // Заменяем старый файл на новый ESD
        if (File.Exists(sourceImage)) File.Delete(sourceImage);
        File.Move(Path.Combine(workDir, "sources", "install_new.esd"), Path.Combine(workDir, "sources", "install.esd"));

        await UpdateProgressAsync(85, "Экспорт завершён");

        // ЭТАП 4: Копирование unattended.xml (85-90%)
        await UpdateProgressAsync(88, "Копирование unattended.xml...");
        if (!string.IsNullOrWhiteSpace(txtXmlPath.Text))
        {
            File.Copy(txtXmlPath.Text, Path.Combine(workDir, "autounattend.xml"), true);
        }

        // ЭТАП 5: Сборка ISO (90-100%)
        await UpdateProgressAsync(92, "Сборка ISO (oscdimg)...");
        string outputIsoPath = Path.Combine(Directory.GetParent(workDir)?.FullName ?? workDir, outputName);
        bool isoBuilt = await BuildIsoAsync(workDir, outputIsoPath);

        if (isoBuilt)
        {
            await UpdateProgressAsync(100, "Готово!");
            await ShowDialogAsync($"ISO успешно создан:\n{outputIsoPath}");
        }
        else
        {
            await ShowDialogAsync("Ошибка при сборке ISO. Проверьте наличие oscdimg.exe.");
        }
    }

    private Task UpdateProgressAsync(int percent, string status)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            progressBar.Value = percent;
            lblProgressPercent.Text = $"{percent}%";
            lblStatus.Text = status;
        });
        return Task.CompletedTask;
    }

    private async Task RunProcessAsync(string exe, string args, Func<int, string, Task> onOutput)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        string? line;
        while ((line = await proc.StandardOutput.ReadLineAsync()) != null)
        {
            // Парсинг процента из вывода DISM, если есть
            if (line.Contains('%'))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+\.?\d*)%");
                if (match.Success)
                    await onOutput((int)double.Parse(match.Groups[1].Value), line);
            }
            // Логирование можно добавить в файл или в UI
        }
        await proc.WaitForExitAsync();
    }

    private async Task ShowDialogAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Внимание",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}