using DCSBIOSBridge.UserControls;

namespace DCSBIOSBridge.Events.Args
{
    public class SerialPortUserControlArgs : EventArgs
    {
        public SerialPortUserControlStatus Status { get; init; }
        public SerialPortUserControl SerialPortUserControl { get; init; }
    }
}
