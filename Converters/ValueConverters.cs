using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ApexBank.Converters;

public class AmountColorConverter : IValueConverter
{
    private static readonly SolidColorBrush EmeraldBrush = new((Color)ColorConverter.ConvertFromString("#059669"));
    private static readonly SolidColorBrush RoseBrush = new((Color)ColorConverter.ConvertFromString("#E11D48"));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal amount)
        {
            return amount >= 0 ? EmeraldBrush : RoseBrush;
        }
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class AmountPrefixConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal amount)
        {
            return amount >= 0 ? $"+৳{amount:N2}" : $"-৳{Math.Abs(amount):N2}";
        }
        return "৳0.00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class EqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return Visibility.Collapsed;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase) 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class EqualityToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch
            {
                // fallback
            }
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class TransactionCategoryToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string cat = value?.ToString() ?? "";
        cat = cat.Trim();

        if (cat.Equals("Income", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Salary", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Deposit", StringComparison.OrdinalIgnoreCase))
        {
            return Application.Current.FindResource("IconIncome");
        }

        if (cat.Equals("Transfer", StringComparison.OrdinalIgnoreCase) ||
            cat.Contains("Transfer", StringComparison.OrdinalIgnoreCase) ||
            cat.Contains("Apex Network", StringComparison.OrdinalIgnoreCase) ||
            cat.Contains("APX", StringComparison.OrdinalIgnoreCase) ||
            cat.Contains("RBank", StringComparison.OrdinalIgnoreCase))
        {
            return Application.Current.FindResource("IconTransferRail");
        }

        if (cat.Equals("Shopping", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Retail", StringComparison.OrdinalIgnoreCase))
        {
            return Application.Current.FindResource("IconShopping");
        }

        if (cat.Equals("Groceries", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Food", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Dining", StringComparison.OrdinalIgnoreCase))
        {
            return Application.Current.FindResource("IconFoodDining");
        }

        if (cat.Equals("Entertainment", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Movies", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Streaming", StringComparison.OrdinalIgnoreCase))
        {
            return Application.Current.FindResource("IconEntertainment");
        }

        if (cat.Equals("Transport", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Travel", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Fuel", StringComparison.OrdinalIgnoreCase))
        {
            return Application.Current.FindResource("IconTransport");
        }

        if (cat.Equals("Utilities", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Utility", StringComparison.OrdinalIgnoreCase) ||
            cat.Contains("Electricity", StringComparison.OrdinalIgnoreCase) ||
            cat.Contains("Gas", StringComparison.OrdinalIgnoreCase) ||
            cat.Contains("WASA", StringComparison.OrdinalIgnoreCase) ||
            cat.Contains("Internet", StringComparison.OrdinalIgnoreCase))
        {
            return Application.Current.FindResource("IconUtilities");
        }

        if (cat.Equals("Health", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Medical", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Pharmacy", StringComparison.OrdinalIgnoreCase))
        {
            return Application.Current.FindResource("IconHealth");
        }

        if (cat.Equals("Card", StringComparison.OrdinalIgnoreCase) ||
            cat.Contains("Card", StringComparison.OrdinalIgnoreCase))
        {
            return Application.Current.FindResource("IconCards");
        }

        return Application.Current.FindResource("IconReceipt");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class TransactionCategoryToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush EmeraldBrush = new((Color)ColorConverter.ConvertFromString("#10B981"));
    private static readonly SolidColorBrush CyanBrush = new((Color)ColorConverter.ConvertFromString("#06B6D4"));
    private static readonly SolidColorBrush IndigoBrush = new((Color)ColorConverter.ConvertFromString("#818CF8"));
    private static readonly SolidColorBrush AmberBrush = new((Color)ColorConverter.ConvertFromString("#F59E0B"));
    private static readonly SolidColorBrush PurpleBrush = new((Color)ColorConverter.ConvertFromString("#A855F7"));
    private static readonly SolidColorBrush BlueBrush = new((Color)ColorConverter.ConvertFromString("#3B82F6"));
    private static readonly SolidColorBrush OrangeBrush = new((Color)ColorConverter.ConvertFromString("#F97316"));
    private static readonly SolidColorBrush RoseBrush = new((Color)ColorConverter.ConvertFromString("#F43F5E"));
    private static readonly SolidColorBrush TealBrush = new((Color)ColorConverter.ConvertFromString("#14B8A6"));
    private static readonly SolidColorBrush SlateBrush = new((Color)ColorConverter.ConvertFromString("#94A3B8"));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string cat = value?.ToString() ?? "";
        cat = cat.Trim();

        if (cat.Equals("Income", StringComparison.OrdinalIgnoreCase) || cat.Equals("Salary", StringComparison.OrdinalIgnoreCase) || cat.Equals("Deposit", StringComparison.OrdinalIgnoreCase))
            return EmeraldBrush;

        if (cat.Equals("Transfer", StringComparison.OrdinalIgnoreCase) || cat.Contains("Transfer", StringComparison.OrdinalIgnoreCase) || cat.Contains("APX", StringComparison.OrdinalIgnoreCase) || cat.Contains("RBank", StringComparison.OrdinalIgnoreCase) || cat.Contains("Apex", StringComparison.OrdinalIgnoreCase))
            return CyanBrush;

        if (cat.Equals("Shopping", StringComparison.OrdinalIgnoreCase) || cat.Equals("Retail", StringComparison.OrdinalIgnoreCase))
            return IndigoBrush;

        if (cat.Equals("Groceries", StringComparison.OrdinalIgnoreCase) || cat.Equals("Food", StringComparison.OrdinalIgnoreCase) || cat.Equals("Dining", StringComparison.OrdinalIgnoreCase))
            return AmberBrush;

        if (cat.Equals("Entertainment", StringComparison.OrdinalIgnoreCase) || cat.Equals("Movies", StringComparison.OrdinalIgnoreCase) || cat.Equals("Streaming", StringComparison.OrdinalIgnoreCase))
            return PurpleBrush;

        if (cat.Equals("Transport", StringComparison.OrdinalIgnoreCase) || cat.Equals("Travel", StringComparison.OrdinalIgnoreCase))
            return BlueBrush;

        if (cat.Equals("Utilities", StringComparison.OrdinalIgnoreCase) || cat.Contains("Electricity", StringComparison.OrdinalIgnoreCase) || cat.Contains("Gas", StringComparison.OrdinalIgnoreCase) || cat.Contains("WASA", StringComparison.OrdinalIgnoreCase) || cat.Contains("Internet", StringComparison.OrdinalIgnoreCase))
            return OrangeBrush;

        if (cat.Equals("Health", StringComparison.OrdinalIgnoreCase) || cat.Equals("Medical", StringComparison.OrdinalIgnoreCase))
            return RoseBrush;

        if (cat.Contains("Card", StringComparison.OrdinalIgnoreCase))
            return TealBrush;

        return SlateBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
