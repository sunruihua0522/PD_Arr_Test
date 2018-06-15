using System;
using System.IO.Ports;

namespace PLC_Test_PD_Array.Instrument
{
    public class PortSerial : ICommunationPort, IDisposable
    {
        SerialPort port;

        public PortSerial(string PortName, int BaudRate, int Timeout)
        {
            port = new SerialPort(PortName, BaudRate);
            port.ReadTimeout = Timeout;
            port.WriteTimeout = Timeout;

            try
            {
                port.Open();
            }
            catch
            {
                port = null;
            }
        }

        public void Dispose()
        {
            if (port != null && port.IsOpen)
                port.Close();
        }

        public string Read()
        {
            return port.ReadLine();
        }

        public void Write(string Command)
        {
            port.WriteLine(Command);
        }

        public override string ToString()
        {
            return $"{port.PortName}";
        }
    }
}
