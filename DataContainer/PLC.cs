using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GalaSoft.MvvmLight;

namespace PLC_Test_PD_Array.DataContainer
{
    public class PLC : ViewModelBase
    {
        public PLC(Configuration Config)
        {
            this.Channels = new ObservableCollection<PLCChannelRawData>();

            this.MaxChannel = Config.MaxChannel;

            for(int i = 0; i < Config.MaxChannel; i++)
            {
                this.Channels.Add(new PLCChannelRawData(i, Config.ITU[i], this, $"Channel{i + 1}"));
            }
        }


        int _maxchannel = 4;
        public int MaxChannel
        {
            get
            {
                return _maxchannel;
            }
            set
            {
                _maxchannel = value;
            }
        }

        public ObservableCollection<PLCChannelRawData> Channels { get; }


        public void AddReferenceData(double lambda, List<double> list)
        {
            if (list.Count != MaxChannel)
                throw new Exception("The count of returned data does not matches to the count of channel.");

            for (int i = 0; i < MaxChannel; i++)
            {
                this.Channels[i].AddReferencePoint(lambda, list[i]);
            }
        }

        public void AddTestedData(double lambda, List<double> list)
        {
            if(list.Count != MaxChannel)
                throw new Exception("The count of returned data points does not matches to the count of channel.");

            for (int i = 0; i < MaxChannel; i++)
            {
                this.Channels[i].AddThroughPLCPoint(lambda, list[i]);
            }
        }

        public void ClearReferenceData()
        {
            foreach (var ch in Channels)
            {
                ch.ClearReference();
            }
        }

        public void ClearTestedData()
        {
            foreach(var ch in Channels)
            {
                ch.ClearThroughPLC();
            }
        }

        public string ExportToCSV()
        {
            // validate row data
            //if(Channels[0])

            throw new NotImplementedException();
        }
    }
}
