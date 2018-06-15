using System;
using NationalInstruments.NI4882;

namespace PLC_Test_PD_Array.Instrument
{
    public class Keysight8164B : InstrumentBase
    {
        #region Variables


        #endregion

        #region Constructors

        public Keysight8164B(int BoardNumber, Address GPIBAddress):base(BoardNumber, GPIBAddress)
        {
        }

        public Keysight8164B(string SerialPort, int BaudRate):base(SerialPort, BaudRate)
        {

        }

        #endregion

        #region Methods

        public void SetOutput(bool Enable)
        {
            // outp 1/0
            if (Enable)
                Send("outp 1");
            else
                Send("outp 0");
        }

        public bool GetOutput()
        {
#if !FAKE_ME
            // outp?
            var ret = Read("outp?");
            if (ret.Contains("1"))
                return true;
            else
                return false;
#else
            return true;
#endif

        }

        /// <summary>
        /// Set the wavelength
        /// </summary>
        /// <param name="Wavelenght">unit in nm</param>
        public void SetWavelenght(double Wavelenght)
        {
            // sour0:wav xxxx.xxxxnm
            Send($"sour0:wav {Wavelenght}nm");
        }

        public double GetWavelenght()
        {
            // sour0:wav?, in m
            var ret = Read("sour0:wav?");
            if (double.TryParse(ret, out double v))
                return v;
            else
                throw new InvalidCastException($"Unable to convert the return string {ret} to wavelength.");

        }

        public void SetPower(double Power)
        {
            // sour0:pow xxx.xxxxmw
            Send($"sour0:pow {Power}mw");
        }

        public double GetPower()
        {
            // sour0:pow?
            var ret = Read("sour0:pow?");
            if (double.TryParse(ret, out double v))
                return v;
            else
                throw new InvalidCastException($"Unable to convert the return string {ret} to optical power.");
        }

        public override string ToString()
        {
            return $"KEISIGHT 8164B @ {port.ToString()}";
        }

        #endregion

        #region Private Methods

        void Send(string Command)
        {
#if !FAKE_ME
            if (port != null)
            {
                port.Write(Command);
            }
            else
            {
                throw new NotImplementedException();
            }
#else
            return;
#endif
        }

        string Read(string Command)
        {
#if !FAKE_ME
            if (port != null)
            {
                port.Write(Command);
                return port.Read();
            }
            else
            {
                throw new NotImplementedException();
            }
#else
            return "";
#endif
        }

        #endregion
    }
}
