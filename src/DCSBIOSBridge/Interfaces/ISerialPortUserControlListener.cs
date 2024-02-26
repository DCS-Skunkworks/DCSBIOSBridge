using DCSBIOSBridge.Events;
using DCSBIOSBridge.Events.Args;

namespace DCSBIOSBridge.Interfaces
{
    public interface ISerialPortUserControlListener
    {
        void OnSerialPortUserControlStatusChanged(SerialPortUserControlArgs args);
    }
}
