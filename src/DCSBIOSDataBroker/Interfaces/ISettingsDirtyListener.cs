using DCSBIOSDataBroker.Events;
using DCSBIOSDataBroker.Events.Args;

namespace DCSBIOSDataBroker.Interfaces
{
    internal interface ISettingsDirtyListener
    {
        void OnSettingsDirty(SettingsDirtyEventArgs args);
    }
}
