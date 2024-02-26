using DCSBIOSBridge.Events;
using DCSBIOSBridge.Events.Args;

namespace DCSBIOSBridge.Interfaces
{
    internal interface IWindowsSerialPortEventListener
    {
        public void PortsChangedEvent(object sender, PortsChangedArgs e);
    }
}
