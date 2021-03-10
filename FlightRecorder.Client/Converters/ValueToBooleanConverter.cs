using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace FlightRecorder.Client.Converters
{
    public class ValueToBooleanConverter : IValueConverter
    {
        public bool Reverse { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var values = parameter.ToString().Split("|").ToArray();
            return values.Contains(value.ToString()) ^ Reverse;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
