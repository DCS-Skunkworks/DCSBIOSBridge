using DCSBIOSDataBroker.Events;
using DCSBIOSDataBroker.Events.Args;

namespace DCSBIOSDataBroker.Interfaces
{
    internal interface IWindowsSerialPortEventListener
    {
        public void PortsChangedEvent(object sender, PortsChangedArgs e);
    }
}
