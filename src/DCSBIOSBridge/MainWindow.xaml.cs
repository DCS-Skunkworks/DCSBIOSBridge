using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Windows;
using DCSBIOSBridge.Properties;
using DCS_BIOS;
using DCS_BIOS.EventArgs;
using DCS_BIOS.Interfaces;
using DCSBIOSBridge.Events;
using DCSBIOSBridge.Interfaces;
using DCSBIOSBridge.misc;
using DCSBIOSBridge.SerialPortClasses;
using DCSBIOSBridge.UserControls;
using DCSBIOSBridge.Windows;
using NLog;
using System.Reflection;
using System.Windows.Input;
using Octokit;
using System.Windows.Navigation;
using DCSBIOSBridge.Events.Args;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace DCSBIOSBridge
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : IDcsBiosConnectionListener,
        ISerialPortStatusListener, ISettingsDirtyListener, ISerialPortUserControlListener, IWindowsSerialPortEventListener, IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private List<string> _lastSerialPortsWhenChecking = new();
        private readonly object _lockObject = new();
        private const string WindowName = "DCS-BIOS Bridge ";
        private DCSBIOS _dcsBios;
        private bool _formLoaded;
        private bool _isDirty;
        private readonly SerialPortsProfileHandler _profileHandler = new();
        private readonly SerialPortService _serialPortService = new();
        private List<SerialPortUserControl> _serialPortUserControls = new();
        
        public MainWindow()
        {
            InitializeComponent();
            DBEventManager.AttachSerialPortStatusListener(this);
            DBEventManager.AttachWindowsSerialPortEventListener(this);
            DBEventManager.AttachSerialPortUserControlListener(this);
            DBEventManager.AttachSettingsDirtyListener(this);
            BIOSEventHandler.AttachConnectionListener(this);
        }

        #region IDisposable Support
        private bool _hasBeenCalledAlready; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (_hasBeenCalledAlready) return;

            if (disposing)
            {
                //  dispose managed state (managed objects).
                _dcsBios?.Shutdown();
                _dcsBios?.Dispose();

                DBEventManager.BroadCastSerialPortUserControlStatus(SerialPortUserControlStatus.DoDispose);
                _serialPortService.CleanUp();
                DBEventManager.DetachSerialPortStatusListener(this);
                DBEventManager.DetachWindowsSerialPortEventListener(this);
                DBEventManager.DetachSerialPortUserControlListener(this);
                DBEventManager.DetachSettingsDirtyListener(this);
                BIOSEventHandler.DetachConnectionListener(this);
            }

            //  free unmanaged resources (unmanaged objects) and override a finalizer below.

            //  set large fields to null.
            _hasBeenCalledAlready = true;
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);

            //  uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion


        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_formLoaded) return;

                Top = Settings.Default.MainWindowTop;
                Left = Settings.Default.MainWindowLeft;
                Height = Settings.Default.MainWindowHeight;
                Width = Settings.Default.MainWindowWidth;

                _lastSerialPortsWhenChecking = SerialPort.GetPortNames().ToList();

                CreateDCSBIOS();

                CheckForNewRelease();

                LoadPorts();

                SetWindowState();
                _formLoaded = true;
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        public void DcsBiosConnectionActive(object sender, DCSBIOSConnectionEventArgs e)
        {
            try
            {
                Dispatcher?.BeginInvoke((Action)(() => ControlSpinningWheel.RotateGear()));
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        public void OnSerialPortStatusChanged(SerialPortStatusEventArgs e)
        {
            try
            {
                switch (e.SerialPortStatus)
                {
                    case SerialPortStatus.Settings:
                        {
                            Dispatcher.Invoke(SetWindowState);
                            break;
                        }
                    case SerialPortStatus.Open:
                        break;
                    case SerialPortStatus.Close:
                        break;
                    case SerialPortStatus.Opened:
                        break;
                    case SerialPortStatus.Closed:
                        break;
                    case SerialPortStatus.Added:
                    case SerialPortStatus.Hidden:
                        {
                            break;
                        }
                    case SerialPortStatus.None:
                        break;
                    case SerialPortStatus.Ok:
                        break;
                    case SerialPortStatus.Error:
                        break;
                    case SerialPortStatus.Critical:
                        break;
                    case SerialPortStatus.IOError:
                        break;
                    case SerialPortStatus.TimeOutError:
                        break;
                    case SerialPortStatus.BytesWritten:
                        break;
                    case SerialPortStatus.BytesRead:
                        break;
                    case SerialPortStatus.DCSBIOSCommandCalled:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(e.SerialPortStatus.ToString());
                }
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        public void OnSerialPortUserControlStatusChanged(SerialPortUserControlArgs args)
        {
            try
            {
                switch (args.Status)
                {
                    case SerialPortUserControlStatus.Created:
                        {
                            Dispatcher.Invoke(() => AddUserControlToUI(args.SerialPortUserControl));
                            break;
                        }
                    case SerialPortUserControlStatus.Hidden:
                        {
                            Dispatcher.Invoke(() => RemoveUserControlFromUI(args.SerialPortUserControl));
                            break;
                        }
                    case SerialPortUserControlStatus.Closed:
                        {
                            Dispatcher.Invoke(() => RemoveUserControlFromUI(args.SerialPortUserControl));
                            break;
                        }
                    case SerialPortUserControlStatus.Check:
                    case SerialPortUserControlStatus.DisposeDisabledPorts:
                    case SerialPortUserControlStatus.DoDispose:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(args.Status.ToString());
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => Common.ShowErrorMessageBox(ex));
            }
        }

        public void PortsChangedEvent(object sender, PortsChangedArgs e)
        {
            try
            {
                var thread = new Thread(CheckComPortExistenceStatus);
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void LoadPorts()
        {
            if (!string.IsNullOrEmpty(Settings.Default.LastProfileFileUsed))
            {
                _profileHandler.LoadProfile(Settings.Default.LastProfileFileUsed);
                if (_profileHandler.SerialPortSettings.Count == 0)
                {
                    ListAllSerialPorts();
                }
                else
                {
                    SerialPortUserControl.LoadSerialPorts(_profileHandler.SerialPortSettings);
                }
            }
            else
            {
                ListAllSerialPorts();
            }
        }

        private void CheckComPortExistenceStatus()
        {
            try
            {
                lock (_lockObject)
                {
                    // make all SerialPortUserControl check whether their SerialPort is OK
                    // if new profile then don't send list, affects whether to remove or grey them out when removed from computer
                    DBEventManager.BroadCastSerialPortUserControlStatus(SerialPortUserControlStatus.Check, null, null, _profileHandler.IsNewProfile ? null : _profileHandler.SerialPortSettings);

                    var currentPorts = SerialPort.GetPortNames();

                    if (currentPorts.Length < _lastSerialPortsWhenChecking.Count)
                    {
                        //No port added
                        _lastSerialPortsWhenChecking = SerialPort.GetPortNames().ToList();
                        return;
                    }

                    foreach (var currentPort in currentPorts)
                    {
                        if (_lastSerialPortsWhenChecking.Any(o => o == currentPort) == false)
                        {
                            Dispatcher.Invoke(() => AddSerialPort(currentPort));
                        }
                    }

                    _lastSerialPortsWhenChecking = SerialPort.GetPortNames().ToList();
                }
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ListAllSerialPorts()
        {
            _lastSerialPortsWhenChecking = SerialPort.GetPortNames().ToList();
            _profileHandler.ClearHiddenPorts();

            foreach (var port in _lastSerialPortsWhenChecking)
            {
                if (_serialPortUserControls.Any(o => o.Name == port) == false)
                {
                    AddSerialPort(port);
                }
            }

            SetWindowState();
        }

        private void OpenProfile()
        {
            if (!DiscardChanges()) return;

            var openFileDialog = Common.OpenFileDialog(Settings.Default.LastProfileFileUsed);

            if (openFileDialog.ShowDialog() == true)
            {
                DisposeAllUserControls();
                _profileHandler.LoadProfile(openFileDialog.FileName);
                SerialPortUserControl.LoadSerialPorts(_profileHandler.SerialPortSettings);
            }
            SetWindowState();
        }

        private void SaveAsNewProfile()
        {
            var lastDirectory = string.IsNullOrEmpty(Settings.Default.LastProfileFileUsed) ? "" : Path.GetDirectoryName(Settings.Default.LastProfileFileUsed);
            var saveFileDialog = Common.SaveProfileDialog(lastDirectory);
            if (saveFileDialog.ShowDialog() == true)
            {
                _profileHandler.SaveProfileAs(saveFileDialog.FileName);
            }
            SetWindowState();
        }

        private void SaveNewOrExistingProfile()
        {
            if (_profileHandler.IsNewProfile)
            {
                SaveAsNewProfile();
            }
            else
            {
                _profileHandler.SaveProfile();
            }
            SetWindowState();
        }

        private void SetWindowState()
        {
            ButtonImageSave.IsEnabled = _isDirty;
            MenuItemSave.IsEnabled = _isDirty && !_profileHandler.IsNewProfile;
            MenuItemSaveAs.IsEnabled = true;
            MenuItemOpen.IsEnabled = true;
            ButtonImageNotepad.IsEnabled = !_profileHandler.IsNewProfile && !_isDirty;
            SetWindowTitle();
        }

        private void SetWindowTitle()
        {
            Title = _profileHandler.IsNewProfile ? WindowName : WindowName + _profileHandler.FileName;

            if (_isDirty)
            {
                Title += " *";
            }
        }

        private void MenuItemOpenClick(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenProfile();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void MenuItemSave_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                _profileHandler.SaveProfile();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void MenuItemSaveAs_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveAsNewProfile();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void MenuItemExit_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void MenuItemOptions_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new SettingsWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                if (settingsWindow.ShowDialog() == true)
                {
                    CreateDCSBIOS();
                }
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void MenuItemAbout_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var about = new AboutWindow();
                about.ShowDialog();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void AddSerialPort(string serialPortName)
        {
            try
            {
                if (_serialPortUserControls.Count(o => o.Name == serialPortName) > 0) return;

                var serialPortUserControl = new SerialPortUserControl(new SerialPortSetting { ComPort = serialPortName });
                AddUserControlToUI(serialPortUserControl);
                DBEventManager.BroadCastPortStatus(serialPortName, SerialPortStatus.Added, 0, null, serialPortUserControl.SerialPortSetting);
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonNew_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isDirty && MessageBox.Show("Discard unsaved changes to current profile?", "Discard changes?", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }

                DisposeAllUserControls();
                _profileHandler.Reset();
                ListAllSerialPorts();
                SetWindowState();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveNewOrExistingProfile();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonSearchForSerialPorts_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ListAllSerialPorts();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonOpen_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenProfile();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonOpenInEditor_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(_profileHandler.FileName);
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private bool DiscardChanges()
        {
            if (_isDirty && MessageBox.Show("Discard unsaved changes to current profile?", "Discard changes?", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return false;
            }

            return true;
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            try
            {
                Settings.Default.MainWindowTop = Top;
                Settings.Default.MainWindowLeft = Left;
                Settings.Default.MainWindowHeight = Height;
                Settings.Default.MainWindowWidth = Width;
                Settings.Default.Save();

                if (!DiscardChanges())
                {
                    e.Cancel = true;
                    return;
                }

                Dispose(true);
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }


        private void AddUserControlToUI(SerialPortUserControl userControl)
        {
            _serialPortUserControls.Add(userControl);
            _serialPortUserControls = _serialPortUserControls.OrderBy(o => o.Name).ToList();
            ItemsControlPorts.ItemsSource = null;
            ItemsControlPorts.ItemsSource = _serialPortUserControls;
        }

        private void RemoveUserControlFromUI(SerialPortUserControl userControl)
        {
            _serialPortUserControls.Remove(userControl);
            ItemsControlPorts.ItemsSource = null;
            ItemsControlPorts.ItemsSource = _serialPortUserControls;
        }

        private void DisposeAllUserControls()
        {
            try
            {
                DBEventManager.BroadCastSerialPortUserControlStatus(SerialPortUserControlStatus.DoDispose);
                _serialPortUserControls.Clear();
                ItemsControlPorts.ItemsSource = null;
                ItemsControlPorts.ItemsSource = _serialPortUserControls;
                SetWindowState();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void CreateDCSBIOS()
        {
            _dcsBios?.Shutdown();
            _dcsBios?.Dispose();
            _dcsBios = new DCSBIOS(Settings.Default.DCSBiosIPFrom,
                Settings.Default.DCSBiosIPTo,
                int.Parse(Settings.Default.DCSBiosPortFrom),
                int.Parse(Settings.Default.DCSBiosPortTo),
                DcsBiosNotificationMode.PassThrough);

            if (!_dcsBios.HasLastException())
            {
                ControlSpinningWheel.RotateGear(2000);
            }
        }

        private void MenuItemErrorLog_OnClick(object sender, RoutedEventArgs e)
        {
            Common.TryOpenLogFileWithTarget("error_logfile");
        }

        public void OnSettingsDirty(SettingsDirtyEventArgs args)
        {
            try
            {
                _isDirty = args.IsDirty;
                Dispatcher.Invoke(SetWindowState);
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonClosePorts_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                DBEventManager.BroadCastPortStatus(null, SerialPortStatus.Close);
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonOpenPorts_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                DBEventManager.BroadCastPortStatus(null, SerialPortStatus.Open);
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }
        
        private async void CheckForNewRelease()
        {
            // #if !DEBUG
            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            if (string.IsNullOrEmpty(fileVersionInfo.FileVersion)) return;

            var thisVersion = new Version(fileVersionInfo.FileVersion);

            try
            {
                var dateTime = DateTime.Now;// Settings.Default.LastGitHubCheck;

                var client = new GitHubClient(new ProductHeaderValue("DCSBIOSBridge"));
                var timeSpan = DateTime.Now - dateTime;
                if (timeSpan.Days > 1)
                {
                    Settings.Default.LastGitHubCheck = DateTime.Now;
                    Settings.Default.Save();
                    var lastRelease = await client.Repository.Release.GetLatest("DCS-Skunkworks", "DCSBIOSBridge");
                    var githubVersion = new Version(lastRelease.TagName.Replace("v", ""));
                    if (githubVersion.CompareTo(thisVersion) > 0)
                    {
                        Dispatcher?.Invoke(() =>
                        {
                            LabelVersionInformation.Visibility = Visibility.Hidden;
                            LabelDownloadNewVersion.Visibility = Visibility.Visible;
                        });
                    }
                    else
                    {
                        Dispatcher?.Invoke(() =>
                        {
                            LabelVersionInformation.Text = "v." + fileVersionInfo.FileVersion;
                            LabelVersionInformation.Visibility = Visibility.Visible;
                        });
                    }
                }
                else
                {
                    Dispatcher?.Invoke(() =>
                    {
                        LabelVersionInformation.Text = "v." + fileVersionInfo.FileVersion;
                        LabelVersionInformation.Visibility = Visibility.Visible;
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking for newer releases.");
                LabelVersionInformation.Text = "v." + fileVersionInfo.FileVersion;
            }

            // #endif
        }
        
        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void UIElement_OnMouseEnterCursorArrow(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Hand;
        }

        private void UIElement_OnMouseLeaveCursorArrow(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void MenuItemRemoveDisabledPorts_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MessageBox.Show(this, "Remove disabled ports from the configuration?", "Remove ports", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    DBEventManager.BroadCastSerialPortUserControlStatus(SerialPortUserControlStatus.DisposeDisabledPorts);
                }
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

    }
}