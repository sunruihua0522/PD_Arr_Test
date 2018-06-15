using System;
using System.Windows;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NationalInstruments.NI4882;
using Newtonsoft.Json;
using PLC_Test_PD_Array.DataContainer;
using PLC_Test_PD_Array.Instrument;
using Excel = Microsoft.Office.Interop.Excel;

namespace PLC_Test_PD_Array
{
    public class Controller
    {
        #region Variables

        public event EventHandler<double> OnSweepingProgressChanged;
        public event EventHandler<string> OnMessagesUpdated;

        CancellationTokenSource ctsSweep = new CancellationTokenSource();
        private DateTime testTime;



        #endregion

        #region Constructors

        public Controller()
        {
            try
            {
                this.Config = LoadConfig();
            }
            catch (Exception ex)
            {
                // if it's error to load the config file, set it to the default value
                Debug.WriteLine(ex.Message);

                Config = new Configuration();
            }

            PLC = new PLC(Config);
            TunableLaser = new Keysight8164B(Config.GPIBBoardNumber, new Address((byte)Config.GPIBKeysight8164B));
            SourceMeter = new Keithley2400[Config.MaxChannel];
            for (int i = 0; i < Config.MaxChannel; i++)
            {
                SourceMeter[i] = new Keithley2400(Config.GPIBBoardNumber, new Address((byte)Config.GPIBKeithley2400[i]));
            }
        }

        #endregion

        #region Properties

        public PLC PLC { get; }

        public Configuration Config { get; }

        public Keysight8164B TunableLaser { get; set; }

        public Keithley2400[] SourceMeter { get; set; }

        #endregion

        #region Private Methods

