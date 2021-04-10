using System;
using System.Globalization;
using System.Windows.Data;

namespace FlightRecorder.Client.Converters
{
    public class RecordedAsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var title = values[0] as string;
            var fileName = values[1] as string;
            if (string.IsNullOrWhiteSpace(title))
            {
                title = "Unknown";
            }
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                title += " (" + fileName + ")";
            }
            return title;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
