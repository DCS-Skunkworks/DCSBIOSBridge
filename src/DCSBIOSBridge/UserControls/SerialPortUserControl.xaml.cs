using System.ComponentModel;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using DCSBIOSBridge.Events;
using DCSBIOSBridge.Events.Args;
using DCSBIOSBridge.Interfaces;
using DCSBIOSBridge.misc;
using DCSBIOSBridge.SerialPortClasses;
using DCSBIOSBridge.Windows;

namespace DCSBIOSBridge.UserControls
{
    public enum SerialPortUserControlStatus
    {
        Created,
        Hidden,
        Closed,
        Check,
        DisposeDisabledPorts,
        DoDispose
    }

    /// <summary>
    /// Interaction logic for SerialPortUserControl.xaml
    /// </summary>
    public partial class SerialPortUserControl : ISerialPortStatusListener, ISerialPortUserControlListener, IDataReceivedListener, INotifyPropertyChanged, IDisposable
    {
        private readonly SerialPortShell _serialPortShell;
        private double _bytesFromDCSBIOS;
        private double _bytesFromSerialPort;
        private readonly Queue<string> _lastDCSBIOSCommands = new(10);
        private const int MaxQueueSize = 10;
        private bool _formLoaded;

        public SerialPortUserControl(SerialPortSetting serialPortSetting)
        {
            InitializeComponent();
            Name = serialPortSetting.ComPort;
            DataContext = this;
            LabelPort.Content = serialPortSetting.ComPort;
            _serialPortShell = new SerialPortShell(serialPortSetting);
            LabelFriendlyName.Content = _serialPortShell.FriendlyName;
            DBEventManager.AttachSerialPortStatusListener(this);
            DBEventManager.AttachSerialPortUserControlListener(this);
            DBEventManager.AttachDataReceivedListener(this);
            IsEnabled = SerialPortShell.SerialPortCurrentlyExists(serialPortSetting.ComPort);
        }

        #region IDisposable Support
        private bool _hasBeenCalledAlready; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (_hasBeenCalledAlready) return;

            if (disposing)
            {
                //  dispose managed state (managed objects).
                DBEventManager.DetachSerialPortStatusListener(this);
                DBEventManager.DetachSerialPortUserControlListener(this);
                DBEventManager.DetachDataReceivedListener(this);
                _serialPortShell.Close();
                _serialPortShell.Dispose();
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

        private void SerialPortUserControl_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_formLoaded) return;

                SetWindowState();
                _formLoaded = true;
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void SetWindowState()
        {
            //IsChecked = Port öppen
            ButtonConnection.IsChecked = _serialPortShell != null && _serialPortShell.IsOpen;
        }

        public void OnSerialPortStatusChanged(SerialPortStatusEventArgs e)
        {
            try
            {
                // null port name allowed when bulk opening, closing ports
                if (!string.IsNullOrEmpty(e.SerialPortName) && e.SerialPortName != _serialPortShell.ComPort) return;

                switch (e.SerialPortStatus)
                {
                    case SerialPortStatus.Open:
                        {
                            Dispatcher.Invoke(OpenPort);
                            break;
                        }
                    case SerialPortStatus.Close:
                        {
                            Dispatcher.Invoke(ClosePort);
                            break;
                        }
                    case SerialPortStatus.Opened:
                        break;
                    case SerialPortStatus.Closed:
                        break;
                    case SerialPortStatus.Added:
                    case SerialPortStatus.Hidden:
                        break;
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
                        {
                            LastDCSBIOSCommand = e.DCSBIOSCommandCalled;
                            break;
                        }
                    case SerialPortStatus.Settings:
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
                        break;
                    case SerialPortUserControlStatus.Hidden:
                        break;
                    case SerialPortUserControlStatus.Closed:
                        break;
                    case SerialPortUserControlStatus.Check:
                        {
                            CheckValidity(args.SerialPortSettings);
                            break;
                        }
                    case SerialPortUserControlStatus.DisposeDisabledPorts:
                        {
                            if (IsEnabled) return;

                            BroadCastClosedAndDispose(SerialPortUserControlStatus.Closed);
                            break;
                        }
                    case SerialPortUserControlStatus.DoDispose:
                        {
                            BroadCastClosedAndDispose(SerialPortUserControlStatus.Closed);
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException(args.Status.ToString());
                }
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void CheckValidity(List<SerialPortSetting> serialPortSettings)
        {
            try
            {
                var portName = "";
                Dispatcher.Invoke(() => portName = Name);
                //Check that COM port which _serialPort has does exist, what more?
                var ports = SerialPort.GetPortNames();
                var found = false;

                foreach (var port in ports)
                {
                    if (!port.Equals(portName)) continue;

                    found = true;
                    Dispatcher.Invoke(() => IsEnabled = true);
                    Dispatcher.Invoke(() => LabelFriendlyName.Content = _serialPortShell.FriendlyName);
                    break;
                }
                if (!found)
                {
                    // If this port is in the profile then it should be greyed out, not disposed
                    if (serialPortSettings == null)
                    {
                        BroadCastClosedAndDispose(SerialPortUserControlStatus.Closed);
                        return;
                    }

                    if (serialPortSettings.Any(o => o.ComPort == _serialPortShell.ComPort) == true)
                    {
                        Dispatcher.Invoke(() => IsEnabled = false);
                    }
                    else
                    {
                        BroadCastClosedAndDispose(SerialPortUserControlStatus.Closed);
                    }
                }
                Dispatcher.Invoke(() => SetWindowState);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => Common.ShowErrorMessageBox(ex));
            }
        }

        private void ButtonConnection_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ButtonConnection.IsChecked.HasValue && ButtonConnection.IsChecked.Value)
                {
                    _serialPortShell.Open();
                }
                else if (ButtonConnection.IsChecked.HasValue && !ButtonConnection.IsChecked.Value)
                {
                    _serialPortShell.Close();
                }
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
            //If error occurs set state 
            ButtonConnection.IsChecked = _serialPortShell.IsOpen;
        }

