using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using NationalInstruments.NI4882;

namespace PLC_Test_PD_Array.Instrument
{
    //TODO The Voltage Source, Current Source, Voltage Sensor, Current Sensor should be packaged into classes

    /// <summary>
    /// Class of Keithley 2400
    /// Contains the low-level operation functions in this class, and it's ready to bind to the view
    /// The default unit in this class is A/V/Ohm
    /// </summary>
    public class Keithley2400 : InstrumentBase
    {
        #region Definitions

        const double PROT_AMPS_DEF = 0.000105; // default compliance of current is 105uA
        const double PROT_AMPS_MIN = 0.00000105; // minimum compliance of current is 1.05uA
        const double PROT_AMPS_MAX = 1.05; // maximum compliance of current is 1.05A

        const double PROT_VOLT_DEF = 21; // default compliance of voltage is 21V
        const double PROT_VOLT_MIN = 0.21; // minimum compliance of voltage is 210mV
        const double PROT_VOLT_MAX = 210; // maximum compliance of voltage is 210V

        const double MEAS_SPEED_DEF = 1; // default measurement speed is 1 to fit 60Hz power line cycling

        public enum EnumInOutTerminal
        {
            FRONT, REAR
        }

        /// <summary>
        /// Measurement Source
        /// </summary>
        public enum EnumMeasFunc
        {
            OFFALL,
            ONVOLT,
            ONCURR,
            ONRES
        }

        /// <summary>
        /// Output Source 
        /// </summary>
        public enum EnumSourceMode { VOLT, CURR, MEM }

        /// <summary>
        /// Mode of Output Source
        /// </summary>
        public enum EnumSourceWorkMode { FIX, LIST, SWP }

        /// <summary>
        /// Range of Output Source
        /// </summary>
        public enum EnumSourceRange { REAL, UP, DOWN, MAX, MIN, DEFAULT, AUTO }

        /// <summary>
        /// Options of compliance setting
        /// </summary>
        public enum EnumComplianceLIMIT { DEFAULT, MAX, MIN, REAL }

        /// <summary>
        /// Options of which measurement result to be read
        /// </summary>
        public enum EnumReadCategory { VOLT = 0, CURR }

        /// <summary>
        /// Valid current measurement range
        /// </summary>
        public enum EnumMeasRangeAmps
        {
            AUTO = 0,
            R1UA,
            R10UA,
            R100UA,
            R1MA,
            R10MA,
            R100MA,
            R1A
        }

        public enum EnumMeasRangeVolts
        {
            AUTO = 0,
            R200MV,
            R2V,
            R21V
        }

        /// <summary>
        /// Elements contained in the data string for commands :FETCh/:READ/:MEAS/:TRAC:DATA
        /// </summary>
        [Flags]
        public enum EnumDataStringElements
        {
            VOLT = 0x1,
            CURR = 0x2,
            RES = 0x4,
            TIME = 0x8,
            STAT = 0x10,
            ALL = VOLT | CURR | RES | TIME | STAT
        }

        /// <summary>
        /// see page 355 of the manual for the definitions of each bit
        /// </summary>
        [Flags]
        public enum EnumOperationStatus
        {
            OFLO = 0x1,
            FILTER = 0x2,
            FRONTREAR = 0x4,
            CMPL = 0x8,
            OVP = 0x10,
            MATH = 0x20,
            NULL = 0x40,
            LIMITS = 0x80,
            LIMITRET0 = 0x100,
            LIMITRET1 = 0x200,
            AUTOOHMS = 0x400,
            VMEAS = 0x800,
            IMEAS = 0x1000,
            RMEAS = 0x2000,
            VSOUR = 0x4000,
            ISOUR = 0x8000,
            RANGECMPL = 0x10000,
            OFFSETCMPS = 0x20000,
            CONTRACTFAIL = 0x40000,
            TESTRET0 = 0x80000,
            TESTRET1 = 0x100000,
            TESTRET2 = 0x200000,
            RMTSENSE = 0x400000,
            PULSEMODE = 0x800000
        }

        public enum AmpsUnit
        {
            uA,
            mA,
            A
        }

