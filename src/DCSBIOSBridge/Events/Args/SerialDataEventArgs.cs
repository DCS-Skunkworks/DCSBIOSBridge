namespace DCSBIOSBridge.Events.Args
{
    public enum StreamInterface
    {
        SerialPortWritten,
        SerialPortRead
    }

    public class SerialDataEventArgs : EventArgs
    {
        public string ComPort { get; init; }
        public int Bytes { get; init; }
        public StreamInterface StreamInterface { get; init; }
    }
}
