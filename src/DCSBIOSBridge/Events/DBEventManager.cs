using DCSBIOSBridge.Events.Args;
using DCSBIOSBridge.Interfaces;
using DCSBIOSBridge.SerialPortClasses;
using DCSBIOSBridge.UserControls;

namespace DCSBIOSBridge.Events
{
    internal static class DBEventManager
    {
        /*
         * For broadcasting serial port status changes or errors
         */
        public delegate void SerialPortStatusEventHandler(SerialPortStatusEventArgs e);
        public static event SerialPortStatusEventHandler OnSerialPortStatus;

        public static void AttachSerialPortStatusListener(ISerialPortStatusListener serialPortStatusListener)
        {
            OnSerialPortStatus += serialPortStatusListener.OnSerialPortStatusChanged;
        }

        public static void DetachSerialPortStatusListener(ISerialPortStatusListener serialPortStatusListener)
        {
            OnSerialPortStatus -= serialPortStatusListener.OnSerialPortStatusChanged;
        }

        public static void BroadCastPortStatus(
            string serialPortName,
            SerialPortStatus serialPortStatus,
            int bytesWritten = 0,
            string dcsbiosCommandCalled = null,
            SerialPortSetting serialPortSetting = null)
        {
            OnSerialPortStatus?.Invoke(new SerialPortStatusEventArgs
            {
                SerialPortName = serialPortName,
                SerialPortStatus = serialPortStatus,
                BytesWritten = bytesWritten,
                DCSBIOSCommandCalled = dcsbiosCommandCalled,
                SerialPortSetting = serialPortSetting
            });
        }

        /*
         * For broadcasting serial port * usercontrol * changes
         */
        public delegate void SerialPortUserControlCreatedEventHandler(SerialPortUserControlArgs e);
        public static event SerialPortUserControlCreatedEventHandler OnSerialPortUserControlStatusChanged;

        public static void AttachSerialPortUserControlListener(ISerialPortUserControlListener usercontrolListener)
        {
            OnSerialPortUserControlStatusChanged += usercontrolListener.OnSerialPortUserControlStatusChanged;
        }

        public static void DetachSerialPortUserControlListener(ISerialPortUserControlListener usercontrolListener)
        {
            OnSerialPortUserControlStatusChanged -= usercontrolListener.OnSerialPortUserControlStatusChanged;
        }

        public static void BroadCastSerialPortUserControlStatus(SerialPortUserControlStatus status, string comPort = null, SerialPortUserControl userControl = null, List<SerialPortSetting> serialPortSettings = null, HardwareInfoToShow hardwareInfoToShow = HardwareInfoToShow.Name)
        {
            OnSerialPortUserControlStatusChanged?.Invoke(new SerialPortUserControlArgs {ComPort = comPort, Status = status, SerialPortUserControl = userControl, SerialPortSettings = serialPortSettings, HardwareInfoToShow = hardwareInfoToShow});
        }

        /*
         * Windows Serial Port Event
         */
        public delegate void PortsChangedEventHandler(object sender, PortsChangedArgs e);
        public static event PortsChangedEventHandler OnSerialPortsChanged;

        public static void AttachWindowsSerialPortEventListener(IWindowsSerialPortEventListener windowsSerialPortEventListener)
        {
            OnSerialPortsChanged += windowsSerialPortEventListener.PortsChangedEvent;
        }

        public static void DetachWindowsSerialPortEventListener(IWindowsSerialPortEventListener windowsSerialPortEventListener)
        {
            OnSerialPortsChanged -= windowsSerialPortEventListener.PortsChangedEvent;
        }

        public static void BroadCastWindowsPortEvent(object sender, string[] portNames, WindowsSerialPortEventType eventType)
        {
            OnSerialPortsChanged?.Invoke(sender, new PortsChangedArgs
            {
                SerialPorts = portNames,
                EventType = eventType
            });
        }

        /*
         * For broadcasting settings are dirty
         */
        public delegate void SettingsDirtyEventHandler(SettingsDirtyEventArgs args);
        public static event SettingsDirtyEventHandler OnSettingsDirty;

        public static void AttachSettingsDirtyListener(ISettingsDirtyListener settingsDirtyListener)
        {
            OnSettingsDirty += settingsDirtyListener.OnSettingsDirty;
        }

        public static void DetachSettingsDirtyListener(ISettingsDirtyListener settingsDirtyListener)
        {
            OnSettingsDirty -= settingsDirtyListener.OnSettingsDirty;
        }

        public static void BroadCastSettingsDirty(bool isDirty)
        {
            OnSettingsDirty?.Invoke(new SettingsDirtyEventArgs { IsDirty = isDirty });
        }

        /*
         * For broadcasting data from DCS-BIOS and Serial Port
         */
        public delegate void DataReceivedEventHandler(SerialDataEventArgs args);
        public static event DataReceivedEventHandler OnDataReceived;

        public static void AttachDataReceivedListener(ISerialDataListener dataReceivedListener)
        {
            OnDataReceived += dataReceivedListener.OnDataReceived;
        }

        public static void DetachDataReceivedListener(ISerialDataListener dataReceivedListener)
        {
            OnDataReceived -= dataReceivedListener.OnDataReceived;
        }

        public static void BroadCastSerialData(string comPort, int bytes, StreamInterface stream)
        {
            OnDataReceived?.Invoke(new SerialDataEventArgs { ComPort = comPort, Bytes = bytes, StreamInterface = stream });
        }
    }
}
