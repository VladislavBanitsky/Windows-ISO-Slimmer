using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;

namespace IsoSlimmer
{
    public class LogWindow : Window
    {
        private TextBox _txtLog;

        public LogWindow()
        {
            // Настраиваем заголовок окна
            this.Title = "Консоль";

            // Создаем ScrollViewer для прокрутки
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10)
            };

            // Создаем TextBox
            _txtLog = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                BorderThickness = new Thickness(0), // Убираем рамку для красоты
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };

            scrollViewer.Content = _txtLog;
            this.Content = scrollViewer; // Устанавливаем контент напрямую в Window
        }

        public void AppendLog(string text)
        {
            // Обновляем UI в главном потоке
            DispatcherQueue.TryEnqueue(() =>
            {
                _txtLog.Text += text + Environment.NewLine;
                // Прокручиваем вниз
                _txtLog.Select(_txtLog.Text.Length, 0);
            });
        }
    }
}