using System.IO.Ports;
using System.IO;

namespace DCSBIOSBridge.SerialPortClasses
{
    /// <summary>
    /// https://stackoverflow.com/questions/13408476/detecting-when-a-serialport-gets-disconnected/63430909#63430909
    ///
    /// For handling the problem when a USB cable is yanked out while the serial port is open.
    /// NB also the background thread checking SerialPort.IsOpen works good, adding this can't make it worse at least.
    /// </summary>
    public class SafeSerialPort : SerialPort
    {
        private Stream _parentBaseStream;

        public new void Open()
        {
            base.Open();
            _parentBaseStream = BaseStream;
            GC.SuppressFinalize(BaseStream);
        }

        public new void Dispose()
        {
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Container != null)
            {
                Container.Dispose();
            }
            try
            {
                if (_parentBaseStream != null && _parentBaseStream.CanRead)
                {
                    _parentBaseStream.Close();
                    GC.ReRegisterForFinalize(_parentBaseStream);
                    _parentBaseStream = null;
                }
            }
            catch
            {
                // ignore exception - bug with USB - serial adapters.
            }
            base.Dispose(disposing);
        }
    }
}
