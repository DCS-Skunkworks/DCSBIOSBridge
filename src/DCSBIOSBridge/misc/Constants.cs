﻿namespace DCSBIOSBridge.misc
{
    internal static class Constants
    {
        public const int MS100 = 100;
        public const int MS200 = 200;
        public const int MS300 = 300;
        public const int MS400 = 400;
        public const int MS500 = 500;
        public const int MS600 = 600;
        public const int MS700 = 700;
        public const int MS800 = 800;
        public const int MS900 = 900;
        public const int MS1000 = 1000;

        public const string DefaultProfileName = "serialports.dcs-bios_settings";
        public const string WildCardProfileSearch = "*.dcs-bios_settings";
        public const string ProfileExtension = ".dcs-bios_settings";
        public const string ProfileFilter = "(.dcs-bios_settings)|*.dcs-bios_settings";
        public const string ProfileSettingKeyword = "SerialPort{";
        public const string ProfileHiddenKeyword = "HiddenList{";
        public const string ProfileOpenKeyword = "Open";
        public const string ProfileClosedKeyword = "Closed";

        public const int KiloByte = 1024;
        public const int MegaByte = 1048576;
    }
}