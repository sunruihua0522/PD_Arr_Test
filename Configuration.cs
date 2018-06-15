namespace PLC_Test_PD_Array
{
    public class Configuration
    {
        public Configuration()
        {
            this.GPIBBoardNumber = 0;
            this.MaxChannel = 4;
            this.GPIBKeithley2400 = new int[4];
            this.GPIBKeysight8164B = 26;
            this.SweepStart = 1310;
            this.SweepStep = 0.1;
            this.SweepEnd = 1390;
            this.ITU = new double[4];
        }

        public int GPIBBoardNumber { get;  set;}
        public int MaxChannel { get; set; }
        public int[] GPIBKeithley2400 { get; set; }
        public int GPIBKeysight8164B { get; set; }
        public double SweepStart { get; set; }
        public double SweepStep { get; set; }
        public double SweepEnd { get; set; }
        public double OpticalPower { get; set; }
        public double[] ITU { get; set; }
    }
}
