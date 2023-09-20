using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ToolWindows
{
    public sealed class DwordBitCollection : List<DwordBitCollection.ObservableBit>, INotifyPropertyChanged
    {
        public sealed class ObservableBit : DefaultNotifyPropertyChanged
        {
            private bool _bit = false;
            public bool Bit { get => _bit; set => SetField(ref _bit, value); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public const int NumBitsWord = 16;
        public const int NumBitsDword = 32;

        public uint DwordValue
        {
            get
            {
                uint binaryValue = 0;
                for (var b = 0; b < NumBitsDword; ++b)
                    binaryValue |= this[b].Bit ? (1u << b) : 0;
                return binaryValue;
            }
            set
            {
                for (var b = 0; b < NumBitsDword; ++b)
                    this[b].Bit = ((value >> b) & 1) != 0;
            }
        }

        public ushort WordHiValue
        {
            get
            {
                ushort binaryValue = 0;
                for (var b = 0; b < NumBitsWord; ++b)
                    binaryValue |= (ushort)(this[b + NumBitsWord].Bit ? (1u << b) : 0);
                return binaryValue;
            }
            set
            {
                for (var b = 0; b < NumBitsWord; ++b)
                    this[b + NumBitsWord].Bit = ((value >> b) & 1) != 0;
            }
        }

        public ushort WordLoValue
        {
            get
            {
                ushort binaryValue = 0;
                for (var b = 0; b < NumBitsWord; ++b)
                    binaryValue |= (ushort)(this[b].Bit ? (1u << b) : 0);
                return binaryValue;
            }
            set
            {
                for (var b = 0; b < NumBitsWord; ++b)
                    this[b].Bit = ((value >> b) & 1) != 0;
            }
        }

        public DwordBitCollection()
        {
            for (var b = 0; b < NumBitsDword; ++b)
            {
                var observable = new ObservableBit();
                PropertyChangedEventManager.AddHandler(observable, BitValueChanged, nameof(observable.Bit));
                Add(observable);
            }
        }

        private void BitValueChanged(object sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(DwordValue));
            RaisePropertyChanged(nameof(WordHiValue));
            RaisePropertyChanged(nameof(WordLoValue));
        }

        private void RaisePropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public partial class FloatViewerControl : UserControl
    {
        public DwordBitCollection Bits { get; } = new DwordBitCollection();

        public FloatViewerControl()
        {
            DataContext = Bits;
            InitializeComponent();
        }

        public void InspectFloat(uint binaryValue, int floatBitSize)
        {
            Bits.DwordValue = binaryValue;
#pragma warning disable VSTHRD001
            Dispatcher.BeginInvoke((Action)(() => TabControl.SelectedIndex = floatBitSize == 32 ? 0 : 1), System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001
        }

        private void TextBoxCommitInputOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (BindingOperations.GetBindingExpression((TextBox)sender, TextBox.TextProperty) is BindingExpression binding)
                    binding.UpdateSource();
            }
        }
    }

    public sealed class DwordHexConverter : ValidationRule, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            "0x" + ((uint)value).ToString("X8");

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            TryParseHex((string)value, out var parsed) ? parsed : 0;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
            TryParseHex((string)value, out _) ? ValidationResult.ValidResult : new ValidationResult(false, "Expected a 32-bit hex number that starts with 0x");

        private bool TryParseHex(string input, out uint value)
        {
            value = 0;
            return input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && uint.TryParse(input.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }
    }

    public sealed class WordHexConverter : ValidationRule, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            "0x" + ((ushort)value).ToString("X4");

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            TryParseHex((string)value, out var parsed) ? parsed : 0;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
            TryParseHex((string)value, out _) ? ValidationResult.ValidResult : new ValidationResult(false, "Expected a 16-bit hex number that starts with 0x");

        private bool TryParseHex(string input, out ushort value)
        {
            value = 0;
            return input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && ushort.TryParse(input.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }
    }

    public sealed class DwordUintConverter : ValidationRule, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            ((uint)value).ToString();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            TryParseUint((string)value, out var parsed) ? parsed : 0;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
            TryParseUint((string)value, out _) ? ValidationResult.ValidResult : new ValidationResult(false, "Expected a 16-bit hex number that starts with 0x");

        private bool TryParseUint(string input, out uint value) =>
            uint.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public sealed class WordUintConverter : ValidationRule, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            ((ushort)value).ToString();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            TryParseUint((string)value, out var parsed) ? parsed : 0;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
            TryParseUint((string)value, out _) ? ValidationResult.ValidResult : new ValidationResult(false, "Expected a 16-bit hex number that starts with 0x");

        private bool TryParseUint(string input, out ushort value) =>
            ushort.TryParse(input.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public sealed class DwordBinConverter : ValidationRule, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            System.Convert.ToString((uint)value, 2).PadLeft(32, '0');

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            TryParseBin((string)value, out var parsed) ? parsed : 0;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
            TryParseBin((string)value, out _) ? ValidationResult.ValidResult : new ValidationResult(false, "Expected a 32-bit binary number");

        private bool TryParseBin(string input, out uint value)
        {
            try
            {
                value = System.Convert.ToUInt32(input, 2);
                return true;
            }
            catch
            {
                value = 0;
                return false;
            }
        }
    }

    public sealed class WordBinConverter : ValidationRule, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            System.Convert.ToString((ushort)value, 2).PadLeft(16, '0');

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            TryParseBin((string)value, out var parsed) ? parsed : 0;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
            TryParseBin((string)value, out _) ? ValidationResult.ValidResult : new ValidationResult(false, "Expected a 16-bit binary number");

        private bool TryParseBin(string input, out ushort value)
        {
            try
            {
                value = System.Convert.ToUInt16(input, 2);
                return true;
            }
            catch
            {
                value = 0;
                return false;
            }
        }
    }

    public sealed class DwordFloatConverter : ValidationRule, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            F32.FromBits((uint)value).ToString();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            F32.TryParse((string)value, out var parsed) ? parsed.Bits : 0;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
            F32.TryParse((string)value, out _) ? ValidationResult.ValidResult : new ValidationResult(false, "Expected a decimal number or either of 'NaN', 'Infinity', '-Infinity'");
    }

    public sealed class WordHalfConverter : ValidationRule, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            new F16((ushort)value).ToString();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            F16.TryParse((string)value, out var parsed) ? parsed.Bits : (ushort)0;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
            F16.TryParse((string)value, out _) ? ValidationResult.ValidResult : new ValidationResult(false, "Expected a decimal number or either of 'NaN', 'Infinity', '-Infinity'");
    }

    public sealed class DwordFloatHighPrecisionReadOnlyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            F32.FromBits((uint)value).ToStringPrecise();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public sealed class WordHalfHighPrecisionReadOnlyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            new F16((ushort)value).ToStringPrecise();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public sealed class DwordFloatSignConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int sign = ((int)(uint)value) >> F32.SignShift;
            return (string)parameter == "Encoded" ? sign.ToString() : (sign != 0 ? "-1" : "+1");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public sealed class WordHalfSignConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int sign = ((ushort)value) >> F16.SignShift;
            return (string)parameter == "Encoded" ? sign.ToString() : (sign != 0 ? "-1" : "+1");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public sealed class DwordFloatExponentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int exp = (int)(((int)(uint)value) & F32.BiasedExpMask) >> F32.BiasedExpShift;
            if (exp == 0)
                return (string)parameter == "Encoded" ? exp.ToString() : "2^-126 (denorm)";
            else
                return (string)parameter == "Encoded" ? exp.ToString() : "2^" + (exp - F32.ExpBias).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public sealed class WordHalfExponentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            short exp = (short)((((ushort)value) & F16.BiasedExpMask) >> F16.BiasedExpShift);
            if (exp == 0)
                return (string)parameter == "Encoded" ? exp.ToString() : "2^-14 (denorm)";
            else
                return (string)parameter == "Encoded" ? exp.ToString() : "2^" + (exp - F16.ExpBias).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public sealed class DwordFloatMantissaConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            const int exp1 = F32.ExpBias << F32.BiasedExpShift; // 2^0
            int exp = (int)(((int)(uint)value) & F32.BiasedExpMask) >> F32.BiasedExpShift;
            uint mant = (uint)(((int)(uint)value) & F32.MantissaMask);
            if (exp == 0)
                return (string)parameter == "Encoded" ? mant.ToString() : (-1.0 + BitConverter.ToSingle(BitConverter.GetBytes(exp1 | mant), 0)).ToString("G16") + " (denorm)";
            else
                return (string)parameter == "Encoded" ? mant.ToString() : ((double)BitConverter.ToSingle(BitConverter.GetBytes(exp1 | mant), 0)).ToString("G16");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public sealed class WordHalfMantissaConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            const short exp1 = F16.ExpBias << F16.BiasedExpShift; // 2^0
            short exp = (short)((((ushort)value) & F16.BiasedExpMask) >> F16.BiasedExpShift);
            ushort mant = (ushort)(((ushort)value) & F16.MantissaMask);
            if (exp == 0)
                return (string)parameter == "Encoded" ? mant.ToString() : (-1.0f + (float)new F16((ushort)(exp1 | mant))).ToString("G7") + " (denorm)";
            else
                return (string)parameter == "Encoded" ? mant.ToString() : ((float)new F16((ushort)(exp1 | mant))).ToString("G7");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
