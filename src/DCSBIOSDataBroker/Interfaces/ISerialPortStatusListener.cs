using DCSBIOSDataBroker.Events;
using DCSBIOSDataBroker.Events.Args;

namespace DCSBIOSDataBroker.Interfaces
{
    internal interface ISerialPortStatusListener
    {
        void OnSerialPortStatusChanged(SerialPortStatusEventArgs  e);
    }
}
