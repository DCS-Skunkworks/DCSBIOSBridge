using System.IO.Ports;

namespace DCSBIOSBridge.Interfaces;

/// <summary>
/// Receives the modem output from the serial port.
/// </summary>
internal interface ISerialReceiver
{
    SerialPort SerialPort { get; set; }
    void Dispose();
    void ReceiveTextOverSerial(object sender, SerialDataReceivedEventArgs e);
}