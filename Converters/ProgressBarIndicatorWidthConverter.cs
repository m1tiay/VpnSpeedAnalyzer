using System;
using System.Globalization;
using System.Windows.Data;

namespace VpnSpeedAnalyzer.Converters
{
    /// <summary>
    /// Ширина заливки прогрессбара: (Value-Min)/(Max-Min) * ActualWidth.
    /// </summary>
    public sealed class ProgressBarIndicatorWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4)
                return 0.0;

            var aw = ToDouble(values[0]);
            if (aw <= 0 || double.IsNaN(aw))
                return 0.0;

            var v = ToDouble(values[1]);
            var min = ToDouble(values[2]);
            var max = ToDouble(values[3]);

            if (double.IsNaN(v) || double.IsNaN(min) || double.IsNaN(max) || max <= min)
                return 0.0;

            var ratio = (v - min) / (max - min);
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;

            return Math.Max(0, aw * ratio);
        }

        private static double ToDouble(object? o)
        {
            return o switch
            {
                null => double.NaN,
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                _ => double.TryParse(System.Convert.ToString(o, CultureInfo.InvariantCulture), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var x)
                    ? x
                    : double.NaN
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
