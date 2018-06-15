using NationalInstruments.NI4882;

namespace PLC_Test_PD_Array.Instrument
{
    public class PortGPIB : ICommunationPort
    {
        Device dev;

        public PortGPIB(int BoardNumber, Address GPIBAddress, TimeoutValue Timeout)
        {
            dev = new Device(BoardNumber, GPIBAddress)
            {
                IOTimeout = Timeout
            };
        }

        public string Read()
        {
            return dev.ReadString();
        }

        public void Write(string Command)
        {
            dev.Write(Command);
        }

        public override string ToString()
        {
            return $"GPIB{dev.PrimaryAddress}";
        }
    }
}
