using DCSBIOSDataBroker.UserControls;

namespace DCSBIOSDataBroker.Events.Args
{
    public class SerialPortUserControlArgs : EventArgs
    {
        public SerialPortUserControlStatus Status { get; init; }
        public SerialPortUserControl SerialPortUserControl { get; init; }
    }
}
