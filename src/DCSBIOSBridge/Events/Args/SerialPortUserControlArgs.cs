using DCSBIOSBridge.SerialPortClasses;
using DCSBIOSBridge.UserControls;

namespace DCSBIOSBridge.Events.Args
{
    public class SerialPortUserControlArgs : EventArgs
    {
        public SerialPortUserControlStatus Status { get; init; }
        public SerialPortUserControl SerialPortUserControl { get; init; }
        public List<SerialPortSetting> SerialPortSettings { get; init; }
    }
}
