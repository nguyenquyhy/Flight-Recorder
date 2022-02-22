using System;
using System.Globalization;
using System.Windows.Data;

namespace FlightRecorder.Client.Converters
{
    public class BooleanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var stringValue = parameter as string;
            if (string.IsNullOrEmpty(stringValue)) return string.Empty;
            var tokens = stringValue.Split("|");
            return (value is bool b && b) ? tokens[0] : (tokens.Length > 1 ? tokens[1] : string.Empty);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
