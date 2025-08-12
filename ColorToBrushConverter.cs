using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace PUBGNetworkMonitor
{
    /// <summary>
    /// Converts Windows.UI.Color to Microsoft.UI.Xaml.Media.SolidColorBrush
    /// </summary>
    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Color color)
            {
                return new SolidColorBrush(color);
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}