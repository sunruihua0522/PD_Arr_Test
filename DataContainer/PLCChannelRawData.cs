using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace PLC_Test_PD_Array.DataContainer
{
    public class PLCChannelRawData
    {
        public PLCChannelRawData(int Channel, double ITU, PLC Parent, string Caption = "CH?")
        {
            this.Reference = new SweepedPointCollection();
            this.ThroughPLC = new SweepedPointCollection();
            this.InsertionLoss = new SweepedPointCollection();

            this.Channel = Channel;
            this.Caption = Caption;
            this.ITU = ITU;
            this.Parent = Parent;
        }

        public int Channel { get; }
        public string Caption { get; }
        public double ITU { get; }

        #region Properties

        public PLC Parent { get; }

        public SweepedPointCollection Reference { get; }
        public SweepedPointCollection ThroughPLC { get; }
        public SweepedPointCollection InsertionLoss { get; }

        public ICollectionView CurveReference
        {
            get
            {
                return CollectionViewSource.GetDefaultView(Reference);
            }
        }

        public ICollectionView CurveThroughPLC
        {
            get
            {
                return CollectionViewSource.GetDefaultView(ThroughPLC);
            }
        }

        public ICollectionView CurveInsertionLoss
        {
            get
            {
                return CollectionViewSource.GetDefaultView(InsertionLoss);
            }
        }



        #region Calculated Parameters

        /// <summary>
        /// Get the measured central wavelength
        /// </summary>
        public double MSR
        {
            get
            {
                if (InsertionLoss.Count == 0)
                    return double.NaN;
                else
                {
                    // get the point with minimum IL
                    var point = InsertionLoss.First(a => a.Y == InsertionLoss.Min(b => b.Y));

                    // get the points close to the point with minimum IL 
                    var adjacent = CalPassBand(point.X, 1);
                    return Math.Round((adjacent.Item1.X + adjacent.Item2.X) / 2, 3);
                }
            }
        }

        /// <summary>
        /// Get the differential of wavelength between ITU and MSR
        /// </summary>
        public double DeltaLambda
        {
            get
            {
                return Math.Abs(MSR - ITU);
            }
        }

        /// <summary>
        ///  Get the minimum of insertion loss
        /// </summary>
        public Point LossMin
        {
            get
            {
                if (InsertionLoss.Count == 0)
                    return new Point(double.NaN, double.NaN);
                else
                {
                    // select the points with point.x locates between ITU - 6.5nm, and ITU + 6.5nm
                    //var points = CalLossMinMaxInRange(ITU, 6.5, 6.5);
                    var points = CalLossMinMaxInRange(ITU, 1, 1);
                    return points.Item1;
                }
            }
        }


        /// <summary>
        /// Get the maximum of insertion loss
        /// </summary>
        public Point LossMax
        {
            get
            {
                if (InsertionLoss.Count == 0)
                    return new Point(double.NaN, double.NaN);
                else
                {
                    // select the points with point.x locates between ITU - 6.5nm, and ITU + 6.5nm
                    //var points = CalLossMinMaxInRange(ITU, 6.5, 6.5);
                    var points = CalLossMinMaxInRange(ITU, 1, 1);
                    return points.Item2;
                }
            }
        }

        /// <summary>
        /// Get the differential between LossMax and LossMin
        /// </summary>
        public double LossRipple
        {
            get
            {
                return Math.Abs(LossMax.Y - LossMin.Y);
            }
        }

        /// <summary>
        /// Get the 1dB passband
        /// </summary>
        public Tuple<Point, Point, double> PassBand_1dB
        {
            get
            {
                return CalPassBand(ITU, 1);

            }
        }

        /// <summary>
        /// Get the 3dB passband
        /// </summary>
        public Tuple<Point, Point, double> PassBand_3dB
        {
            get
            {
                return CalPassBand(ITU, 3);

            }
        }

        /// <summary>
        /// Get the Ax- of crosstalk
        /// </summary>
        public Point AxN
        {
            get
            {
                var adjacentCH = this.Channel - 1;

                if (adjacentCH < 0) // this is the first channel
                    return new Point(double.NaN, double.NaN);
                else
                {
                    //var points = CalLossMinMaxInRange(Parent.Channels[adjacentCH].ITU, 6.5, 6.5);
                    var points = CalLossMinMaxInRange(Parent.Channels[adjacentCH].ITU, 1, 1);
                    return points.Item1;
                }
            }
        }

        /// <summary>
        /// Get the Ax+ of crosstalk
        /// </summary>
        public Point AxP
        {
            get
            {
                var adjacentCH = this.Channel + 1;

                if (adjacentCH >= Parent.MaxChannel) // this is the last channel
                    return new Point(double.NaN, double.NaN);
                else
                {
                    //var points = CalLossMinMaxInRange(Parent.Channels[adjacentCH].ITU, 6.5, 6.5);
                    var points = CalLossMinMaxInRange(Parent.Channels[adjacentCH].ITU, 1, 1);
                    return points.Item1;
                }
            }
        }

        /// <summary>
        /// Get none crosstalk
        /// </summary>
        public double NX
        {
            get
            {
                var lossSum = 0.0;

                for (int i = 0; i < Parent.MaxChannel; i++)
                {
                    if(i < this.Channel - 1 || i > this.Channel + 1)
                    {
                        //var loss = CalLossMinMaxInRange(Parent.Channels[i].ITU, 6.5, 6.5).Item1.Y;
                        var loss = CalLossMinMaxInRange(Parent.Channels[i].ITU, 1, 1).Item1.Y;
                        loss = Math.Pow(10, loss / 10);
                        lossSum += loss;
                    }
                }

                return 10 * Math.Log10(lossSum);
            }
        }

        #endregion

        #endregion


        #region Private Methods

        Point FindPointByWavelength(double Wavelength)
        {
            return InsertionLoss.SkipWhile(a => a.X < Wavelength).ElementAt(0);
        }


        /// <summary>
        /// Calculate the passband in the range of ndB dropped from the loss of specified wavelength
        /// </summary>
        /// <param name="CentralWavelength">The wavelength of the reference point</param>
        /// <param name="DropdB"></param>
        /// <returns>
        /// Tuple.
        /// item1: point on the left side of ITU
        /// item2: point on the right side of ITU
        /// item3: passband
        /// </returns>
        Tuple<Point, Point, double> CalPassBand(double CentralWavelength, double DropdB)
        {
            // get the point adjacents to the ITU
            var pointRef = FindPointByWavelength(CentralWavelength);

            // the IL used to calculate passband
            var targeIL = pointRef.Y + DropdB;

            // get the points before the ITU
            var pointsBeforeITU = InsertionLoss.Reverse().SkipWhile(p => p.X > pointRef.X);

            // get the point locates at the left side of the ITU
            var pointLeftITU = pointsBeforeITU.SkipWhile(p => p.Y < targeIL).First();

            // get the points after the ITU
            var pointsAfterITU = InsertionLoss.SkipWhile(p => p.X < pointRef.X);

            // get the point locates at the right side of the ITU
            var pointRightITU = pointsAfterITU.Reverse().SkipWhile(p => p.Y > targeIL).First();

            
            
            return new Tuple<Point, Point, double>(pointLeftITU, pointRightITU, pointRightITU.X - pointLeftITU.X);
        }


        /// <summary>
        /// Calculate the passband within the specified wavelength range
        /// </summary>
        /// <param name="CentralWavelength"></param>
        /// <param name="BoundRight"></param>
        /// <param name="BoundLeft"></param>
        /// <returns>
        /// Tuple,
        /// item1: point with minimum loss
        /// item2: point with maximum loss
        /// </returns>
        Tuple<Point, Point> CalLossMinMaxInRange(double CentralWavelength, double BoundLeft, double BoundRight)
        {
            double wavLeft = CentralWavelength - BoundLeft;
            double waveRight = CentralWavelength + BoundRight;

            var range = from p in InsertionLoss
                        where p.X >= wavLeft && p.X <= waveRight
                        select p;

            var pointMin = range.First(p => p.Y == range.Min(a => a.Y));
            var pointMax= range.First(p => p.Y == range.Max(a => a.Y));


            return new Tuple<Point, Point>(pointMin, pointMax);
        }

        #endregion

        #region Methods

        public void AddReferencePoint(double Wavelength, double Intensity)
        {
            this.Reference.Add(new Point(Wavelength, Math.Abs(Intensity)));
        }

        public void ClearReference()
        {
            this.Reference.Clear();
            ClearThroughPLC();
        }

        public void AddThroughPLCPoint(double Wavelength, double Intensity)
        {
#if !FAKE_ME
            double intensityRef = double.NaN;
            try
            {
                intensityRef = Reference.Where(a => a.X == Wavelength).Select(b => { return b.Y; }).First();
            }
            catch
            {
                throw new Exception($"Unable to find the reference point at {Wavelength}nm.");
            }
            
            if (intensityRef is double.NaN)
                throw new Exception($"The reference intensity at {Wavelength}nm is NaN.");
            else
            {
                this.ThroughPLC.Add(new Point(Wavelength, Math.Abs(Intensity)));
                // Convert to dB
                double il = -10.0 * Math.Log10(Math.Abs(Intensity) / intensityRef);
                if (il >40)
                    il = 40;

                this.InsertionLoss.Add(new Point(Wavelength, il));
            }
#else
            this.ThroughPLC.Add(new Point(Wavelength, Intensity));
            double intensityRef = double.NaN;
            try
            {
                intensityRef = Reference.Where(a => a.X == Wavelength).Select(b => { return b.Y; }).First();
            }
            catch
            {
                throw new Exception($"Unable to find the reference point at {Wavelength}nm.");
            }
            double il = 10.0 * Math.Log10(Math.Abs(Intensity) / intensityRef);
            if (il < -40)
                il = -40;
            this.InsertionLoss.Add(new Point(Wavelength, Intensity));
#endif
        }

        public void ClearThroughPLC()
        {
            this.ThroughPLC.Clear();
            this.InsertionLoss.Clear();
        }

        #endregion
    }
}
