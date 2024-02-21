using DCSBIOSDataBroker.Events;
using DCSBIOSDataBroker.Events.Args;

namespace DCSBIOSDataBroker.Interfaces
{
    internal interface IDataReceivedListener
    {
        void OnDataReceived(DataReceivedEventArgs e);
    }
}
