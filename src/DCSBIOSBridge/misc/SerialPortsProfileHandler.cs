using System.IO;
using System.Text;
using DCSBIOSBridge.Events;
using DCSBIOSBridge.Events.Args;
using DCSBIOSBridge.Interfaces;
using DCSBIOSBridge.Properties;
using DCSBIOSBridge.SerialPortClasses;
using DCSBIOSBridge.UserControls;

namespace DCSBIOSBridge.misc
{

    public class SerialPortsProfileHandler : ISerialPortStatusListener, ISerialPortUserControlListener, IDisposable
    {
        private readonly object _lockObject = new();
        private List<string> SerialPortsToHide { get; set; } = [];
        public List<SerialPortSetting> SerialPortSettings { get; set; } = [];
        public string FileName { get; set; } = Constants.DefaultProfileName;
        public bool IsNewProfile { get; set; }



        public SerialPortsProfileHandler()
        {
            IsNewProfile = true;
            DBEventManager.AttachSerialPortStatusListener(this);
            DBEventManager.AttachSerialPortUserControlListener(this);
        }

        #region IDisposable Support
        private bool _hasBeenCalledAlready; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (_hasBeenCalledAlready) return;

            if (disposing)
            {
                //  dispose managed state (managed objects).
                DBEventManager.DetachSerialPortStatusListener(this);
                DBEventManager.DetachSerialPortUserControlListener(this);
            }

            //  free unmanaged resources (unmanaged objects) and override a finalizer below.

            //  set large fields to null.
            _hasBeenCalledAlready = true;
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);

            //  uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion

