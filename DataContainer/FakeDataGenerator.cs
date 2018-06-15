using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace PLC_Test_PD_Array.DataContainer
{
    public class FakeDataGenerator
    {
        static public bool ReadRawData(string strFilePathName, out List<Point> IL1, out List<Point> IL2, out List<Point> IL3, out List<Point> IL4)
        {
            IL1 = new List<Point>();
            IL2 = new List<Point>();
            IL3 = new List<Point>();
            IL4 = new List<Point>();

            double m_pdwWave;
          
            int lineNbr = 0;

            FileStream fs = new FileStream(strFilePathName, FileMode.Open);
            using (StreamReader reader = new StreamReader(fs))
            {
                int dwIndex = 0;

                string[] lineElems;
                // read the 1st line
                lineElems = reader.ReadLine().Split(',');
                lineNbr++;
                lineElems = reader.ReadLine().Split(',');
                int lineElemLen = lineElems.Length;
                if (lineElemLen <= 7)
                    return false;

                try
                {
                    do
                    {
                        lineNbr++;


                        m_pdwWave = double.Parse(lineElems[0]);

                        IL1.Add(new Point(m_pdwWave, -double.Parse(lineElems[1])));
                        IL2.Add(new Point(m_pdwWave, -double.Parse(lineElems[3])));
                        IL3.Add(new Point(m_pdwWave, -double.Parse(lineElems[5])));
                        IL4.Add(new Point(m_pdwWave, -double.Parse(lineElems[7])));

                        dwIndex++;
                        if (dwIndex >= 7501)
                            break;

                    }
                    while ((lineElems = reader.ReadLine().Split(',')) != null);

                }
                catch (Exception er)
                {

                    string errMsg = String.Format("Invalid line in  file: '{0}', line {1}",
                        strFilePathName, lineNbr);
                    throw new Exception(errMsg, er);
                }


            }

            return true;

        }

        
    }
}
