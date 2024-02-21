using System.IO;
using System.IO.Ports;
using System.Threading.Channels;
using DCS_BIOS.EventArgs;
using DCS_BIOS.Interfaces;
using DCSBIOSDataBroker.Events;
using DCSBIOSDataBroker.Events.Args;
using DCSBIOSDataBroker.Interfaces;
using DCSBIOSDataBroker.misc;
using NLog;

namespace DCSBIOSDataBroker.SerialPortClasses
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

    public class SerialPortShell : IAsyncDcsBiosBulkDataListener, IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        //mode COM%COMPORT% BAUD=500000 PARITY=N DATA=8 STOP=1 TO=off DTR=on

        private readonly SerialPort _serialPort;
        private readonly ISerialReceiver _serialReceiver;
        private readonly Channel<byte[]> _serialDataToWrite = Channel.CreateUnbounded<byte[]>();
        private readonly AutoResetEvent _serialDataWaitingForWriteResetEvent = new(false);
        private bool _shutdown;

        public SerialPortSetting SerialPortSetting { get; set; } = new();

        public SerialPortShell(SerialPortSetting serialPortSetting)
        {
            SerialPortSetting = serialPortSetting;
            _serialPort = new SerialPort();
            _serialReceiver = new SerialReceiver
            {
                SerialPort = _serialPort
            };
            _serialPort.DataReceived += _serialReceiver.ReceiveTextOverSerial;
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
                //  dispose managed state (managed objects).
                _serialDataWaitingForWriteResetEvent.Set();
                _serialPort.DataReceived -= _serialReceiver.ReceiveTextOverSerial;
                _serialPort?.Close();
                _serialPort?.Dispose();
                BIOSEventHandler.DetachAsyncBulkDataListener(this);
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
            if (_serialPort != null && _serialPort.IsOpen) return;

            if (_serialPort == null) return;

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
        }

        private async Task QueueSerialData(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            var cts = new CancellationTokenSource(Constants.MS100);
            await _serialDataToWrite.Writer.WriteAsync(data, cts.Token);
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
                    var serialDataArray = await _serialDataToWrite.Reader.ReadAsync(cts.Token);

                    var cts2 = new CancellationTokenSource(Constants.MS200);
                    await _serialPort.BaseStream.WriteAsync(serialDataArray, 0, serialDataArray.Length, cts2.Token);
                    DBEventManager.BroadCastDataReceived(ComPort, serialDataArray.Length, StreamInterface.SerialPortWritten);
                }
                catch (Exception e)
                {
                    Logger.Error("AsyncSerialDataWrite failed => {0}", e);
                }
            }
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
            var existingPorts = SerialPort.GetPortNames();
            return existingPorts.Any(portName.Equals);
        }
    }
}
