using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed partial class NumberInput : UserControl, INotifyPropertyChanged, INotifyDataErrorInfo
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(uint), typeof(NumberInput),
                new FrameworkPropertyMetadata((uint)0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, DependencyPropertyChanged));
        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register(nameof(Step), typeof(uint), typeof(NumberInput),
                new FrameworkPropertyMetadata((uint)1, DependencyPropertyChanged));
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(uint), typeof(NumberInput),
                new FrameworkPropertyMetadata((uint)0, DependencyPropertyChanged));
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(uint), typeof(NumberInput),
                new FrameworkPropertyMetadata(uint.MaxValue, DependencyPropertyChanged));

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public uint Value
        {
            get => (uint)GetValue(ValueProperty);
            set
            {
                var newValue = value;
                if (newValue < Minimum)
                    newValue = Minimum;
                if (newValue > Maximum)
                    newValue = Maximum;

                SetValue(ValueProperty, newValue);
                if (!(uint.TryParse(RawValue, out var enteredValue) && enteredValue == newValue))
                    RawValue = newValue.ToString();
            }
        }

        public uint Step { get => (uint)GetValue(StepProperty); set => SetValue(StepProperty, value); }

        public uint Minimum { get => (uint)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

        public uint Maximum { get => (uint)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

        private string _rawValue = "0";
        public string RawValue
        {
            get => _rawValue;
            set
            {
                _rawValue = value;
                if (uint.TryParse(value, out var enteredValue) && enteredValue >= Minimum && enteredValue <= Maximum)
                {
                    Value = enteredValue;
                    _rawValueError = null;
                }
                else
                {
                    _rawValueError = $"Enter a numeric value between {Minimum} and {Maximum}";
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RawValue)));
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(RawValue)));
            }
        }

        private string _rawValueError = null;
        public bool HasErrors => _rawValueError != null;

        public IEnumerable GetErrors(string propertyName)
        {
            if (propertyName == nameof(RawValue) && _rawValueError != null)
                return new[] { _rawValueError };
            return Enumerable.Empty<string>();
        }

        public NumberInput()
        {
            InitializeComponent();
            // Do not change this.DataContext, it inherits from the control that uses NumberInput
            Root.DataContext = this;
        }

        private void Increment(object sender, RoutedEventArgs e)
        {
            Value += Step - Value % Step;
        }

        private void Decrement(object sender, RoutedEventArgs e)
        {
            var stepRem = Value % Step;
            var dec = stepRem == 0 ? Step : stepRem;
            Value = (Value > dec) ? Value - dec : 0;
        }

        private static void DependencyPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            var numberInput = (NumberInput)dependencyObject;
            switch (args.Property.Name)
            {
                case nameof(Value): numberInput.Value = (uint)args.NewValue; break;
                case nameof(Step): numberInput.Step = (uint)args.NewValue; break;
                case nameof(Minimum): numberInput.Minimum = (uint)args.NewValue; break;
                case nameof(Maximum): numberInput.Maximum = (uint)args.NewValue; break;
            }
        }

        private void ResetValueIfInvalid(object sender, RoutedEventArgs e)
        {
            // When the control loses focus, the displayed value should match the external binding (Value).
            RawValue = Value.ToString();
        }
    }
}