        public enum VoltsUnit
        {
            uV,
            mV,
            V
        }
        #endregion

        #region Variables

        public struct MeasuredData
        {
            public double Voltage;
            public double Current;
            public double Resistance;
            public double Timestamp;
            public UInt32 Status;
        }

        #endregion

        #region Constructor

        public Keithley2400(int BoardNumber, Address GPIBAddress) : base(BoardNumber, GPIBAddress)
        {

        }

        public Keithley2400(string SerialPort, int BaudRate):base(SerialPort, BaudRate)
        {

        }

        #endregion

        #region Properties



        #endregion

        #region Public Methods

        /// <summary>
        /// Set the SourceMeter to V-Source Mode
        /// </summary>
        public void SetToVoltageSource()
        {
            SetOutputState(false);
            SetMeasurementFunc(EnumMeasFunc.ONCURR);
            SetSourceMode(EnumSourceMode.VOLT);
            SetRangeOfVoltageSource(EnumSourceRange.AUTO);

            // only return current measurement value under V-Source
            SetDataElement(EnumDataStringElements.CURR | EnumDataStringElements.STAT);
        }

        /// <summary>
        /// Set the SourceMeter to I-Source Mode
        /// </summary>
        public void SetToCurrentSource()
        {
            SetOutputState(false);
            SetMeasurementFunc(Keithley2400.EnumMeasFunc.ONVOLT);
            SetSourceMode(Keithley2400.EnumSourceMode.CURR);
            SetRangeOfCurrentSource(Keithley2400.EnumSourceRange.AUTO);

            // only return voltage measurement value under I-Source
            SetDataElement(EnumDataStringElements.VOLT | EnumDataStringElements.STAT);
        }

        #endregion

        #region Appropriative Methods of Keithley 2400

        #region Common

        public void SetOutputState(bool IsEnabled)
        {
            if (IsEnabled)
            {
                Send("OUTP ON");
            }
            else
            {
                Send("OUTP OFF");
            }
        }

        public bool GetOutputState()
        {
            var ret = Read("OUTP?");
            if (bool.TryParse(ret, out bool r))
                return r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }

        /// <summary>
        /// Set which In/Out terminal is valid
        /// </summary>
        /// <param name="Terminal">Front panel / Rear panel</param>
        public void SetInOutTerminal(EnumInOutTerminal Terminal)
        {
            switch (Terminal)
            {
                case EnumInOutTerminal.FRONT:
                    Send(":ROUT:TERM FRON");
                    break;

                case EnumInOutTerminal.REAR:
                    Send(":ROUT:TERM REAR");
                    break;
            }
        }

        public EnumInOutTerminal GetInOutTerminal()
        {
            var ret = Read(":ROUT:TERM?");

            if (ret.Contains("FRON"))
                return EnumInOutTerminal.FRONT;
            else if (ret.Contains("REAR"))
                return EnumInOutTerminal.REAR;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }

        public MeasuredData GetMeasuredData(EnumDataStringElements Elements)
        {

            MeasuredData result = new MeasuredData();
            result.Voltage = double.NaN;
            result.Current = double.NaN;
            result.Resistance = double.NaN;
            result.Timestamp = double.NaN;
            result.Status = UInt32.MinValue;

            var ret = Read(":READ?");

            if (Elements == EnumDataStringElements.ALL)
            {
                var retArray = ret.Split(',');
                // get voltage
                if (double.TryParse(retArray[0], out double v))
                    result.Voltage = v;

                // get current
                if (double.TryParse(retArray[1], out double c))
                    result.Current = c;

                // get resistance
                if (double.TryParse(retArray[2], out double r))
                    result.Resistance = r;

                // get timestamps
                if (double.TryParse(retArray[3], out double t))
                    result.Timestamp = t;

                // get status
                if (UInt32.TryParse(retArray[4], out UInt32 s))
                    result.Status = s;
            }
            else if(Elements == EnumDataStringElements.VOLT)
            {
                // get voltage
                if (double.TryParse(ret, out double v))
                    result.Voltage = v;
            }
            else if (Elements == EnumDataStringElements.CURR)
            {
                // get current
                if (double.TryParse(ret, out double c))
                    result.Current = c;
            }
            else if (Elements == EnumDataStringElements.RES)
            {
                // get resistance
                if (double.TryParse(ret, out double r))
                    result.Resistance = r;
            }
            else if (Elements == EnumDataStringElements.TIME)
            {
                // get timestamps
                if (double.TryParse(ret, out double t))
                    result.Timestamp = t;
            }
            else if (Elements == EnumDataStringElements.STAT)
            {
                // get status
                if (UInt32.TryParse(ret, out UInt32 s))
                    result.Status = s;
            }


            return result;

        }

