using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Media;
using NLog;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace DCSBIOSBridge.misc
{
    public static class Common
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static readonly Encoding UsedEncoding = Encoding.GetEncoding(28591);


        public static List<string> GetSerialPortNames()
        {
            /*
             * Sometimes when disconnecting cables this can return same port listed 3 times.
             */
            return SerialPort.GetPortNames().Distinct().ToList();
        }

        public static Microsoft.Win32.OpenFileDialog OpenFileDialog(string initialDirectory)
        {
            return new Microsoft.Win32.OpenFileDialog
            {
                RestoreDirectory = true,
                InitialDirectory = Path.GetDirectoryName(initialDirectory),
                FileName = Constants.WildCardProfileSearch,
                DefaultExt = Constants.ProfileExtension,
                Filter = Constants.ProfileFilter
            };
        }

        public static Microsoft.Win32.SaveFileDialog SaveProfileDialog(string lastDirectory)
        {
            return new Microsoft.Win32.SaveFileDialog
            {
                RestoreDirectory = true,
                InitialDirectory = string.IsNullOrEmpty(lastDirectory) ? MyDocumentsPath() : lastDirectory,
                FileName = Constants.WildCardProfileSearch,
                DefaultExt = Constants.ProfileExtension,
                Filter = Constants.ProfileFilter,
                OverwritePrompt = true
            };
        }

        public static string DefaultFile()
        {
            return Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)) + "\\" + Constants.DefaultProfileName;
        }

        public static string MyDocumentsPath()
        {
            return Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        }

        internal static void TryOpenLogFileWithTarget(string targetName)
        {
            try
            {
                var errorLogFilePath = GetLogFilePathByTarget(targetName);
                if (errorLogFilePath == null || !File.Exists(errorLogFilePath))
                {
                    MessageBox.Show($"No log file found {errorLogFilePath}", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = errorLogFilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowErrorMessageBox(ex);
            }
        }

        /// <summary>
        /// Try to find the path of the log with a file target given as parameter
        /// See NLog.config in the main folder of the application for configured log targets
        /// </summary>
        private static string GetLogFilePathByTarget(string targetName)
        {
            string fileName;
            if (LogManager.Configuration != null && LogManager.Configuration.ConfiguredNamedTargets.Count != 0)
            {
                var target = LogManager.Configuration.FindTargetByName(targetName);
                if (target == null)
                {
                    throw new Exception($"Could not find log with a target named: [{targetName}]. See NLog.config for configured targets");
                }

                FileTarget fileTarget;

                // Unwrap the target if necessary.
                if (target is not WrapperTargetBase wrapperTarget)
                {
                    fileTarget = target as FileTarget;
                }
                else
                {
                    fileTarget = wrapperTarget.WrappedTarget as FileTarget;
                }

                if (fileTarget == null)
                {
                    throw new Exception($"Could not get a FileTarget type log from {target.GetType()}");
                }

                var logEventInfo = new LogEventInfo { TimeStamp = DateTime.Now };
                fileName = fileTarget.FileName.Render(logEventInfo);
            }
            else
            {
                throw new Exception("LogManager contains no configuration or there are no named targets. See NLog.config file to configure the logs.");
            }
            return fileName;
        }

        public static void ShowMessageBox(string message)
        {
            MessageBox.Show(message, "", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void ShowErrorMessageBox(Exception ex, string message = null)
        {
            Logger.Error(ex, message);
            MessageBox.Show((!string.IsNullOrEmpty(message) ? $"{message}\n\n{ex.Message}" : ex.Message), $"Details logged to error log.{Environment.NewLine}{ex.Source}", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                var count = 0;
                Application.Current.Dispatcher.Invoke(new Action(() => count = VisualTreeHelper.GetChildrenCount(depObj)));
                for (var i = 0; i < count; i++)
                {
                    DependencyObject child = null;
                    Application.Current.Dispatcher.Invoke(new Action(() => child = VisualTreeHelper.GetChild(depObj, i)));
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (var childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }
}
