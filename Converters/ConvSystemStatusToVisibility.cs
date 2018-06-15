using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using static PLC_Test_PD_Array.ViewModel.MainViewModel;

namespace PLC_Test_PD_Array.Converters
{
    public class ConvSystemStatusToVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            SystemStatus status = (SystemStatus)value;
            if (status == SystemStatus.Sweeping)
                return Visibility.Visible;
            else
                return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