        private void OpenPort()
        {
            try
            {
                if (!IsEnabled || _serialPortShell.IsOpen) return;
                Dispatcher.Invoke(() => LabelFriendlyName.Content = _serialPortShell.FriendlyName);
                _serialPortShell.Open();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
            //If error occurs set state 
            ButtonConnection.IsChecked = _serialPortShell.IsOpen;
        }

        private void ClosePort()
        {
            try
            {
                if (!IsEnabled || !_serialPortShell.IsOpen) return;
                _serialPortShell.Close();
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
            //If error occurs set state 
            ButtonConnection.IsChecked = _serialPortShell.IsOpen;
        }

        private void ButtonRemoveSerialPort_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DBEventManager.BroadCastPortStatus(Name, SerialPortStatus.Hidden);
                BroadCastClosedAndDispose(SerialPortUserControlStatus.Hidden);
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void BroadCastClosedAndDispose(SerialPortUserControlStatus status)
        {
            Dispatcher.Invoke(() => DBEventManager.BroadCastSerialPortUserControlStatus(status, Name, this));
            Dispose(true);
        }

        public static void LoadSerialPorts(List<SerialPortSetting> serialPortsStringSettingsList)
        {
            try
            {
                foreach (var serialPortSetting in serialPortsStringSettingsList)
                {
                    var serialPortUserControl = new SerialPortUserControl(serialPortSetting);
                    DBEventManager.BroadCastSerialPortUserControlStatus(SerialPortUserControlStatus.Created, serialPortUserControl.Name, serialPortUserControl);
                }
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        public SerialPortSetting SerialPortSetting
        {
            get => _serialPortShell.SerialPortSetting;
            set => _serialPortShell.SerialPortSetting = value;
        }

        public void OnDataReceived(DataReceivedEventArgs e)
        {
            try
            {
                if (e.ComPort != _serialPortShell.ComPort) return;

                switch (e.StreamInterface)
                {
                    case StreamInterface.SerialPortRead:
                        {
                            _bytesFromSerialPort += e.Bytes;
                            SerialPortDataReceived = GetDataString(_bytesFromSerialPort);
                            break;
                        }
                    case StreamInterface.SerialPortWritten:
                        {
                            _bytesFromDCSBIOS += e.Bytes;
                            SerialPortDataWritten = GetDataString(_bytesFromDCSBIOS);
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(e));
                }
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private string GetDataString(double bytes)
        {
            return bytes switch
            {
                < Constants.KiloByte => bytes + " bytes",
                < Constants.MegaByte => Math.Round(bytes / Constants.KiloByte) + " KB",
                _ => Math.Round(bytes / Constants.MegaByte, 2) + " MB"
            };
        }

        private string _serialPortDataWritten;
        public string SerialPortDataWritten
        {
            get => _serialPortDataWritten;
            set
            {
                _serialPortDataWritten = value;
                // Call OnPropertyChanged whenever the property is updated
                OnPropertyChanged();
            }
        }

        private string _serialPortDataReceived;
        public string SerialPortDataReceived

        {
            get => _serialPortDataReceived;
            set
            {
                _serialPortDataReceived = value;
                // Call OnPropertyChanged whenever the property is updated
                OnPropertyChanged();
            }
        }

        private string _lastDCSBIOSCommand;
        public string LastDCSBIOSCommand

        {
            get => _lastDCSBIOSCommand;
            set
            {
                _lastDCSBIOSCommand = string.IsNullOrEmpty(value) ? string.Empty : value.Replace("_", "__");
                LastDCSBIOSCommands = value;
                // Call OnPropertyChanged whenever the property is updated
                OnPropertyChanged();
            }
        }


        public string LastDCSBIOSCommands

        {
            get => string.Join("\n", _lastDCSBIOSCommands.ToList());
            set
            {
                _lastDCSBIOSCommands.Enqueue(value);
                while (_lastDCSBIOSCommands.Count > MaxQueueSize)
                {
                    _lastDCSBIOSCommands.Dequeue();
                }
                // Call OnPropertyChanged whenever the property is updated
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SettingsButton_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var serialPortConfigWindow = new SerialPortConfigWindow(_serialPortShell.SerialPortSetting)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                if (serialPortConfigWindow.ShowDialog() == true)
                {
                    _serialPortShell.SerialPortSetting = serialPortConfigWindow.SerialPortSetting;
                    _serialPortShell.ApplyPortConfig();
                    DBEventManager.BroadCastPortStatus(Name, SerialPortStatus.Settings, 0, null, _serialPortShell.SerialPortSetting);
                }
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }
    }
}