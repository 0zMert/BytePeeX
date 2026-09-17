using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace BytePeeX.Converters
{
    public class LevelToIndentConverter : IValueConverter
    {
        public double IndentStep { get; set; } = 18.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                return new Thickness(level * IndentStep + 4, 0, 4, 0);
            }
            return new Thickness(4, 0, 4, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class PercentToWidthConverter : IValueConverter
    {
        public double MaxWidth { get; set; } = 80.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percent)
            {
                double width = (percent / 100.0) * MaxWidth;
                return Math.Clamp(width, 0, MaxWidth);
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BooleanToVisibilityInvertedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class LanguageActiveBgConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new((Color)ColorConverter.ConvertFromString("#2563EB"));
        private static readonly SolidColorBrush InactiveBrush = new((Color)ColorConverter.ConvertFromString("#1E293B"));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? ActiveBrush : InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class LanguageActiveFgConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new((Color)ColorConverter.ConvertFromString("#FFFFFF"));
        private static readonly SolidColorBrush InactiveBrush = new((Color)ColorConverter.ConvertFromString("#94A3B8"));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? ActiveBrush : InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