        public void OnSerialPortStatusChanged(SerialPortStatusEventArgs e)
        {
            try
            {
                switch (e.SerialPortStatus)
                {
                    case SerialPortStatus.Close:
                        break;
                    case SerialPortStatus.Open:
                        break;
                    case SerialPortStatus.Opened:
                        break;
                    case SerialPortStatus.Closed:
                        break;
                    case SerialPortStatus.Added:
                        {
                            AddSerialPort(e.SerialPortSetting);
                            break;
                        }
                    case SerialPortStatus.Hidden:
                        {
                            RemoveSerialPort(e.SerialPortName);
                            break;
                        }
                    case SerialPortStatus.None:
                        break;
                    case SerialPortStatus.Ok:
                        break;
                    case SerialPortStatus.Error:
                        break;
                    case SerialPortStatus.Critical:
                        break;
                    case SerialPortStatus.IOError:
                        break;
                    case SerialPortStatus.TimeOutError:
                        break;
                    case SerialPortStatus.BytesWritten:
                        break;
                    case SerialPortStatus.BytesRead:
                        break;
                    case SerialPortStatus.DCSBIOSCommandCalled:
                        break;
                    case SerialPortStatus.Settings:
                        {
                            UpdateSettings(e.SerialPortSetting);
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException(e.SerialPortStatus.ToString());
                }
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        public void OnSerialPortUserControlStatusChanged(SerialPortUserControlArgs args)
        {
            switch (args.Status)
            {
                case SerialPortUserControlStatus.Created:
                    break;
                case SerialPortUserControlStatus.Hidden:
                case SerialPortUserControlStatus.Closed:
                    {
                        RemoveSerialPort(args.ComPort);
                        break;
                    }
                case SerialPortUserControlStatus.Check:
                    break;
                case SerialPortUserControlStatus.DisposeDisabledPorts:
                    break;
                case SerialPortUserControlStatus.DoDispose:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void UpdateSettings(SerialPortSetting serialPortSetting)
        {
            SerialPortSettings.RemoveAll(o => o.ComPort == serialPortSetting.ComPort);
            SerialPortSettings.Add(serialPortSetting);
            DBEventManager.BroadCastSettingsDirty(true);
        }

        private void AddSerialPort(SerialPortSetting serialPortSetting)
        {
            lock (_lockObject)
            {
                if (SerialPortSettings.Any(o => o.ComPort == serialPortSetting.ComPort)) return;

                SerialPortSettings.RemoveAll(o => o.ComPort == serialPortSetting.ComPort);
                SerialPortSettings.Add(serialPortSetting);
                DBEventManager.BroadCastSettingsDirty(true);
            }
        }

        private void RemoveSerialPort(string portName)
        {
            lock (_lockObject)
            {
                DBEventManager.BroadCastSettingsDirty(true);

                SerialPortSettings.RemoveAll(o => o.ComPort == portName);

                if (SerialPortsToHide.Any(o => o == portName)) return;

                SerialPortsToHide.Add(portName);
            }
        }

        public void Reset()
        {
            SerialPortsToHide.Clear();
            SerialPortSettings.Clear();
            IsNewProfile = true;
            DBEventManager.BroadCastSettingsDirty(true);
        }

        public void ClearHiddenPorts()
        {
            //User has chosen to see all ports.
            if (SerialPortsToHide.Count <= 0) return;
            SerialPortsToHide.Clear();
            DBEventManager.BroadCastSettingsDirty(true);
        }

        public void LoadProfile(string filename)
        {
            try
            {
                if (!string.IsNullOrEmpty(filename) && !File.Exists(filename)) return;

                if (!string.IsNullOrEmpty(filename))
                {
                    FileName = filename;
                }
                else if (!string.IsNullOrEmpty(Settings.Default.LastProfileFileUsed) && File.Exists(Settings.Default.LastProfileFileUsed))
                {
                    FileName = Settings.Default.LastProfileFileUsed;
                }

                if (string.IsNullOrEmpty(FileName)) return;

                /*
                 * 0 Open specified filename (parameter) if not null
                 * 1 If exists open last profile used (settings)
                 * 3 If none found do nothing
                 */
                IsNewProfile = false;
                SerialPortsToHide.Clear();
                SerialPortSettings.Clear();

                Settings.Default.LastProfileFileUsed = FileName;
                Settings.Default.Save();
                var fileLines = File.ReadAllLines(FileName);
                foreach (var fileLine in fileLines)
                {
                    if (fileLine.StartsWith('#')) continue;


                    if (fileLine.StartsWith(Constants.ProfileSettingKeyword))
                    {
                        SerialPortSettings.Add(SerialPortSetting.ParseSetting(fileLine));
                    }
                    else if (fileLine.StartsWith(Constants.ProfileHiddenKeyword))
                    {
                        //HiddenList{COM1|COM3|COM4}
                        var str = fileLine.Replace(Constants.ProfileHiddenKeyword, "").Replace("#", "");
                        //COM1|COM3|COM4
                        var list = str.Split(["|"], StringSplitOptions.RemoveEmptyEntries);
                        foreach (var s in list)
                        {
                            SerialPortsToHide.Add(s);
                        }
                    }
                }

                DBEventManager.BroadCastSettingsDirty(false);
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        public void SaveProfile()
        {
            try
            {
                SaveProfileAs(FileName);
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }

        public void SaveProfileAs(string filename)
        {
            try
            {
                if (string.IsNullOrEmpty(filename)) return;

                FileName = filename;
                Settings.Default.LastProfileFileUsed = filename;
                Settings.Default.Save();

                var header = "#This file can be manually edited using any ASCII editor.\n#File created on " + DateTime.Today + " " + DateTime.Now;

                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(header);
                stringBuilder.AppendLine("#--------------------------------------------------------------------");

                foreach (var serialPortSetting in SerialPortSettings)
                {
                    if (serialPortSetting != null)
                    {
                        stringBuilder.AppendLine(serialPortSetting.ExportSetting());
                    }
                }
                if (SerialPortsToHide.Count > 0)
                {
                    stringBuilder.Append("HiddenList{");
                    foreach (var serialPort in SerialPortsToHide)
                    {
                        stringBuilder.Append(serialPort + '|');
                    }
                    if (stringBuilder.ToString().EndsWith('|'))
                    {
                        stringBuilder.Remove(stringBuilder.Length - 1, 1);
                    }
                    stringBuilder.Append('}');
                }
                File.WriteAllText(filename, stringBuilder.ToString(), Encoding.ASCII);

                DBEventManager.BroadCastSettingsDirty(false);
                IsNewProfile = false;

                LoadProfile(null);
            }
            catch (Exception ex)
            {
                Common.ShowErrorMessageBox(ex);
            }
        }
    }
}
