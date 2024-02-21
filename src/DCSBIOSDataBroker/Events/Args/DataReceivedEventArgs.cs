namespace DCSBIOSDataBroker.Events.Args
{
    public enum StreamInterface
    {
        SerialPortWritten,
        SerialPortRead
    }

    public class DataReceivedEventArgs : EventArgs
    {
        public string ComPort { get; init; }
        public int Bytes { get; init; }
        public StreamInterface StreamInterface { get; init; }
    }
}
