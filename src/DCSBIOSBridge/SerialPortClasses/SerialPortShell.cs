using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading.Channels;
using DCS_BIOS.EventArgs;
using DCS_BIOS.Interfaces;
using DCSBIOSBridge.Events;
using DCSBIOSBridge.Events.Args;
using DCSBIOSBridge.Interfaces;
using DCSBIOSBridge.misc;
using Microsoft.Win32;
using NLog;

namespace DCSBIOSBridge.SerialPortClasses
{
    public enum SerialPortStatus
    {
        Opened,
        Closed,
        Open,
        Close,
        Added,
        Hidden,
        None,
        Ok,
        IOError,
        TimeOutError,
        Error,
        Critical,
        BytesWritten,
        BytesRead,
        Settings
    }

    public enum HardwareInfoToShow
    {
        Name,
        VIDPID
    }

    public class SerialPortShell : IAsyncDcsBiosBulkDataListener, IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        //mode COM%COMPORT% BAUD=500000 PARITY=N DATA=8 STOP=1 TO=off DTR=on

        private SafeSerialPort _safeSerialPort;
        private SerialReceiver _serialReceiver;
        private readonly Channel<byte[]> _serialDataChannel = Channel.CreateUnbounded<byte[]>();
        private AutoResetEvent _serialDataWaitingForWriteResetEvent = new(false);
        private bool _shutdown;
        private bool _portShouldBeOpen;

        public SerialPortSetting SerialPortSetting { get; set; }

        public SerialPortShell(SerialPortSetting serialPortSetting)
        {
            Debug.WriteLine($"Creating shell for {serialPortSetting.ComPort}");
            SerialPortSetting = serialPortSetting;
            GetFriendlyName();

            var thread = new Thread(CheckPortOpen);
            thread.Start();
            
            BIOSEventHandler.AttachAsyncBulkDataListener(this);
        }

        #region IDisposable Support
        private bool _hasBeenCalledAlready; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (_hasBeenCalledAlready) return;

            if (disposing)
            {
                _shutdown = true;
                _portShouldBeOpen = false;

                Debug.WriteLine($"Disposing shell for {SerialPortSetting.ComPort}");
                
                BIOSEventHandler.DetachAsyncBulkDataListener(this);
                //  dispose managed state (managed objects).
                _serialDataWaitingForWriteResetEvent?.Set();
                _serialDataWaitingForWriteResetEvent?.Close();
                _serialDataWaitingForWriteResetEvent?.Dispose();
                _serialDataWaitingForWriteResetEvent = null;

                _serialReceiver?.Dispose();
                _serialReceiver = null;

                _safeSerialPort?.Close();
                _safeSerialPort?.Dispose();
                _safeSerialPort = null;
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

        public void Open()
        {
            if (_safeSerialPort != null && _safeSerialPort.IsOpen) return;

            Logger.Info($"Creating and opening serial port {SerialPortSetting.ComPort}");
 
            _serialReceiver?.Dispose();
            _serialReceiver = null;

            _safeSerialPort = new SafeSerialPort();
            _serialReceiver = new SerialReceiver
            {
                SerialPort = _safeSerialPort
            };
            _safeSerialPort.DataReceived += _serialReceiver.ReceiveTextOverSerial;
            _safeSerialPort.ErrorReceived += _serialReceiver.SerialPortError;

            ApplyPortConfig();
            try
            {
                GetFriendlyName();
                _safeSerialPort.Open();
            }
            catch (IOException e)
            {
                Common.ShowErrorMessageBox(e, $"Failed to open port {SerialPortSetting.ComPort}.");
                Logger.Error(e);
            }

            _portShouldBeOpen = true;

            _ = Task.Run(AsyncSerialDataWrite);
            DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Opened);
        }

        public bool IsOpen => _safeSerialPort != null && _safeSerialPort.IsOpen;

