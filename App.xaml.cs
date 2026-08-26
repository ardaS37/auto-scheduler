using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AutoScheduler
{
    /// <summary>
    /// App.xaml etkileşim mantığı
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);

            MessageBox.Show(
                "Beklenmeyen bir hata oluştu ve işlem tamamlanamadı:\n\n" + e.Exception.Message,
                "Hata",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException(e.Exception);
            e.SetObserved();
        }

        private static void LogException(Exception ex)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoScheduler");
                Directory.CreateDirectory(dir);
                var logPath = Path.Combine(dir, "hata_gunlugu.txt");
                File.AppendAllText(logPath, "\n===== " + DateTime.Now + " =====\n" + ex + "\n");
            }
            catch
            {
                // Günlük dosyasına yazılamıyorsa yok say; kullanıcıya zaten hata mesajı gösteriliyor.
            }
        }
    }
}
