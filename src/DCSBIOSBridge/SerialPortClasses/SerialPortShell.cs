using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading.Channels;
using DCS_BIOS.EventArgs;
using DCS_BIOS.Interfaces;
using DCSBIOSBridge.Events;
using DCSBIOSBridge.Events.Args;
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
        Error,
        Critical,
        IOError,
        TimeOutError,
        BytesWritten,
        BytesRead,
        DCSBIOSCommandCalled,
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

        private SerialPort _serialPort;
        private SerialReceiver _serialReceiver;
        private readonly Channel<byte[]> _serialDataChannel = Channel.CreateUnbounded<byte[]>();
        private AutoResetEvent _serialDataWaitingForWriteResetEvent = new(false);
        private bool _shutdown;

        public SerialPortSetting SerialPortSetting { get; set; }

        public SerialPortShell(SerialPortSetting serialPortSetting)
        {
            SerialPortSetting = serialPortSetting;
            GetFriendlyName();
            _serialPort = new SerialPort();
            _serialReceiver = new SerialReceiver
            {
                SerialPort = _serialPort
            };
            _serialPort.DataReceived += _serialReceiver.ReceiveTextOverSerial;
            _serialPort.ErrorReceived += _serialReceiver.SerialPortError;

            ApplyPortConfig();

            if (serialPortSetting.Connected)
            {
                _serialPort.Open();
                DBEventManager.BroadCastPortStatus(_serialPort.PortName, SerialPortStatus.Opened);
            }
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
                BIOSEventHandler.DetachAsyncBulkDataListener(this);
                //  dispose managed state (managed objects).
                _serialDataWaitingForWriteResetEvent?.Set();
                _serialDataWaitingForWriteResetEvent?.Close();
                _serialDataWaitingForWriteResetEvent?.Dispose();
                _serialDataWaitingForWriteResetEvent = null;

                _serialReceiver.Release();
                _serialReceiver = null;

                _serialPort?.Close();
                _serialPort?.Dispose();
                _serialPort = null;
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
            if (_serialPort != null && _serialPort.IsOpen || _serialPort == null) return;

            ApplyPortConfig();
            try
            {
                GetFriendlyName();
                _serialPort.Open();
            }
            catch (IOException e)
            {
                Common.ShowErrorMessageBox(e, $"Failed to open port {SerialPortSetting.ComPort}.");
                Logger.Error(e);
            }
            _ = Task.Run(AsyncSerialDataWrite);
            DBEventManager.BroadCastPortStatus(_serialPort.PortName, SerialPortStatus.Opened);
        }

        public bool IsOpen => _serialPort != null && _serialPort.IsOpen;

        public void Close()
        {
            try
            {
                if (_serialPort != null && !_serialPort.IsOpen) return;

                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                    DBEventManager.BroadCastPortStatus(_serialPort.PortName, SerialPortStatus.Closed);
                }
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

        public void ApplyPortConfig()
        {
            if (_serialPort == null) return;

            var wasOpen = _serialPort.IsOpen;

            _serialPort.Close();

            _serialPort.PortName = SerialPortSetting.ComPort;
            _serialPort.BaudRate = SerialPortSetting.BaudRate;
            _serialPort.Parity = SerialPortSetting.Parity;
            _serialPort.StopBits = SerialPortSetting.Stopbits;
            _serialPort.DataBits = SerialPortSetting.Databits;
            if (!SerialPortSetting.LineSignalDtr && !SerialPortSetting.LineSignalRts)
            {
                _serialPort.Handshake = Handshake.XOnXOff;
            }
            _serialPort.DtrEnable = SerialPortSetting.LineSignalDtr;
            _serialPort.RtsEnable = SerialPortSetting.LineSignalRts;
            _serialPort.WriteTimeout = SerialPortSetting.WriteTimeout == 0 ? SerialPort.InfiniteTimeout : SerialPortSetting.WriteTimeout;
            _serialPort.ReadTimeout = SerialPortSetting.ReadTimeout == 0 ? SerialPort.InfiniteTimeout : SerialPortSetting.ReadTimeout;

            if (wasOpen) _serialPort.Open();
            GetFriendlyName();
        }

        private async Task QueueSerialData(byte[] data)
        {
            if (data == null || data.Length == 0 || _serialPort == null || !_serialPort.IsOpen) return;

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
                    if (_shutdown || _serialPort == null || !_serialPort.IsOpen) break;

                    var cts = new CancellationTokenSource(Constants.MS100);
                    var serialDataArray = await _serialDataChannel.Reader.ReadAsync(cts.Token);

                    var cts2 = new CancellationTokenSource(Constants.MS200);
                    await _serialPort.BaseStream.WriteAsync(serialDataArray, 0, serialDataArray.Length, cts2.Token);
                    DBEventManager.BroadCastSerialData(ComPort, serialDataArray.Length, StreamInterface.SerialPortWritten);
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
    }
}
