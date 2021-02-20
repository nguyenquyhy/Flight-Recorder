using System;
using System.Globalization;
using System.Windows.Data;

namespace FlightRecorder.Client.Converters
{
    public class AdditionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null)
            {
                if (value is int number && int.TryParse(parameter.ToString(), out var increment))
                {
                    return number + increment;
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
