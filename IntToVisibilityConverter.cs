using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace PUBGNetworkMonitor
{
    /// <summary>
    /// Converts integer count to Visibility for empty state handling
    /// </summary>
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int count)
            {
                // Show empty state when count is 0, hide it when count > 0
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}