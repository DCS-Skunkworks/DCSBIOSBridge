using System.IO.Ports;
using System.Windows;
using DCSBIOSBridge.misc;
using DCSBIOSBridge.SerialPortClasses;

namespace DCSBIOSBridge.Windows
{
    /// <summary>
    /// Interaction logic for SerialPortConfigWindow.xaml
    /// </summary>
    public partial class SerialPortConfigWindow : Window
    {
        public SerialPortConfigWindow(string comPort)
        {
            InitializeComponent();
            SerialPortSetting.ComPort = comPort;
        }


        public SerialPortConfigWindow(SerialPortSetting serialPortSetting)
        {
            InitializeComponent();
            SerialPortSetting = serialPortSetting;
        }


        private void SerialPortConfigWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            //PopulateCombos();
            ShowValues();
        }

        private void ShowValues()
        {
            LabelSerialPortName.Content = SerialPortSetting.ComPort;
            ComboBoxBaud.SelectedValue = SerialPortSetting.BaudRate;
            ComboBoxParity.SelectedValue = SerialPortSetting.Parity;
            ComboBoxStopBits.SelectedValue = SerialPortSetting.Stopbits;
            ComboBoxDataBits.SelectedValue = SerialPortSetting.Databits;
            ComboBoxHandshake.SelectedValue = SerialPortSetting.Handshake;
            CheckBoxLineSignalRts.IsChecked = SerialPortSetting.LineSignalRts;
            CheckBoxLineSignalDtr.IsChecked = SerialPortSetting.LineSignalDtr;
            ComboBoxWriteTimeout.SelectedValue = SerialPortSetting.WriteTimeout;
            ComboBoxReadTimeout.SelectedValue = SerialPortSetting.ReadTimeout;

        }

        private void ButtonOk_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                SerialPortSetting.BaudRate = int.Parse(ComboBoxBaud.SelectedValue.ToString() ?? "0");
                SerialPortSetting.Parity = (Parity)Enum.Parse(typeof(Parity), ComboBoxParity.SelectedValue.ToString() ?? "None");
                SerialPortSetting.Stopbits = (StopBits)Enum.Parse(typeof(StopBits), ComboBoxStopBits.SelectedValue.ToString() ?? "One");
                SerialPortSetting.Databits = int.Parse(ComboBoxDataBits.SelectedValue.ToString() ?? "8");
                SerialPortSetting.Handshake = (Handshake)Enum.Parse(typeof(Handshake), ComboBoxHandshake.SelectedValue.ToString() ?? "None");   
                SerialPortSetting.LineSignalRts = CheckBoxLineSignalRts.IsChecked.GetValueOrDefault();
                SerialPortSetting.LineSignalDtr = CheckBoxLineSignalDtr.IsChecked.GetValueOrDefault();
                SerialPortSetting.WriteTimeout = int.Parse(ComboBoxWriteTimeout.SelectedValue.ToString() ?? "0");
                SerialPortSetting.ReadTimeout = int.Parse(ComboBoxReadTimeout.SelectedValue.ToString() ?? "0");
                DialogResult = true;
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        private void ButtonCancel_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = false;
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        public SerialPortSetting SerialPortSetting { get; set; } = new();
    }
}