        /// <summary>
        /// Start to sweep the specified channel asynchronously
        /// </summary>
        /// <param name="Channel"></param>
        /// <param name="DataReadCallback"></param>
        /// <returns></returns>
        private async Task SweepAsync(Action<double, List<double>> DataReadCallback)
        {
            DateTime startTime = DateTime.Now;

            try
            {
                OnMessagesUpdated?.Invoke(this, $"Start to sweep from {Config.SweepStart}nm to {Config.SweepEnd}nm with step {Config.SweepStep}nm ...");

                IProgress<double> progressChanged = new Progress<double>(prog =>
                {
                    OnSweepingProgressChanged?.Invoke(this, prog);
                });

                IProgress<string> ProgressMessageUpdate = new Progress<string>(msg =>
                {
                    OnMessagesUpdated?.Invoke(this, msg);
                });

                // reset the cancellation token source
                ctsSweep = new CancellationTokenSource();

                await Task.Run(() =>
                {
                    // set the optical power
                    TunableLaser.SetPower(this.Config.OpticalPower);

                    // turn on the laser
                    TunableLaser.SetOutput(true);
                    if (TunableLaser.GetOutput() == false)
                        throw new Exception("Unable to turn on the laser.");

                    for (int i = 0; i < Config.MaxChannel; i++)
                    {
                        SourceMeter[i].SetDataElement(Keithley2400.EnumDataStringElements.CURR);
                        SourceMeter[i].SetMeasurementFunc(Keithley2400.EnumMeasFunc.ONCURR);
                        SourceMeter[i].SetSourceMode(Keithley2400.EnumSourceMode.VOLT);
                        SourceMeter[i].SetComplianceCurrent(Keithley2400.EnumComplianceLIMIT.REAL, 0.01);
                        // SourceMeter.SetMeasRangeOfAmps(Keithley2400.EnumMeasRangeAmps.R1MA);
                        SourceMeter[i].SetMeasRangeOfAmps(Keithley2400.EnumMeasRangeAmps.R1MA);
                        SourceMeter[i].SetVoltageSourceLevel(0);

                        SourceMeter[i].SetOutputState(true);
                        Thread.Sleep(100);
                        SourceMeter[i].GetMeasuredData(Keithley2400.EnumDataStringElements.CURR);
                        Thread.Sleep(100);
                        SourceMeter[i].GetMeasuredData(Keithley2400.EnumDataStringElements.CURR);
                        Thread.Sleep(100);
                        SourceMeter[i].GetMeasuredData(Keithley2400.EnumDataStringElements.CURR);
                        Thread.Sleep(100);
                        SourceMeter[i].GetMeasuredData(Keithley2400.EnumDataStringElements.CURR);
                        Thread.Sleep(100);
                        SourceMeter[i].GetMeasuredData(Keithley2400.EnumDataStringElements.CURR);
                    }


                    for (var lambda = Config.SweepStart; lambda <= Config.SweepEnd; lambda += Config.SweepStep)
                    {
                        lambda = Math.Round(lambda, 3);

                        //TODO Add data fetching process here
                        TunableLaser.SetWavelenght(lambda);

                        Thread.Sleep(20);

                        List<double> intensityList = new List<double>();

                        for (int i = 0; i < Config.MaxChannel; i++)
                        {

                            var intensity = SourceMeter[i].GetMeasuredData(Keithley2400.EnumDataStringElements.CURR).Current;
                            intensity *= 1000; // convert A to mA

                            intensityList.Add(intensity);
                        }

                        // add the point to collection on UI thread
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            DataReadCallback.Invoke(lambda, intensityList);
                        });

                        // calculate the progress in percent
                        var prog = (lambda - Config.SweepStart) / (Config.SweepEnd - Config.SweepStart);
                        progressChanged.Report(prog);


                        // check if the task should be canceled
                        if (ctsSweep.Token.IsCancellationRequested)
                        {
                            ProgressMessageUpdate.Report("The sweeping process was canceled.");
                            throw new OperationCanceledException("The sweeping process was canceled by user.");
                        }

                        //Thread.Sleep(10);
                    }
                });
            }
            catch (AggregateException ae)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var ex in ae.Flatten().InnerExceptions)
                {
                    sb.Append(ex.Message);
                    sb.Append("\r\n");
                }

                throw new Exception(sb.ToString());
            }
            finally
            {

#if !FAKE_ME
                // turn off the laser
                TunableLaser.SetOutput(false);

                for (int i = 0; i < Config.MaxChannel; i++)
                {
                    // turn off 2400
                    SourceMeter[i].SetOutputState(false);
                }
#endif

                OnMessagesUpdated?.Invoke(this, $"The sweeping process costs {(DateTime.Now - startTime).TotalSeconds}s");
            }

        }

        #endregion

        #region Methods

        /// <summary>
        /// Find the available instruments automatically
        /// </summary>
        /// <returns></returns>
        [Obsolete("The GPIB address must be specified.")]
        public async Task FindInstruments()
        {
            IProgress<string> ProgressMessageUpdate = new Progress<string>(msg =>
            {
                OnMessagesUpdated?.Invoke(this, msg);
            });

            await Task.Run(() =>
            {
                ProgressMessageUpdate.Report("Start to finding instruments ...");

                Board gpibBoard = new Board(Config.GPIBBoardNumber);

                // get all addresses
                gpibBoard.SendInterfaceClear();
                var addresses = gpibBoard.FindListeners();
                gpibBoard.SetRemoteWithLockout(addresses);

                ProgressMessageUpdate.Report($"{addresses.Count} instruments were found.");
                ProgressMessageUpdate.Report("Checking model of the instruments ...");

                // query device description from each address
                foreach (Address address in addresses)
                {
                    ProgressMessageUpdate.Report($"Querying the description of the instrument at {address} ...");

                    var dev = new Device(Config.GPIBBoardNumber, address.PrimaryAddress, address.SecondaryAddress);
                    dev.Write("*IDN?");
                    var ret = dev.ReadString();

                    ProgressMessageUpdate.Report($"{ret.TrimEnd('\n')}");

                    if (ret.Contains("8164B"))
                    {
                        this.TunableLaser = new Keysight8164B(Config.GPIBBoardNumber, address);
                        ProgressMessageUpdate.Report($"Keysight 8164B was found at {address}.");
                    }
                    else if (ret.Contains("2400"))
                    {
                        //this.SourceMeter = new Keithley2400(Config.GPIBBoardNumber, address);
                        ProgressMessageUpdate.Report($"Keithley 2400 was found at {address}.");
                    }
                }

#if FAKE_ME
                this.TunableLaser = new Keysight8164B(0, new Address(1));
                ProgressMessageUpdate.Report($"[DEBUG] Keysight 8164B was added.");
#endif

                List<InvalidOperationException> exceptions = new List<InvalidOperationException>();

                // check if all instruments were found
                if (this.SourceMeter == null)
                {
                    exceptions.Add(new InvalidOperationException("Unable to find Keithley 2400."));
                }
                else if (this.TunableLaser == null)
                {
                    exceptions.Add(new InvalidOperationException("Unable to find Keysight 81645B."));
                }

                if (exceptions.Count > 0)
                    throw new AggregateException(exceptions);
            });
        }

        /// <summary>
        /// Connect instruments defined in the config file
        /// </summary>
        /// <returns></returns>
        public async Task ConnectInstruments()
        {
            IProgress<string> ProgressMessageUpdate = new Progress<string>(msg =>
            {
                OnMessagesUpdated?.Invoke(this, msg);
            });

            await Task.Run(() =>
            {
                List<Exception> exceptions = new List<Exception>();

                ProgressMessageUpdate.Report("Connecting KEYSIGHT 8164B ...");

                // connect keysight 8164B
#if !FAKE_ME
                try
                {
                    if (TunableLaser.GetDescription().Contains("8164B"))
                    {
                        ProgressMessageUpdate.Report($"{TunableLaser} was found.");
                    }
                }
                catch (Exception ex)
                {
                    ProgressMessageUpdate.Report($"Unabel to find {TunableLaser}, {ex.Message}");
                    exceptions.Add(ex);
                }
#else
                ProgressMessageUpdate.Report($"{TunableLaser} was found.");
#endif
                int i = 1;
                foreach (var sm in this.SourceMeter)
                {
#if !FAKE_ME
                    // connect keithley 2400
                    try
                    {
                        ProgressMessageUpdate.Report("Connecting KEITHLEY 2400 ...");

                        if (sm.GetDescription().Contains("2400"))
                        {
                            ProgressMessageUpdate.Report($"{sm} was found.");
                            sm.SetDisplayTextState(1, true);
                            sm.SetDisplayTextMessage(1, $"PLC CH {i}");
                        }
                    }
                    catch (Exception ex)
                    {
                        ProgressMessageUpdate.Report($"Unabel to find {sm}, {ex.Message}");
                        exceptions.Add(ex);
                    }

                    i++;
#else
                    ProgressMessageUpdate.Report($"{sm} was found.");
#endif
                }

                if (exceptions.Count > 0)
                    throw new AggregateException(exceptions);
            });
        }

        /// <summary>
        /// Stop sweep process
        /// </summary>
        public void StopSweeping()
        {
            ctsSweep.Cancel();
        }
        /// <summary>
        /// save test result to excel
        /// </summary>
        /// <param name="chipID"></param>
        public void SaveTestResult(string chipID)
        {
            double dblWLMin = 2000.0, dblWLMax = -2000.0;
            double dblDiffWLMin = 2000.0, dblDiffWLMax = -2000.0;
            double dblILMinMin = 100.0, dblILMinMax = -100.0;
            double dblILMaxMin = 100.0, dblILMaxMax = -100.0;
            double dblRippleMin = 100.0, dblRippleMax = -100.0;
            double dblBW1dBMin = 100.0, dblBW1dBMax = -100.0;
            double dblBW3dBMin = 100.0, dblBW3dBMax = -100.0;
            double dblAXLeftMin = 100.0, dblAXLeftMax = -100.0;
            double dblAXRightMin = 100.0, dblAXRightMax = -100.0;
            double dblNXMin = 100.0, dblNXMax = -100.0;

            string strXlsName = string.Format("SU-{0}-{1}-{2}-{3}.xls", chipID.Substring(0, 9), chipID.Substring(9, 2).PadLeft(3, '0'), chipID.Substring(11, 2).PadLeft(3, '0'), "S03", "0000", testTime.ToString("yyyy-MM-dd-hh-mm"));
            string strXlsPath = Directory.GetCurrentDirectory() + "\\Data\\" + strXlsName;
            if (File.Exists(strXlsPath))
            {
                if (System.Windows.Forms.MessageBox.Show("文件已经存在，是否覆盖？", "保存测试结果", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                    return;
                else
                    File.Delete(strXlsPath);
            }
            string TemplatePath = Directory.GetCurrentDirectory() + "\\" + @"Data\ReportTmpl.xls";
            Excel.Application excelApp = new Excel.Application();
            Excel.Workbook excelWorkBook = excelApp.Workbooks.Open(TemplatePath);
            excelWorkBook.SaveAs(strXlsPath);

            Excel.Worksheet worksheet = excelWorkBook.Worksheets.get_Item("DataSheet");
            Excel.Range excelCell = worksheet.UsedRange;
            try
            {
                // string a = excelCell[1, 1].value;//读数据
                string value = chipID;
                excelCell[1, 3].value = value;
                value = testTime.ToString("yyyy/MM/dd HH:mm");
                excelCell[10 - 3, 8].value = value;
                int initialIndex = 22;
                int iPos = 0;
                for (int dwChannelIndex = 1; dwChannelIndex <= Config.MaxChannel; dwChannelIndex++)
                {
                    iPos = initialIndex + dwChannelIndex;
                    excelCell[iPos - 3, 1].value = dwChannelIndex.ToString();

                    excelCell[iPos - 3, 2].value = dwChannelIndex.ToString();

                    double dblWL = this.PLC.Channels[dwChannelIndex-1].MSR;
                    if (dblWL < dblWLMin)
                        dblWLMin = dblWL;
                    if (dblWL > dblWLMax)
                        dblWLMax = dblWL;
                    excelCell[iPos - 3, 3].value = dblWL.ToString("####.000");

                    double dblDiffWL = this.PLC.Channels[dwChannelIndex-1].DeltaLambda;
                    if (dblDiffWL < dblDiffWLMin)
                        dblDiffWLMin = dblDiffWL;
                    if (dblDiffWL > dblDiffWLMax)
                        dblDiffWLMax = dblDiffWL;
                    excelCell[iPos - 3, 4].value = dblDiffWL.ToString("####.000");

                    double dblILMin = this.PLC.Channels[dwChannelIndex-1].LossMin.Y;
                    if (dblILMin < dblILMinMin)
                        dblILMinMin = dblILMin;
                    if (dblILMin > dblILMinMax)
                        dblILMinMax = dblILMin;
                    excelCell[iPos - 3, 5].value = dblILMin.ToString("####.00");

                    double dblILMax = this.PLC.Channels[dwChannelIndex-1].LossMax.Y;
                    if (dblILMax < dblILMaxMin)
                        dblILMaxMin = dblILMax;
                    if (dblILMax > dblILMaxMax)
                        dblILMaxMax = dblILMax;
                    excelCell[iPos - 3, 6].value = dblILMax.ToString("####.00");

                    double dblRipple = this.PLC.Channels[dwChannelIndex-1].LossRipple;
                    if (dblRipple < dblRippleMin)
                        dblRippleMin = dblRipple;
                    if (dblRipple > dblRippleMax)
                        dblRippleMax = dblRipple;
                    excelCell[iPos - 3, 7].value = dblRipple.ToString("####.00");

                    double dblBW1dB = this.PLC.Channels[dwChannelIndex-1].PassBand_1dB.Item3;
                    if (dblBW1dB < dblBW1dBMin)
                        dblBW1dBMin = dblBW1dB;
                    if (dblBW1dB > dblBW1dBMax)
                        dblBW1dBMax = dblBW1dB;
                    excelCell[iPos - 3, 8].value = dblBW1dB.ToString("####.000");

                    double dblBW3dB = this.PLC.Channels[dwChannelIndex-1].PassBand_3dB.Item3;
                    if (dblBW3dB < dblBW3dBMin)
                        dblBW3dBMin = dblBW3dB;
                    if (dblBW3dB > dblBW3dBMax)
                        dblBW3dBMax = dblBW3dB;
                    excelCell[iPos - 3, 9].value = dblBW3dB.ToString("####.000");

                    double dblAXLeft = this.PLC.Channels[dwChannelIndex-1].AxN.Y;
                    if (dblAXLeft < dblAXLeftMin)
                        dblAXLeftMin = dblAXLeft;
                    if (dblAXLeft > dblAXLeftMax)
                        dblAXLeftMax = dblAXLeft;
                    excelCell[iPos - 3, 10].value = dblAXLeft.ToString("####.00");

                    double dblAXRight = this.PLC.Channels[dwChannelIndex-1].AxP.Y;
                    if (dblAXRight < dblAXRightMin)
                        dblAXRightMin = dblAXRight;
                    if (dblAXRight > dblAXRightMax)
                        dblAXRightMax = dblAXRight;
                    excelCell[iPos - 3, 11].value = dblAXRight.ToString("####.00");

                    double dblNX = this.PLC.Channels[dwChannelIndex-1].NX;
                    if (dblNX < dblNXMin)
                        dblNXMin = dblNX;
                    if (dblNX > dblNXMax)
                        dblNXMax = dblNX;
                    excelCell[iPos - 3, 12].value = dblNX.ToString("####.00");

                    excelCell[initialIndex - 1 - 3, 3].value = dblWLMin.ToString("####.000");
                    excelCell[initialIndex - 3, 3].value = dblWLMax.ToString("####.000");
                    excelCell[initialIndex - 1 - 3, 4].value = dblDiffWLMin.ToString("####.000");
                    excelCell[initialIndex - 3, 4].value = dblDiffWLMax.ToString("####.000");

                    excelCell[initialIndex - 1 - 3, 5].value = dblILMinMin.ToString("####.00");
                    excelCell[initialIndex - 3, 5].value = dblILMinMax.ToString("####.00");

                    excelCell[initialIndex - 1 - 3, 6].value = dblILMaxMin.ToString("####.00");
                    excelCell[initialIndex - 3, 6].value = dblILMaxMax.ToString("####.00");

                    excelCell[initialIndex - 1 - 3, 7].value = dblRippleMin.ToString("####.00");
                    excelCell[initialIndex - 3, 7].value = dblRippleMax.ToString("####.00");

                    excelCell[initialIndex - 1 - 3, 8].value = dblBW1dBMin.ToString("####.000");
                    excelCell[initialIndex - 3, 8].value = dblBW1dBMax.ToString("####.000");

                    excelCell[initialIndex - 1 - 3, 9].value = dblBW3dBMin.ToString("####.000");
                    excelCell[initialIndex - 3, 9].value = dblBW3dBMax.ToString("####.000");

                    excelCell[initialIndex - 1 - 3, 10].value = dblAXRightMin.ToString("####.00");
                    excelCell[initialIndex - 3, 10].value = dblAXRightMax.ToString("####.00");

                    excelCell[initialIndex - 1 - 3, 11].value = dblAXLeftMin.ToString("####.00");
                    excelCell[initialIndex - 3, 11].value = dblAXLeftMax.ToString("####.00");

                    excelCell[initialIndex - 1 - 3, 12].value = dblNXMin.ToString("####.00");
                    excelCell[initialIndex - 3, 12].value = dblNXMax.ToString("####.00");

                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                excelWorkBook.Save();
                excelWorkBook.Close();
                excelApp.Quit();
            }
        }

        public void SaveRawData(string chipID)
        {
            string strInfo = "";

            string strRawDataName = string.Format("SU-{0}-{1}-{2}-{3}.csv", chipID.Substring(0, 9), chipID.Substring(9, 2).PadLeft(3, '0'), chipID.Substring(11, 2).PadLeft(3, '0'), "S03", "0000", testTime.ToString("yyyy-MM-dd-hh-mm"));
            string strRawDataPath = Directory.GetCurrentDirectory() + "\\Data\\" + strRawDataName;

            if (File.Exists(strRawDataPath))
                File.Delete(strRawDataPath);

            StreamWriter writer = new StreamWriter(strRawDataPath, true);

            strInfo = "WL,";

            for (int Channel = 1; Channel <= this.PLC.MaxChannel; Channel++)
            {
                strInfo += string.Format("CH{0} Ref,CH{0} IL,CH{0} PLC,", Channel);
            }
            writer.WriteLine(strInfo);

            for (int pointIndex = 0; pointIndex < this.PLC.Channels[0].InsertionLoss.Count; pointIndex++)
            {
                strInfo = Math.Round(this.PLC.Channels[0].InsertionLoss[pointIndex].X, 3).ToString() + ",";
                for (int Channel = 1; Channel <= this.PLC.MaxChannel; Channel++)
                {
                    strInfo += Math.Round(this.PLC.Channels[Channel-1].Reference[pointIndex].Y, 2).ToString() + "," + Math.Round(this.PLC.Channels[Channel-1].InsertionLoss[pointIndex].Y, 2).ToString() + "," + Math.Round(this.PLC.Channels[Channel-1].ThroughPLC[pointIndex].Y, 2).ToString() + ",";

                }
                writer.WriteLine(strInfo);
            }

        }

        /// <summary>
        /// Start to sweep reference data
        /// </summary>
        /// <param name="Channel"></param>
        /// <returns></returns>
        public Task StartSweepReference()
        {
            PLC.ClearReferenceData();

#if FAKE_REF

            for(var lambda = Config.SweepStart; lambda <= Config.SweepEnd; lambda += Config.SweepStep)
            {
                lambda = Math.Round(lambda, 3);
                this.PLC.AddReferenceData(lambda, new List<double>(new double[] { 1.6, 1.6, 1.6, 1.6 }));      
            }

            return Task.Run(() => { });
#else

            return SweepAsync((lambda, list) =>
            {
#if !FAKE_ME
                this.PLC.AddReferenceData(lambda, list);

#else
                this.PLC.AddReferenceData(lambda, new List<double>(new double[] { 4, 4, 4, 4 }));
#endif
            });

#endif
        }

        /// <summary>
        /// Start to sweep PLC data
        /// </summary>
        /// <param name="Channel"></param>
        /// <returns></returns>
        public Task StartSweepThroughPLC()
        {
            PLC.ClearTestedData();
            testTime = DateTime.Now;

#if !FAKE_ME
            return SweepAsync((lambda, list) =>
            {
                this.PLC.AddTestedData(lambda, list);
            });

#else

            FakeDataGenerator.ReadRawData("fakedata.csv", out List<Point> data1, out List<Point> data2, out List<Point> data3, out List<Point> data4);
            this.PLC.Channels[0].InsertionLoss.AddRange(data1);
            this.PLC.Channels[1].InsertionLoss.AddRange(data2);
            this.PLC.Channels[2].InsertionLoss.AddRange(data3);
            this.PLC.Channels[3].InsertionLoss.AddRange(data4);

            return Task.Run(() => { });

#endif


        }

        /// <summary>
        ///  Load config from json file
        /// </summary>
        /// <returns></returns>
        public Configuration LoadConfig()
        {
            string json = "";

            try
            {
                json = File.ReadAllText("config.json");
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to load config file, {ex.Message}");
            }

            var cfg = JsonConvert.DeserializeObject<Configuration>(json);
            return cfg;
        }

        #endregion
    }
}
