using DCSBIOSDataBroker.SerialPortClasses;

namespace DCSBIOSDataBroker.Events.Args
{
    public class SerialPortStatusEventArgs : EventArgs
    {
        public string SerialPortName { get; init; }        
        public SerialPortStatus SerialPortStatus { get; init; }
        public int BytesWritten { get; init; }
        public string DCSBIOSCommandCalled { get; init; }
        public SerialPortSetting SerialPortSetting { get; init; }
    }
}
