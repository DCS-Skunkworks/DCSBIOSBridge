using DCSBIOSBridge.Events.Args;

namespace DCSBIOSBridge.Interfaces
{
    internal interface ISerialDataListener
    {
        void OnDataReceived(SerialDataEventArgs e);
    }
}