        public void Close()
        {
            try
            {
                if (_safeSerialPort == null) return;

                _portShouldBeOpen = false;

                Logger.Info($"Closing and disposing serial port {SerialPortSetting.ComPort}");

                _serialReceiver?.Dispose();
                _serialReceiver = null;

                _safeSerialPort.Close();
                _safeSerialPort.Dispose();
                _safeSerialPort = null;

                DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Closed);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public async Task AsyncDcsBiosBulkDataReceived(object sender, DCSBIOSBulkDataEventArgs e)
        {
            try
            {
                await QueueSerialData(e.Data);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        public void ApplyPortConfig(SerialPortSetting serialPortSetting)
        {
            if(serialPortSetting == null) return;

            var wasOpen = _safeSerialPort != null && _safeSerialPort.IsOpen;

            SerialPortSetting = serialPortSetting;
            Close();

            if (wasOpen)
            {
                Open();
            }
        }

        private void ApplyPortConfig()
        {
            if (_safeSerialPort == null) return;
            
            _safeSerialPort.PortName = SerialPortSetting.ComPort;
            _safeSerialPort.BaudRate = SerialPortSetting.BaudRate;
            _safeSerialPort.Parity = SerialPortSetting.Parity;
            _safeSerialPort.StopBits = SerialPortSetting.Stopbits;
            _safeSerialPort.DataBits = SerialPortSetting.Databits;
            _safeSerialPort.Handshake = SerialPortSetting.Handshake;
            _safeSerialPort.DtrEnable = SerialPortSetting.LineSignalDtr;
            _safeSerialPort.RtsEnable = SerialPortSetting.LineSignalRts;
            _safeSerialPort.WriteTimeout = SerialPortSetting.WriteTimeout == 0 ? SerialPort.InfiniteTimeout : SerialPortSetting.WriteTimeout;
            _safeSerialPort.ReadTimeout = SerialPortSetting.ReadTimeout == 0 ? SerialPort.InfiniteTimeout : SerialPortSetting.ReadTimeout;

            GetFriendlyName();
        }

        private async Task QueueSerialData(byte[] data)
        {
            if (data == null || data.Length == 0 || _safeSerialPort == null || !_safeSerialPort.IsOpen) return;

            var cts = new CancellationTokenSource(Constants.MS100);
            await _serialDataChannel.Writer.WriteAsync(data, cts.Token);
            _serialDataWaitingForWriteResetEvent.Set();
        }

        private async Task AsyncSerialDataWrite()
        {
            while (true)
            {
                try
                {
                    _serialDataWaitingForWriteResetEvent.WaitOne();
                    if (_shutdown || _safeSerialPort == null || !_safeSerialPort.IsOpen) break;

                    var cts = new CancellationTokenSource(Constants.MS100);
                    var serialDataArray = await _serialDataChannel.Reader.ReadAsync(cts.Token);

                    var cts2 = new CancellationTokenSource(Constants.MS200);
                    await _safeSerialPort.BaseStream.WriteAsync(serialDataArray, 0, serialDataArray.Length, cts2.Token);
                    DBEventManager.BroadCastSerialData(ComPort, serialDataArray.Length, StreamInterface.SerialPortWritten);
                }
                catch (OperationCanceledException e)
                {
                    Logger.Error("AsyncSerialDataWrite failed => {0}", e);
                    DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Error);
                    break;
                }
                catch (IOException e)
                {
                    Logger.Error("AsyncSerialDataWrite failed => {0}", e);
                    DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.IOError);
                    break;
                }
                catch (Exception e)
                {
                    Logger.Error("AsyncSerialDataWrite failed => {0}", e);
                    DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Error);
                    break;
                }
            }
        }

        private static IEnumerable<RegistryKey> GetSubKeys(RegistryKey key)
        {
            foreach (var keyName in key.GetSubKeyNames())
                using (var subKey = key.OpenSubKey(keyName))
                    yield return subKey;
        }

        private static string GetName(RegistryKey key)
        {
            var name = key.Name;
            int idx;
            return (idx = name.LastIndexOf('\\')) == -1 ? name : name[(idx + 1)..];
        }

        private void GetFriendlyName()
        {
            if (!GetFriendlyName1())
            {
                GetFriendlyName2();
            }
        }

        private bool GetFriendlyName1()
        {
            using var usbDevicesKey = Registry.LocalMachine.OpenSubKey(Constants.USBDevices);

            foreach (var usbDeviceKey in GetSubKeys(usbDevicesKey))
            {
                foreach (var devFnKey in GetSubKeys(usbDeviceKey))
                {
                    var friendlyName = (string)devFnKey.GetValue("FriendlyName") ?? (string)devFnKey.GetValue("DeviceDesc");

                    using var deviceParametersKey = devFnKey.OpenSubKey("Device Parameters");
                    var portName = (string)deviceParametersKey?.GetValue("PortName");

                    if (string.IsNullOrEmpty(portName) || SerialPortSetting.ComPort != portName) continue;

                    FriendlyName = friendlyName?.Replace($"({SerialPortSetting.ComPort})", "", StringComparison.Ordinal);
                    VIDPID = GetName(usbDeviceKey);
                    FriendlyName = string.IsNullOrEmpty(FriendlyName) ? VIDPID : FriendlyName;

                    return true;
                    //yield return new UsbSerialPort(portName, GetName(devBaseKey) + @"\" + GetName(devFnKey), friendlyName);
                }
            }

            return false;
        }

