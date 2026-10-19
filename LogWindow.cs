using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Text;
using Windows.Graphics;
using WinRT.Interop;

namespace IsoSlimmer
{
    public class LogWindow : Window
    {
        private TextBox _txtLog;
        private StringBuilder _buffer = new();
        private DispatcherTimer _timer;

        public LogWindow()
        {
            this.Title = "Консоль";
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            // Задаем ширину и высоту (в логических пикселях)
            appWindow.ResizeClient(new SizeInt32(700, 500));

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10)
            };

            _txtLog = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };

            scrollViewer.Content = _txtLog;
            this.Content = scrollViewer;

            // Таймер для батчинга: обновляем UI раз в 100 мс (10 раз в секунду)
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += (s, e) => FlushBuffer();
            _timer.Start();

            // Останавливаем таймер при закрытии окна
            this.Closed += (s, e) => _timer.Stop();
        }

        public void AppendLog(string text)
        {
            // Просто копим в буфере. Этот метод вызывается из фоновых потоков,
            // поэтому используем lock для потокобезопасности.
            lock (_buffer)
            {
                _buffer.AppendLine(text);
            }
        }

        private void FlushBuffer()
        {
            string textToAppend;

            lock (_buffer)
            {
                if (_buffer.Length == 0) return; // нечего обновлять
                textToAppend = _buffer.ToString();
                _buffer.Clear();
            }

            // Дописываем всё разом
            _txtLog.Text += textToAppend;

            // Прокручиваем вниз. Используем более мягкий метод для TextBox
            _txtLog.Select(_txtLog.Text.Length, 0);
        }
    }
}