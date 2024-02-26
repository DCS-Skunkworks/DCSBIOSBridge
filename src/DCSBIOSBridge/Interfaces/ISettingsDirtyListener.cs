using DCSBIOSBridge.Events;
using DCSBIOSBridge.Events.Args;

namespace DCSBIOSBridge.Interfaces
{
    internal interface ISettingsDirtyListener
    {
        void OnSettingsDirty(SettingsDirtyEventArgs args);
    }
}
