using DCSBIOSDataBroker.Events;
using DCSBIOSDataBroker.Events.Args;

namespace DCSBIOSDataBroker.Interfaces
{
    public interface ISerialPortUserControlListener
    {
        void OnSerialPortUserControlStatusChanged(SerialPortUserControlArgs args);
    }
}
