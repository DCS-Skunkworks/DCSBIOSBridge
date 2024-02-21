using System.IO.Ports;

namespace DCSBIOSDataBroker.Interfaces;

/// <summary>
/// Receives the modem output from the serial port.
/// </summary>
internal interface ISerialReceiver
{
    SerialPort SerialPort { get; set; }
    void ReceiveTextOverSerial(object sender, SerialDataReceivedEventArgs e);
}