using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PLC_Test_PD_Array
{
    /// <summary>
    /// ChipIDInput.xaml 的交互逻辑
    /// </summary>
    public partial class ChipIDInput : Window
    {
        public string ChipID { get; private set; }

        public ChipIDInput()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(txtChipID!=null)
            {
                int dwLength = txtChipID.Text.Length;
                if (dwLength != 14)
                {
                    MessageBox.Show("Wafer-ID长度不等于14，请确认后重新输入", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                bool bError = false;
                char[] strInfo = txtChipID.Text.ToCharArray();
                for (int i = 0; i < dwLength; i++)
                {
                    if (i == 0)
                    {
                        if (strInfo[i] < 'A' || strInfo[i] > 'Z')
                            bError = true;
                    }
                    else if (i == 6)
                    {
                        if (strInfo[i] != '-')
                            bError = true;
                    }
                    else if (i == 9)
                    {
                        if (strInfo[i] != '-')
                            bError = true;
                    }
                    else
                    {
                        if (strInfo[i] < '0' || strInfo[i] > '9')
                            bError = true;
                    }
                }
                if (bError)
                {
                    MessageBox.Show("Wafer-ID格式不正确，请确认后重新输入", "测试操作", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                    this.ChipID = txtChipID.Text.ToString();
                Close();
            }
       
            else
            {
                MessageBox.Show("plese input chipID!", "Caution", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
