using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace PUBGNetworkMonitor
{
    /// <summary>
    /// Converts boolean to brush for game server identification
    /// </summary>
    public class BoolToGameServerBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isGameServer)
            {
                return isGameServer
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 76, 175, 80))  // Green
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 158, 158, 158)); // Gray
            }
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 158, 158, 158));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean to text for game server identification
    /// </summary>
    public class BoolToGameServerTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isGameServer)
            {
                return isGameServer ? "GAME" : "OTHER";
            }
            return "OTHER";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}