        public override string ToString()
        {
            return $"KEITHLEY 2400 @ {port.ToString()}";
        }

        #endregion

        #region Format Subsystem
        /// <summary>
        /// Set the elements valid while executing :Read/etc. commands
        /// </summary>
        /// <param name="Elements"></param>
        public void SetDataElement(EnumDataStringElements Elements)
        {
            List<string> elemlsit = new List<string>();

            if (Elements.HasFlag(EnumDataStringElements.VOLT))
                elemlsit.Add(EnumDataStringElements.VOLT.ToString());

            if (Elements.HasFlag(EnumDataStringElements.CURR))
                elemlsit.Add(EnumDataStringElements.CURR.ToString());

            if (Elements.HasFlag(EnumDataStringElements.RES))
                elemlsit.Add(EnumDataStringElements.RES.ToString());

            if (Elements.HasFlag(EnumDataStringElements.TIME))
                elemlsit.Add(EnumDataStringElements.TIME.ToString());

            if (Elements.HasFlag(EnumDataStringElements.STAT))
                elemlsit.Add(EnumDataStringElements.STAT.ToString());

            if (elemlsit.Count == 0)
                throw new ArgumentException(string.Format("the null elemtents passed, {0}", new StackTrace().GetFrame(0).ToString()));
            else
            {
                string arg = String.Join(",", elemlsit.ToArray());
                Send(string.Format("FORM:ELEM {0}", arg));

                //this.DataStringElements = Elements;
            }
        }
        #endregion

        #region Sense1 Subsystem

        public void SetMeasurementSpeed(double Nplc)
        {
            Send(string.Format(":SENS:CURR:NPLC {0}", Nplc));
        }

