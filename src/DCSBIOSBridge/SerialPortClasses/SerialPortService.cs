using System.IO.Ports;
using System.Management;
using DCSBIOSBridge.Events;
using NLog;

namespace DCSBIOSBridge.SerialPortClasses
{


    //Creds to : http://stackoverflow.com/questions/4199083/detect-serial-port-insertion-removal
    public class SerialPortService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private string[] _serialPorts;
        private ManagementEventWatcher _managementEventWatchArrival;
        private ManagementEventWatcher _managementEventWatchRemoval;

        public SerialPortService()
        {
            _serialPorts = SerialPort.GetPortNames();
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
        public void CleanUp()
        {
            _managementEventWatchArrival.Stop();
            _managementEventWatchRemoval.Stop();
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
                var availableSerialPorts = SerialPort.GetPortNames();
                if (_serialPorts.SequenceEqual(availableSerialPorts)) return;

                _serialPorts = availableSerialPorts;
                DBEventManager.BroadCastWindowsPortEvent(null, _serialPorts, eventType);
            }
        }
    }


    public enum WindowsSerialPortEventType
    {
        Insertion,
        Removal,
    }
}
