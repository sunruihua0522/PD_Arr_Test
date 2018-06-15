using System;
using System.Globalization;
using System.Windows.Data;
using static PLC_Test_PD_Array.ViewModel.MainViewModel;

namespace PLC_Test_PD_Array.Converters
{
    public class ConvSystemStatusToButtonEnable : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var param = parameter.ToString();

            SystemStatus status = (SystemStatus)value;

            if (param == "" || param == "normal")
            {
                if (status == SystemStatus.Idle)
                    return true;
                else
                    return false;
            }
            else if(param == "reverse")
            {
                if (status == SystemStatus.Idle)
                    return false;
                else
                    return true;
            }
            else
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
