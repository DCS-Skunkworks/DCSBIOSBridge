using System.IO.Ports;
using System.Text;
using DCSBIOSBridge.misc;

namespace DCSBIOSBridge.SerialPortClasses
{
    public class SerialPortSetting
    {

        public static SerialPortSetting ParseSetting(string portSetting)
        {
            if (!portSetting.StartsWith(Constants.ProfileSettingKeyword)) throw new Exception("Default port settings have changed. Please create a new profile. If you need to change the port settings do so for the new profile.");

            //SerialPort{_comPort|_baudRate|_handshake|_databits|_stopbits|_parity|_writeTimeout|_readTimeout|_lineSignalDtr|LineSignalRts|connectedStatus}
            //SerialPort{COM1|500000|None|8|One|None|40000|40000|True|False|Closed}
            if (string.IsNullOrEmpty(portSetting)) return null;

            //COM1|500000|None|8|One|None|40000|40000|True|False|Closed
            var str = portSetting.Replace(Constants.ProfileSettingKeyword, "").Replace("}", "");

            //COM1|500000|None|8|One|None|40000|40000|True|False|Closed
            var list = str.Split(["|"], StringSplitOptions.RemoveEmptyEntries);
            var result = new SerialPortSetting
            {
                ComPort = list[0],
                BaudRate = int.Parse(list[1]),
                Handshake = (Handshake)Enum.Parse(typeof(Handshake), list[2]),
                Databits = int.Parse(list[3]),
                Stopbits = (StopBits)Enum.Parse(typeof(StopBits), list[4]),
                Parity = (Parity)Enum.Parse(typeof(Parity), list[5]),
                WriteTimeout = int.Parse(list[6]),
                ReadTimeout = int.Parse(list[7]),
                LineSignalDtr = bool.Parse(list[8]),
                LineSignalRts = bool.Parse(list[9])
            };
            result.LineSignalRts = bool.Parse(list[9]);
            return result;
        }

        public string ExportSetting()
        {
            var result = new StringBuilder();
            result.Append(Constants.ProfileSettingKeyword + ComPort + "|" +
                          BaudRate + "|" +
                          Handshake + "|" +
                          Databits + "|" +
                          Stopbits + "|" +
                          Parity + "|" +
                          WriteTimeout + "|" +
                          ReadTimeout + "|" +
                          LineSignalDtr + "|" +
                          LineSignalRts);
            return result.ToString();
        }

        public string ComPort { get; set; }
        public int BaudRate { get; set; } = 250000;
        public Parity Parity { get; set; } = Parity.None;
        public int Databits { get; set; } = 8;
        public StopBits Stopbits { get; set; } = StopBits.One;
        public bool LineSignalDtr { get; set; }
        public bool LineSignalRts { get; set; } = true;
        public Handshake Handshake { get; set; }
        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }
    }
}
