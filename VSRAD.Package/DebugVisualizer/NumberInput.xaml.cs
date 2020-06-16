using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed partial class NumberInput : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(uint), typeof(NumberInput),
                new FrameworkPropertyMetadata((uint)0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PropertyChanged));
        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register(nameof(Step), typeof(uint), typeof(NumberInput),
                new FrameworkPropertyMetadata((uint)1, PropertyChanged));
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(uint), typeof(NumberInput),
                new FrameworkPropertyMetadata((uint)0, PropertyChanged));
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(uint), typeof(NumberInput),
                new FrameworkPropertyMetadata(uint.MaxValue, PropertyChanged));

        public event PropertyChangedEventHandler NotifyPropertyChanged;

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        { add => NotifyPropertyChanged += value; remove => NotifyPropertyChanged -= value; }

        public uint RawValue
        {
            get => Value;
            set => Value = (value < Minimum) ? Minimum : (value > Maximum) ? Maximum : value;
        }

        public uint Value
        {
            get => (uint)GetValue(ValueProperty);
            set
            {
                SetValue(ValueProperty, value);
                NotifyPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RawValue)));
            }
        }

        public uint Step { get => (uint)GetValue(StepProperty); set => SetValue(StepProperty, value); }

        public uint Minimum { get => (uint)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

        public uint Maximum { get => (uint)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

        public NumberInput()
        {
            InitializeComponent();
            // Do not change this.DataContext, it inherits from the control that uses NumberInput
            Root.DataContext = this;
        }

        private void Increment(object sender, RoutedEventArgs e) => RawValue += Step;

        private void Decrement(object sender, RoutedEventArgs e) => RawValue = (Value > Step) ? Value - Step : 0;

        private static void PropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
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
            // If the text is empty, reset it to the current value of the source property (RawValue)
            if (sender is TextBox textBox)
                BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty).UpdateTarget();
        }
    }
}
