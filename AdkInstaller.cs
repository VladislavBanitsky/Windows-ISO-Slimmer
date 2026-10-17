using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace IsoSlimmer
{
    public static class AdkInstaller
    {
        // Официальная ссылка Microsoft на загрузку установщика ADK
        private const string AdkUrl = "https://go.microsoft.com/fwlink/?linkid=2289980";

        private static readonly string InstallerPath = Path.Combine(
            Path.GetTempPath(), "adksetup.exe");

        private static readonly string OscdimgPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Windows Kits", "10", "Assessment and Deployment Kit",
            "Deployment Tools", "amd64", "Oscdimg", "oscdimg.exe");

        public static bool IsAdkInstalled()
        {
            return File.Exists(OscdimgPath);
        }

        public static async Task<bool> EnsureAdkAsync(
            Action<string> progressCallback = null)
        {
            if (IsAdkInstalled())
            {
                progressCallback?.Invoke("ADK Deployment Tools уже установлены");
                return true;
            }

            // 1. Загрузка установщика
            progressCallback?.Invoke("Загрузка установщика Windows ADK...");
            using var http = new HttpClient();
            var data = await http.GetByteArrayAsync(AdkUrl);
            await File.WriteAllBytesAsync(InstallerPath, data);

            // 2. Тихая установка только Deployment Tools
            progressCallback?.Invoke("Установка Deployment Tools (может занять несколько минут)...");
            var psi = new ProcessStartInfo
            {
                FileName = InstallerPath,
                Arguments = "/quiet /features OptionId.DeploymentTools /norestart",
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = "runas" // требует прав администратора
            };

            using var proc = Process.Start(psi);
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                progressCallback?.Invoke($"Ошибка установки, код выхода: {proc.ExitCode}");
                return false;
            }

            // 3. Проверка
            if (IsAdkInstalled())
            {
                progressCallback?.Invoke("ADK Deployment Tools успешно установлены");
                return true;
            }

            progressCallback?.Invoke("Установка завершена, но oscdimg.exe не найден");
            return false;
        }
    }
}