using DCSBIOSBridge.Events;
using DCSBIOSBridge.Events.Args;

namespace DCSBIOSBridge.Interfaces
{
    internal interface IDataReceivedListener
    {
        void OnDataReceived(DataReceivedEventArgs e);
    }
}
