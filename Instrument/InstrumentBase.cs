using NationalInstruments.NI4882;

namespace PLC_Test_PD_Array.Instrument
{
    public class InstrumentBase
    {
        protected ICommunationPort port;

        public InstrumentBase(string PortName, int BaudRate)
        {
            port = new PortSerial(PortName, BaudRate, 3000);
        }

        public InstrumentBase(int GPIBBoardNumber, Address GPIBAddress)
        {
            port = new PortGPIB(GPIBBoardNumber, GPIBAddress, TimeoutValue.T10s);
        }


        public string GetDescription()
        {
            port.Write("*IDN?");
            return port.Read();
        }
    }
}
