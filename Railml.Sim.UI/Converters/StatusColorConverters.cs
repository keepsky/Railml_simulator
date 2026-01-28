using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Railml.Sim.UI.Converters
{
    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Brushes.Transparent;
            string status = value.ToString();
            
            switch (status)
            {
                case "Occupied": return Brushes.LightPink;
                case "Unoccupied": return Brushes.LightGreen;
                case "Normal": return Brushes.LightBlue;
                case "Reverse": return Brushes.Orange;
                case "Moving": return Brushes.Yellow;
                default: return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class SignalColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Brushes.Black;
            string status = value.ToString();
            
            switch (status)
            {
                case "Proceed":
                case "Green": return Brushes.Green;
                case "Stop":
                case "Red": return Brushes.Red;
                case "Caution":
                case "Yellow": return Brushes.Orange;
                default: return Brushes.Gray;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
