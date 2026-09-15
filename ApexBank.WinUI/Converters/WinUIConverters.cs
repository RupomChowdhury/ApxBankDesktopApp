using System;
using System.Globalization;
using Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace ApexBank.WinUI.Converters;

public static class ColorHelper
{
    public static Color FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Colors.Transparent;
        hex = hex.Trim().TrimStart('#');
        byte a = 255;
        int pos = 0;
        if (hex.Length == 8)
        {
            a = byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber);
            pos = 2;
        }
        else if (hex.Length != 6)
        {
            return Colors.Transparent;
        }

        byte r = byte.Parse(hex.AsSpan(pos, 2), NumberStyles.HexNumber);
        byte g = byte.Parse(hex.AsSpan(pos + 2, 2), NumberStyles.HexNumber);
        byte b = byte.Parse(hex.AsSpan(pos + 4, 2), NumberStyles.HexNumber);
        return Color.FromArgb(a, r, g, b);
    }
}

public class AmountColorConverter : IValueConverter
{
    private static readonly SolidColorBrush EmeraldBrush = new(ColorHelper.FromHex("#059669"));
    private static readonly SolidColorBrush RoseBrush = new(ColorHelper.FromHex("#E11D48"));
    private static readonly SolidColorBrush NeutralBrush = new(ColorHelper.FromHex("#0F172A"));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is decimal amount)
        {
            return amount >= 0 ? EmeraldBrush : RoseBrush;
        }
        return NeutralBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class AmountPrefixConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is decimal amount)
        {
            return amount >= 0 ? $"+৳{amount:N2}" : $"-৳{Math.Abs(amount):N2}";
        }
        return "৳0.00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class EqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null || parameter == null) return Visibility.Collapsed;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase) 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class EqualityToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                return new SolidColorBrush(ColorHelper.FromHex(hex));
            }
            catch
            {
                // fallback
            }
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class TransactionCategoryToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string cat = value?.ToString() ?? "";
        cat = cat.Trim();

        string key = "IconReceipt";

        if (cat.Equals("Income", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Salary", StringComparison.OrdinalIgnoreCase) ||
            cat.Equals("Deposit", StringComparison.OrdinalIgnoreCase))
        {
            key = "IconIncome";
        }
        else if (cat.Equals("Transfer", StringComparison.OrdinalIgnoreCase) ||
                 cat.Contains("Transfer", StringComparison.OrdinalIgnoreCase) ||
                 cat.Contains("Apex Network", StringComparison.OrdinalIgnoreCase) ||
                 cat.Contains("APX", StringComparison.OrdinalIgnoreCase) ||
                 cat.Contains("RBank", StringComparison.OrdinalIgnoreCase))
        {
            key = "IconTransferRail";
        }
        else if (cat.Equals("Shopping", StringComparison.OrdinalIgnoreCase) ||
                 cat.Equals("Retail", StringComparison.OrdinalIgnoreCase))
        {
            key = "IconShopping";
        }
        else if (cat.Equals("Groceries", StringComparison.OrdinalIgnoreCase) ||
                 cat.Equals("Food", StringComparison.OrdinalIgnoreCase) ||
                 cat.Equals("Dining", StringComparison.OrdinalIgnoreCase))
        {
            key = "IconFoodDining";
        }
        else if (cat.Equals("Entertainment", StringComparison.OrdinalIgnoreCase) ||
                 cat.Equals("Movies", StringComparison.OrdinalIgnoreCase) ||
                 cat.Equals("Streaming", StringComparison.OrdinalIgnoreCase))
        {
            key = "IconEntertainment";
        }
        else if (cat.Equals("Transport", StringComparison.OrdinalIgnoreCase) ||
                 cat.Equals("Travel", StringComparison.OrdinalIgnoreCase) ||
                 cat.Equals("Fuel", StringComparison.OrdinalIgnoreCase))
        {
            key = "IconTransport";
        }
        else if (cat.Equals("Utilities", StringComparison.OrdinalIgnoreCase) ||
                 cat.Equals("Utility", StringComparison.OrdinalIgnoreCase) ||
                 cat.Contains("Electricity", StringComparison.OrdinalIgnoreCase) ||
                 cat.Contains("Gas", StringComparison.OrdinalIgnoreCase) ||
                 cat.Contains("WASA", StringComparison.OrdinalIgnoreCase) ||
                 cat.Contains("Internet", StringComparison.OrdinalIgnoreCase))
        {
            key = "IconUtilities";
        }
        else if (cat.Equals("Health", StringComparison.OrdinalIgnoreCase) ||
                 cat.Equals("Medical", StringComparison.OrdinalIgnoreCase) ||
                 cat.Equals("Pharmacy", StringComparison.OrdinalIgnoreCase))
        {
            key = "IconHealth";
        }
        else if (cat.Equals("Card", StringComparison.OrdinalIgnoreCase) ||
                 cat.Contains("Card", StringComparison.OrdinalIgnoreCase))
        {
            key = "IconCards";
        }

        if (Application.Current?.Resources?.ContainsKey(key) == true)
        {
            return Application.Current.Resources[key];
        }

        return Application.Current?.Resources?["IconReceipt"] ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class TransactionCategoryToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush EmeraldBrush = new(ColorHelper.FromHex("#10B981"));
    private static readonly SolidColorBrush CyanBrush = new(ColorHelper.FromHex("#06B6D4"));
    private static readonly SolidColorBrush IndigoBrush = new(ColorHelper.FromHex("#818CF8"));
    private static readonly SolidColorBrush AmberBrush = new(ColorHelper.FromHex("#F59E0B"));
    private static readonly SolidColorBrush PurpleBrush = new(ColorHelper.FromHex("#A855F7"));
    private static readonly SolidColorBrush BlueBrush = new(ColorHelper.FromHex("#3B82F6"));
    private static readonly SolidColorBrush OrangeBrush = new(ColorHelper.FromHex("#F97316"));
    private static readonly SolidColorBrush RoseBrush = new(ColorHelper.FromHex("#F43F5E"));
    private static readonly SolidColorBrush TealBrush = new(ColorHelper.FromHex("#14B8A6"));
    private static readonly SolidColorBrush SlateBrush = new(ColorHelper.FromHex("#94A3B8"));

    public object Convert(object value, Type targetType, object parameter, string language)
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

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
