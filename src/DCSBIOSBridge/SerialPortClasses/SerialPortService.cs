using System.Diagnostics;
using System.IO.Ports;
using System.Management;
using DCSBIOSBridge.Events;
using DCSBIOSBridge.Events.Args;
using DCSBIOSBridge.Interfaces;
using DCSBIOSBridge.misc;
using NLog;
using Theraot.Collections;

namespace DCSBIOSBridge.SerialPortClasses
{


    //Creds to : http://stackoverflow.com/questions/4199083/detect-serial-port-insertion-removal
    public class SerialPortService : ISerialPortStatusListener, IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private List<string> _serialPorts;
        private ManagementEventWatcher _managementEventWatchArrival;
        private ManagementEventWatcher _managementEventWatchRemoval;

        public SerialPortService()
        {
            _serialPorts = Common.GetSerialPortNames();
            DBEventManager.AttachSerialPortStatusListener(this);
            MonitorDeviceChanges();
        }
        /// <summary>
        /// If this method isn't called, an InvalidComObjectException will be thrown (like below):
        /// System.Runtime.InteropServices.InvalidComObjectException was unhandled
        ///Message=COM object that has been separated from its underlying RCW cannot be used.
        ///Source=mscorlib
        ///StackTrace:
        ///     at System.StubHelpers.StubHelpers.StubRegisterRCW(Object pThis, IntPtr pThread)
        ///     at System.Management.IWbemServices.CancelAsyncCall_(IWbemObjectSink pSink)
        ///     at System.Management.SinkForEventQuery.Cancel()
        ///     at System.Management.ManagementEventWatcher.Stop()
        ///     at System.Management.ManagementEventWatcher.Finalize()
        ///InnerException: 
        /// </summary>
        
        public void Dispose()
        {
            DBEventManager.DetachSerialPortStatusListener(this);
            _managementEventWatchArrival?.Dispose();
            _managementEventWatchRemoval?.Dispose();
        }

        private void MonitorDeviceChanges()
        {
            try
            {
                var deviceArrivalQuery = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2");
                var deviceRemovalQuery = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3");

                _managementEventWatchArrival = new ManagementEventWatcher(deviceArrivalQuery);
                _managementEventWatchRemoval = new ManagementEventWatcher(deviceRemovalQuery);

                _managementEventWatchArrival.EventArrived += (o, args) => RaisePortsChangedIfNecessary(WindowsSerialPortEventType.Insertion);
                _managementEventWatchRemoval.EventArrived += (sender, eventArgs) => RaisePortsChangedIfNecessary(WindowsSerialPortEventType.Removal);

                // Start listening for events
                _managementEventWatchArrival.Start();
                _managementEventWatchRemoval.Start();
            }
            catch (ManagementException me)
            {
                Logger.Error(me);
            }
        }

        private void RaisePortsChangedIfNecessary(WindowsSerialPortEventType eventType)
        {
            lock (_serialPorts)
            {
                var availableSerialPorts = Common.GetSerialPortNames();

                if (availableSerialPorts.Except(_serialPorts).ToArray().Length == 0 && _serialPorts.Except(availableSerialPorts).ToArray().Length == 0)
                {
                    Logger.Info($"No port changes detected. SerialPorts = {string.Join(", ", _serialPorts)}");
                    return;
                }

                Logger.Info($"Earlier ports = {string.Join(", ", _serialPorts)}, available ports = {string.Join(", ", availableSerialPorts)}");

                switch (eventType)
                {
                    case WindowsSerialPortEventType.Insertion:
                        {
                            var addedPorts = availableSerialPorts.Except(_serialPorts).ToArray();
                            Logger.Info($"Added ports = {string.Join(", ", addedPorts)}");

                            DBEventManager.BroadCastWindowsPortEvent(null, addedPorts, eventType);
                            break;
                        }
                    case WindowsSerialPortEventType.Removal:
                        {
                            var removedPorts = _serialPorts.Except(availableSerialPorts).ToArray();
                            Logger.Info($"Removed ports = {string.Join(", ", removedPorts)}");

                            DBEventManager.BroadCastWindowsPortEvent(null, removedPorts, eventType);
                            break;
                        }
                }

                _serialPorts = availableSerialPorts;
            }
        }

        /*
        private void RaisePortsChangedIfNecessary(WindowsSerialPortEventType eventType)
        {
            lock (_serialPorts)
            {
                var availableSerialPorts = Common.GetSerialPortNames();
                if (_serialPorts.SequenceEqual(availableSerialPorts)) return;

                _serialPorts = availableSerialPorts;
                DBEventManager.BroadCastWindowsPortEvent(null, _serialPorts, eventType);
            }
        }
        */
        public void OnSerialPortStatusChanged(SerialPortStatusEventArgs e)
        {
            switch (e.SerialPortStatus)
            {
                case SerialPortStatus.Opened:
                    break;
                case SerialPortStatus.Closed:
                    break;
                case SerialPortStatus.Open:
                    break;
                case SerialPortStatus.Close:
                    break;
                case SerialPortStatus.Added:
                    break;
                case SerialPortStatus.Hidden:
                    break;
                case SerialPortStatus.None:
                    break;
                case SerialPortStatus.Ok:
                    break;
                case SerialPortStatus.Error:
                    break;
                case SerialPortStatus.IOError:
                    break;
                case SerialPortStatus.Critical:
                    {
                        /*
                         * Critical here is used when:
                         * - port is open
                         * - USB cable is removed
                         *
                         * Windows SerialPort.GetPortNames() list is not updated when this
                         * happens.
                         */
                        _serialPorts.RemoveWhere(o => o == e.SerialPortName);
                        break;
                    }
                case SerialPortStatus.TimeOutError:
                    break;
                case SerialPortStatus.BytesWritten:
                    break;
                case SerialPortStatus.BytesRead:
                    break;
                case SerialPortStatus.Settings:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }


    public enum WindowsSerialPortEventType
    {
        Insertion,
        Removal,
    }
}