        private bool GetFriendlyName2()
        {
            using var devicesKeys = Registry.LocalMachine.OpenSubKey(Constants.DeviceEnumeration);

            foreach (var deviceKey in GetSubKeys(devicesKeys))
            {
                foreach (var deviceSub1Key in GetSubKeys(deviceKey))
                {
                    foreach (var deviceSub2Key in GetSubKeys(deviceSub1Key))
                    {
                        var friendlyName = (string)deviceSub2Key.GetValue("FriendlyName") ?? (string)deviceSub2Key.GetValue("DeviceDesc");

                        using var deviceParametersKey = deviceSub2Key.OpenSubKey("Device Parameters");
                        var portName = (string)deviceParametersKey?.GetValue("PortName");

                        if (string.IsNullOrEmpty(portName) || SerialPortSetting.ComPort != portName) continue;

                        FriendlyName = friendlyName?.Replace($"({SerialPortSetting.ComPort})", "", StringComparison.Ordinal);
                        VIDPID = GetName(deviceKey);
                        FriendlyName = string.IsNullOrEmpty(FriendlyName) ? VIDPID : FriendlyName;

                        return true;
                    }
                }
            }

            return false;
        }

        public Handshake Handshake
        {
            get => SerialPortSetting.Handshake;
            set
            {
                SerialPortSetting.Handshake = value;
                DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Settings, 0, null, SerialPortSetting);
            }
        }

        public string ComPort
        {
            get => SerialPortSetting.ComPort;
            set
            {
                SerialPortSetting.ComPort = value;
                DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Settings, 0, null, SerialPortSetting);
            }
        }

        public string FriendlyName { get; set; }

        public string VIDPID { get; set; }

        public int BaudRate
        {
            get => SerialPortSetting.BaudRate;
            set
            {
                SerialPortSetting.BaudRate = value;
                DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Settings, 0, null, SerialPortSetting);
            }
        }

        public int Databits
        {
            get => SerialPortSetting.Databits;
            set
            {
                SerialPortSetting.Databits = value;
                DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Settings, 0, null, SerialPortSetting);
            }
        }

        public StopBits Stopbits
        {
            get => SerialPortSetting.Stopbits;
            set
            {
                SerialPortSetting.Stopbits = value;
                DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Settings, 0, null, SerialPortSetting);
            }
        }

        public Parity Parity
        {
            get => SerialPortSetting.Parity;
            set
            {
                SerialPortSetting.Parity = value;
                DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Settings, 0, null, SerialPortSetting);
            }
        }

        public int WriteTimeout
        {
            get => SerialPortSetting.WriteTimeout;
            set
            {
                SerialPortSetting.WriteTimeout = value;
                DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Settings, 0, null, SerialPortSetting);
            }
        }

        public int ReadTimeout
        {
            get => SerialPortSetting.ReadTimeout;
            set
            {
                SerialPortSetting.ReadTimeout = value;
                DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Settings, 0, null, SerialPortSetting);
            }
        }

        public bool LineSignalDtr
        {
            get => SerialPortSetting.LineSignalDtr;
            set
            {
                SerialPortSetting.LineSignalDtr = value;
                DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Settings, 0, null, SerialPortSetting);
            }
        }

        public bool LineSignalRts
        {
            get => SerialPortSetting.LineSignalRts;
            set
            {
                SerialPortSetting.LineSignalRts = value;
                DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Settings, 0, null, SerialPortSetting);
            }
        }

        public static bool SerialPortCurrentlyExists(string portName)
        {
            if (string.IsNullOrEmpty(portName))
            {
                return false;
            }
            var existingPorts = Common.GetSerialPortNames();
            return existingPorts.Any(portName.Equals);
        }

        private void CheckPortOpen()
        {
            try
            {
                while (!_shutdown)
                {
                    if (_portShouldBeOpen)
                    {
                        if (_safeSerialPort == null || !_safeSerialPort.IsOpen)
                        {
                            Logger.Error("Background Thread (CheckPortOpen) detected port is not open.");
                            DBEventManager.BroadCastPortStatus(SerialPortSetting.ComPort, SerialPortStatus.Critical);
                            break;
                        }
                    }

                    Thread.Sleep(Constants.MS1000);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Port check thread.");
            }
        }
    }
}