        public double GetMeasurementSpeed()
        {
            var ret = Read(":SENS:CURR:NPLC?");
            if (double.TryParse(ret, out double r))
                return r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));

        }

        public void SetMeasurementFunc(EnumMeasFunc MeasFunc)
        {
            switch (MeasFunc)
            {
                case EnumMeasFunc.OFFALL:
                    Send(":SENS:FUNC:OFF:ALL");
                    break;

                case EnumMeasFunc.ONCURR:
                    Send(":SENS:FUNC:OFF:ALL;:SENS:FUNC:ON \"CURR\"");
                    break;

                case EnumMeasFunc.ONVOLT:
                    Send(":SENS:FUNC:OFF:ALL;:SENS:FUNC:ON \"VOLT\"");
                    break;

                case EnumMeasFunc.ONRES:
                    Send(":SENS:FUNC:OFF:ALL;:SENS:FUNC:ON \"RES\"");
                    break;
            }
        }

        public EnumMeasFunc GetMeasurementFunc()
        {
            //CURR:DC
            var ret = Read(":SENS:FUNC?");

            if (ret == "")
                return EnumMeasFunc.OFFALL;
            else if (ret.Contains("CURR"))
                return EnumMeasFunc.ONCURR;
            else if (ret.Contains("VOLT"))
                return EnumMeasFunc.ONVOLT;
            else if (ret.Contains("CURR"))
                return EnumMeasFunc.ONRES;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));

        }

        public void SetMeasRangeOfAmps(EnumMeasRangeAmps Range)
        {
            switch (Range)
            {
                case EnumMeasRangeAmps.AUTO:
                    Send(":SENS:CURR:RANG:AUTO ON");
                    break;

                default:
                    Send(string.Format(":SENS:CURR:RANG {0}", this.ConvertMeasRangeAmpsToDouble(Range)));
                    break;
            }

            GetMeasRangeOfAmps();
        }

        public EnumMeasRangeAmps GetMeasRangeOfAmps()
        {
            var ret = Read("SENS:CURR:RANGE:AUTO?");
            if (ret.Contains("1"))
            {
                return EnumMeasRangeAmps.AUTO;
            }
            else
            {

                ret = Read(":SENS:CURR:RANG?");

                if (double.TryParse(ret, out double r))
                    return ConvertDoubleToMeasRangeAmps(r);
                else
                    throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
            }
        }

        public void SetMeasRangeOfVolts(EnumMeasRangeVolts Range)
        {
            switch (Range)
            {
                case EnumMeasRangeVolts.AUTO:
                    Send(":SENS:VOLT:RANG:AUTO ON");
                    break;

                default:
                    Send(string.Format(":SENS:VOLT:RANG {0}", this.ConvertMeasRangeVoltToDouble(Range)));
                    break;
            }

            GetMeasRangeOfVolts();
        }

        public EnumMeasRangeVolts GetMeasRangeOfVolts()
        {
            var ret = Read("SENS:VOLT:RANGE:AUTO?");
            if (ret.Contains("1"))
            {
                return EnumMeasRangeVolts.AUTO;
            }
            else
            {
                ret = Read(":SENS:VOLT:RANG?");

                if (double.TryParse(ret, out double r))
                    return ConvertDoubleToMeasRangeVolt(r);
                else
                    throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
            }
        }

        #endregion

        #region Source Subsystem

        public void SetSourceMode(EnumSourceMode Mode)
        {
            switch (Mode)
            {
                case EnumSourceMode.CURR:
                    Send(":SOUR:FUNC CURR");
                    break;

                case EnumSourceMode.VOLT:
                    Send(":SOUR:FUNC VOLT");
                    break;

                case EnumSourceMode.MEM:
                    Send(":SOUR:FUNC MEM");
                    break;
            }
        }

        public EnumSourceMode GetSourceMode()
        {
            var ret = Read(":SOUR:FUNC?");

            if (ret.Contains("CURR"))
                return EnumSourceMode.CURR;
            else if (ret.Contains("VOLT"))
                return EnumSourceMode.VOLT;
            else if (ret.Contains("MEM"))
                return EnumSourceMode.MEM;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }

        public void SetSourcingModeOfCurrentSource(EnumSourceWorkMode Mode)
        {
            switch (Mode)
            {
                case EnumSourceWorkMode.FIX:
                    Send(":SOUR:CURR:MODE FIX");
                    break;

                case EnumSourceWorkMode.LIST:
                    Send(":SOUR:CURR:MODE LIST");
                    break;

                case EnumSourceWorkMode.SWP:
                    Send(":SOUR:CURR:MODE SWP");
                    break;
            }
        }

        public void SetSourcingModeOfVoltageSource(EnumSourceWorkMode Mode)
        {
            switch (Mode)
            {
                case EnumSourceWorkMode.FIX:
                    Send(":SOUR:VOLT:MODE FIX");
                    break;

                case EnumSourceWorkMode.LIST:
                    Send(":SOUR:VOLT:MODE LIST");
                    break;

                case EnumSourceWorkMode.SWP:
                    Send(":SOUR:VOLT:MODE SWP");
                    break;
            }
        }

        public void SetRangeOfCurrentSource(EnumSourceRange Range, double Real = -1)
        {
            switch (Range)
            {
                case EnumSourceRange.AUTO:
                    Send(":SOUR:CURR:RANG:AUTO 1");
                    break;

                case EnumSourceRange.DEFAULT:
                    Send(":SOUR:CURR:RANG DEF");
                    break;

                case EnumSourceRange.DOWN:
                    Send(":SOUR:CURR:RANG DOWN");
                    break;

                case EnumSourceRange.UP:
                    Send(":SOUR:CURR:RANG UP");
                    break;

                case EnumSourceRange.MIN:
                    Send(":SOUR:CURR:RANG MIN");
                    break;

                case EnumSourceRange.MAX:
                    Send(":SOUR:CURR:RANG MAX");
                    break;

                case EnumSourceRange.REAL:
                    Send(":SOUR:CURR:RANG " + ((decimal)Real).ToString());
                    break;
            }
        }

        public void SetRangeOfVoltageSource(EnumSourceRange Range, double Real = -1)
        {
            switch (Range)
            {
                case EnumSourceRange.AUTO:
                    Send(":SOUR:VOLT:RANG:AUTO 1");
                    break;

                case EnumSourceRange.DEFAULT:
                    Send(":SOUR:VOLT:RANG DEF");
                    break;

                case EnumSourceRange.DOWN:
                    Send(":SOUR:VOLT:RANG DOWN");
                    break;

                case EnumSourceRange.UP:
                    Send(":SOUR:VOLT:RANG UP");
                    break;

                case EnumSourceRange.MIN:
                    Send(":SOUR:VOLT:RANG MIN");
                    break;

                case EnumSourceRange.MAX:
                    Send(":SOUR:VOLT:RANG MAX");
                    break;

                case EnumSourceRange.REAL:
                    Send(":SOUR:VOLT:RANG " + ((decimal)Real).ToString());
                    break;
            }
        }

        public void SetComplianceCurrent(EnumComplianceLIMIT Cmpl, double Real = -1)
        {
            switch (Cmpl)
            {
                case EnumComplianceLIMIT.DEFAULT:
                    Send(":SENS:CURR:PROT DEF");
                    break;

                case EnumComplianceLIMIT.MIN:
                    Send(":SENS:CURR:PROT MIN");
                    break;

                case EnumComplianceLIMIT.MAX:
                    Send(":SENS:CURR:PROT MAX");
                    break;

                case EnumComplianceLIMIT.REAL:
                    Send(":SENS:CURR:PROT " + Real.ToString("F7"));
                    break;
            }

            GetComplianceCurrent();
        }

        public double GetComplianceCurrent()
        {
            var ret = Read(":SENS:CURR:PROT?");

            if (double.TryParse(ret, out double r))
                return r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }

        public void SetComplianceVoltage(EnumComplianceLIMIT Cmpl, double Real = -1)
        {
            switch (Cmpl)
            {
                case EnumComplianceLIMIT.DEFAULT:
                    Send(":SENS:VOLT:PROT DEF");
                    break;

                case EnumComplianceLIMIT.MIN:
                    Send(":SENS:VOLT:PROT MIN");
                    break;

                case EnumComplianceLIMIT.MAX:
                    Send(":SENS:VOLT:PROT MAX");
                    break;

                case EnumComplianceLIMIT.REAL:
                    Send(":SENS:VOLT:PROT " + ((decimal)Real).ToString());
                    break;
            }

            GetComplianceVoltage();
        }

        public double GetComplianceVoltage()
        {
            var ret = Read(":SENS:VOLT:PROT?");

            if (double.TryParse(ret, out double r))
                return r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }

        public void SetVoltageSourceLevel(double Voltage)
        {
            Send(":SOUR:VOLT:LEV " + Voltage.ToString());
        }

        public double GetVoltageSourceLevel()
        {
            var ret = Read(":SOUR:VOLT:LEV?");

            if (double.TryParse(ret, out double r))
                return r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }

        public void SetCurrentSourceLevel(double Current)
        {
            Send(":SOUR:CURR:LEV " + Current.ToString());
        }

        public double GetCurrentSourceLevel()
        {
            var ret = Read(":SOUR:CURR:LEV?");

            if (double.TryParse(ret, out double r))
                return r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));

        }
        #endregion

        #region System Subsystem

        /// <summary>
        /// Remove the SourceMeter from the remote state and enables the operation of front panel keys
        /// </summary>
        public void SetExitRemoteState()
        {
            Send(":SYST:LOC");
        }

        /// <summary>
        /// Control beeper
        /// </summary>
        /// <param name="Frequency"></param>
        /// <param name="Duration"></param>
        public void SetBeep(double Frequency, double Duration)
        {
            if (Frequency < 65 || Frequency > 2000000)
            {
                throw new ArgumentException(string.Format("the argument frequency is invalid, {0}", new StackTrace().GetFrame(0).ToString()));
            }
            else if (Duration < 0 || Duration > 7.9)
            {
                throw new ArgumentException(string.Format("the argument duration is invalid, {0}", new StackTrace().GetFrame(0).ToString()));
            }
            else
            {

                Send(string.Format(":SYST:BEEP {0},{1}", Frequency, Duration));
            }
        }

        /// <summary>
        /// Enable or disable beeper
        /// </summary>
        /// <param name="IsEnabled"></param>
        public void SetBeeperState(bool IsEnabled)
        {
            if (IsEnabled)
                Send(":SYST:BEEP:STAT ON");
            else
                Send(":SYST:BEEP:STAT OFF");
        }

        #endregion

        #region Display Subsystem
        /// <summary>
        /// Enable or disable the front display circuitry, when disabled, the instrument works at a higher speed
        /// </summary>
        /// <param name="IsEnabled"></param>
        public void SetDisplayCircuitry(bool IsEnabled)
        {
            if (IsEnabled)
                Send(":DISP:ENAB ON");
            else
                Send(":DISP:ENAB OFF");
        }

        /// <summary>
        /// Enable or disable the text message display function
        /// </summary>
        /// <param name="WinId"></param>
        /// <param name="IsEnabled"></param>
        public void SetDisplayTextState(int WinId, bool IsEnabled)
        {
            if (WinId != 1 && WinId != 2)
            {
                throw new ArgumentOutOfRangeException(string.Format("window id is error, {0}", new StackTrace().GetFrame(0).ToString()));
            }
            else
            {
                if (IsEnabled)
                    Send(string.Format(":DISP:WIND{0}:TEXT:STAT ON", WinId));
                else
                    Send(string.Format(":DISP:WIND{0}:TEXT:STAT OFF", WinId));
            }
        }

        /// <summary>
        /// Set the message displayed on the screen
        /// </summary>
        /// <param name="WinId"></param>
        /// <param name="Message"></param>
        public void SetDisplayTextMessage(int WinId, string Message)
        {
            if (WinId != 1 && WinId != 2)
            {
                throw new ArgumentOutOfRangeException(string.Format("window id is error, {0}", new StackTrace().GetFrame(0).ToString()));
            }
            else
            {
                if (WinId == 1 && Message.Length > 20)
                {
                    throw new ArgumentOutOfRangeException(string.Format("the length of message on top display can not be greater then 20, {0}", new StackTrace().GetFrame(0).ToString()));
                }
                else if (WinId == 2 && Message.Length > 32)
                {
                    throw new ArgumentOutOfRangeException(string.Format("the length of message on bottom display can not be greater then 32, {0}", new StackTrace().GetFrame(0).ToString()));
                }
                else
                {
                    Send(string.Format(":DISP:WIND{0}:TEXT:DATA \"{1}\"", WinId, Message));
                }
            }
        }
        #endregion 

        #endregion

        #region Private Methods
        void Send(string Command)
        {
            port.Write(Command);
        }

        string Read(string Command)
        {
            port.Write(Command);
            return port.Read();
        }
        
        double ConvertMeasRangeAmpsToDouble(EnumMeasRangeAmps Range)
        {
            double real = 1.05 * Math.Pow(10, ((int)Range) - 7);
            return real;
        }

        EnumMeasRangeAmps ConvertDoubleToMeasRangeAmps(double Range)
        {
            var digital = Range / 1.05;
            digital = Math.Log10(digital);
            return (EnumMeasRangeAmps)(digital + 7);
        }

        double ConvertMeasRangeVoltToDouble(EnumMeasRangeVolts Range)
        {
            double real = 2.1 * Math.Pow(10, ((int)Range - 2));
            return real;
        }

        EnumMeasRangeVolts ConvertDoubleToMeasRangeVolt(double Range)
        {
            var digital = Range / 2.1;
            digital = Math.Log10(digital);
            return (EnumMeasRangeVolts)(digital + 2);
        }
        
#endregion
    }
}
