using DCSBIOSBridge.Events;
using DCSBIOSBridge.Events.Args;

namespace DCSBIOSBridge.Interfaces
{
    internal interface ISerialPortStatusListener
    {
        void OnSerialPortStatusChanged(SerialPortStatusEventArgs  e);
    }
}
