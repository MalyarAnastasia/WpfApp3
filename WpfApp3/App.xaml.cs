using System;
using System.Windows;

namespace WpfApp3
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = $"An unhandled exception occurred:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}";
            MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // Отмечаем исключение как обработанное, чтобы приложение не завершалось (по крайней мере, сразу)
        }


        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            string errorMessage = $"An unhandled exception occurred in CurrentDomain:\n\n{ex.Message}\n\n{ex.StackTrace}";
            MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        }
    }
}