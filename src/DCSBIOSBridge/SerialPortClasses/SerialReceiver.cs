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
internal class SerialReceiver : ISerialReceiver, IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private SemaphoreSlim ReadFromSerialPortSemaphore { get; } = new(1);

    public SerialPort SerialPort { get; set; }
    private readonly StringBuilder _incomingData = new();

    public void Dispose()
    {
        SerialPort.DataReceived -= ReceiveTextOverSerial;
        SerialPort.ErrorReceived -= SerialPortError;
        ReadFromSerialPortSemaphore?.Dispose();
    }

    public void ReceiveTextOverSerial(object sender, SerialDataReceivedEventArgs e)
    {
        ReadFromSerialPortSemaphore.Wait();

        switch (e.EventType)
        {
            case SerialData.Chars:
                {
                    try
                    {
                        Logger.Debug($"SerialPort.Buffer = {SerialPort.BytesToRead}");
                        var byteArray = new byte[SerialPort.BytesToRead];
                        
                        var bytesRead = SerialPort.BaseStream.Read(byteArray, 0, byteArray.Length);

                        _incomingData.Append(Common.UsedEncoding.GetString(byteArray, 0, bytesRead));

                        // FLAPS_SWITCH INC\nFLAPS_SWITCH DEC\nGEAR_LE
                        var array = Regex.Split(_incomingData.ToString(), @"(?<=[\n])");
                        foreach (var command in array)
                        {
                            if (command.Trim().Length == 0 || !command.EndsWith('\n')) continue;

                            DCSBIOS.Send(SerialPort.PortName, command);
                            DBEventManager.BroadCastSerialData(SerialPort.PortName, command.Length, StreamInterface.SerialPortRead);
                        }
                        // When all commands have been processed they can be removed from the buffer
                        foreach (var command in array)
                        {
                            if (command.Trim().Length == 0 || !command.EndsWith('\n')) continue;

                            _incomingData.Replace(command, string.Empty);
                        }
                    }
                    catch (TimeoutException t)
                    {
                        var message =
                            $"{SerialPort.PortName} TimeoutException when reading from SerialPort. Message = {t.DecodeException()} \n\n->{_incomingData}<-";
                        Logger.Error(t, message);
                        DBEventManager.BroadCastPortStatus(SerialPort.PortName, SerialPortStatus.TimeOutError);
                    }
                    catch (IOException t)
                    {
                        var message =
                            $"{SerialPort.PortName} IOException when reading from SerialPort. Message = {t.Message} \n\n->{_incomingData}<-";
                        Logger.Error(t, message);
                        DBEventManager.BroadCastPortStatus(SerialPort.PortName, SerialPortStatus.IOError);

                    }
                    catch (Exception t)
                    {
                        var message =
                            $"{SerialPort.PortName} Exception when reading from SerialPort. Message = {t.Message} \n\n->{_incomingData}<-";
                        Logger.Error(t, message);
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
                    Logger.Error("Socket switch statement defaulted.");
                    DBEventManager.BroadCastPortStatus(SerialPort.PortName, SerialPortStatus.Error);
                    break;
                }
        }

        ReadFromSerialPortSemaphore.Release();
    }
    
    public void SerialPortError(object sender, SerialErrorReceivedEventArgs e)
    {
        Logger.Error($"Serial port error: {SerialPort.PortName}. Error type: {e.EventType}");
    }
}