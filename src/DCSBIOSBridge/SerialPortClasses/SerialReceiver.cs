using System.IO;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using DCS_BIOS;
using DCSBIOSBridge.Events;
using DCSBIOSBridge.Events.Args;
using DCSBIOSBridge.Interfaces;
using DCSBIOSBridge.misc;
using NLog;

namespace DCSBIOSBridge.SerialPortClasses;

/// <summary>
/// Handles reading from serial port.
/// </summary>
internal class SerialReceiver : ISerialReceiver
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private SemaphoreSlim ReadFromSerialPortSemaphore { get; } = new(1);

    public SerialPort SerialPort { get; set; }
    private readonly StringBuilder _incomingData = new();

    public void Release()
    {
        SerialPort.DataReceived -= ReceiveTextOverSerial;
        SerialPort = null;
    }

    public async void ReceiveTextOverSerial(object sender, SerialDataReceivedEventArgs e)
    {
        await ReadFromSerialPortSemaphore.WaitAsync();

        switch (e.EventType)
        {
            case SerialData.Chars:
                {
                    try
                    {
                        Logger.Debug($"SerialPort.Buffer = {SerialPort.BytesToRead}");
                        var byteArray = new byte[SerialPort.BytesToRead];
                        var cts = new CancellationTokenSource(Constants.MS1000);
                        var bytesRead = await SerialPort.BaseStream.ReadAsync(byteArray, 0, byteArray.Length, cts.Token);

                        _incomingData.Append(Common.UsedEncoding.GetString(byteArray, 0, bytesRead));

                        // FLAPS_SWITCH INC\nFLAPS_SWITCH DEC\nGEAR_LE
                        var array = Regex.Split(_incomingData.ToString(), @"(?<=[\n])");
                        foreach (var command in array)
                        {
                            if (!command.EndsWith('\n')) continue;

                            DCSBIOS.Send(command);
                            DBEventManager.BroadCastPortStatus(SerialPort.PortName, SerialPortStatus.DCSBIOSCommandCalled, 0, command);
                            DBEventManager.BroadCastDataReceived(SerialPort.PortName, command.Length, StreamInterface.SerialPortRead);
                        }
                        // When all commands have been processed they can be removed from the buffer
                        foreach (var command in array)
                        {
                            if (!command.EndsWith('\n')) continue;

                            _incomingData.Replace(command, string.Empty);
                        }
                    }
                    catch (TimeoutException t)
                    {
                        var message =
                            $"{SerialPort.PortName} Timeout when reading from SerialPort. Message = {t.DecodeException()} \n\n->{_incomingData}<-";
                        Logger.Error(message);
                        DBEventManager.BroadCastPortStatus(SerialPort.PortName, SerialPortStatus.TimeOutError);
                    }
                    catch (IOException t)
                    {
                        var message =
                            $"{SerialPort.PortName} IOException when reading from SerialPort. Message = {t.Message} \n\n->{_incomingData}<-";
                        Logger.Error(message);
                        DBEventManager.BroadCastPortStatus(SerialPort.PortName, SerialPortStatus.IOError);
                    }
                    catch (Exception t)
                    {
                        var message =
                            $"{SerialPort.PortName} Exception when reading from SerialPort. Message = {t.Message} \n\n->{_incomingData}<-";
                        Logger.Error(message);
                        DBEventManager.BroadCastPortStatus(SerialPort.PortName, SerialPortStatus.Error);
                    }

                    break;
                }
            case SerialData.Eof:
                {
                    break;
                }
            default:
                {
                    var message = "Socket switch statement defaulted.";
                    Logger.Error(message);
                    DBEventManager.BroadCastPortStatus(SerialPort.PortName, SerialPortStatus.Error);
                    break;
                }
        }

        ReadFromSerialPortSemaphore.Release();
    }
}