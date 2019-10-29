using System;
using System.Windows;
using System.Windows.Controls;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed class PropertyPageEditorWrapper
    {
        public delegate object GetPropertyValueDelegate(PropertyPage propertyPage, Property property);
        public delegate void SetPropertyValueDelegate(PropertyPage propertyPage, Property property, object value);
        public delegate void UpdateDescriptionDelegate(string description);
        public delegate Options.ProfileOptions GetProfileOptionsDelegate();
        public delegate void ProfileNameChangedDelegate();

        private readonly Grid _propertyPageGrid;
        private readonly Macros.MacroEditor _macroEditor;
        private readonly GetPropertyValueDelegate _getValue;
        private readonly SetPropertyValueDelegate _setValue;
        private readonly UpdateDescriptionDelegate _updateDescription;
        private readonly GetProfileOptionsDelegate _getProfileOptions;
        private readonly ProfileNameChangedDelegate _profileNameChanged;

        private TextBox _profileNameBox = null;
        public string EditedProfileName
        {
            get => _profileNameBox?.Text;
        }

        public PropertyPageEditorWrapper(
            Grid propertyPageGrid,
            Macros.MacroEditor macroEditor,
            GetPropertyValueDelegate getValue,
            SetPropertyValueDelegate setValue,
            UpdateDescriptionDelegate updateDescription,
            GetProfileOptionsDelegate getProfileOptions,
            ProfileNameChangedDelegate profileNameChanged)
        {
            _propertyPageGrid = propertyPageGrid;
            _macroEditor = macroEditor;
            _getValue = getValue;
            _setValue = setValue;
            _updateDescription = updateDescription;
            _getProfileOptions = getProfileOptions;
            _profileNameChanged = profileNameChanged;
        }

        public void SetupPropertyPageGrid(PropertyPage selectedPage, string profileName, bool updateProfileName = false)
        {
            _propertyPageGrid.RowDefinitions.Clear();
            _propertyPageGrid.Children.Clear();

            var isGeneral = selectedPage.DisplayName == "General";
            if (isGeneral)
                SetupProfileName(profileName, updateProfileName);

            foreach (var property in selectedPage.Properties)
            {
                var nameControl = new TextBlock { Text = property.DisplayName, IsEnabled = false };
                nameControl.Margin = new Thickness(5);
                var valueControl = GetPropertyValueControl(selectedPage, property);
                valueControl.Margin = new Thickness(5);
                valueControl.GotFocus += (sender, args) => _updateDescription(property.FullDescription);
                valueControl.LostFocus += (sender, args) => _updateDescription("");

                _propertyPageGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                int propertyIndex = _propertyPageGrid.RowDefinitions.Count - 1;

                Grid.SetRow(nameControl, propertyIndex);
                Grid.SetColumn(nameControl, 0);

                Grid.SetRow(valueControl, propertyIndex);
                Grid.SetColumn(valueControl, 1);

                _propertyPageGrid.Children.Add(nameControl);
                _propertyPageGrid.Children.Add(valueControl);
            }
        }

        private void SetupProfileName(string profileName, bool updateName)
        {
            var nameControl = new TextBlock { Text = "Profile Name:" };
            nameControl.Margin = new Thickness(5);
            var valueControl = new TextBox
            {
                Text = updateName ? profileName : _profileNameBox?.Text ?? profileName
            };
            valueControl.Margin = new Thickness(5);
            valueControl.TextChanged += (s, e) => _profileNameChanged();
            _profileNameBox = valueControl;

            _propertyPageGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(nameControl, 0);
            Grid.SetColumn(nameControl, 0);

            Grid.SetRow(valueControl, 0);
            Grid.SetColumn(valueControl, 1);

            _propertyPageGrid.Children.Add(nameControl);
            _propertyPageGrid.Children.Add(valueControl);
        }

        private FrameworkElement GetPropertyValueControl(PropertyPage page, Property property)
        {
            var value = _getValue(page, property);
            switch (value)
            {
                case int intValue:
                    var intBox = new TextBox { Text = value.ToString() };
                    intBox.PreviewTextInput += (s, e) => e.Handled = !int.TryParse(e.Text, out _);
                    intBox.TextChanged += (s, e) => _setValue(page, property, int.TryParse(intBox.Text, out var res) ? res : 0);
                    return intBox;
                case bool boolValue:
                    var boolBox = new ComboBox();
                    boolBox.SelectionChanged += (s, e) => _setValue(page, property, boolBox.SelectedIndex == 0);
                    boolBox.Items.Add(property.TrueString);
                    boolBox.Items.Add(property.FalseString);
                    boolBox.SelectedItem = boolValue ? property.TrueString : property.FalseString;
                    return boolBox;
                case object _ when value.GetType().IsEnum:
                    var enumBox = new ComboBox();
                    var type = value.GetType();
                    enumBox.ItemsSource = Enum.GetValues(type);
                    enumBox.SelectedItem = value;
                    enumBox.SelectionChanged += (s, e) => _setValue(page, property, Enum.Parse(type, enumBox.SelectedItem.ToString()));
                    return enumBox;
                default:
                    var textBox = new TextBox { Text = value != null ? value.ToString() : "" };
                    textBox.TextChanged += (s, e) => _setValue(page, property, textBox.Text);
                    return property.Macro != null ? EditorWithMacroButton(textBox, property.Macro) : textBox;
            }
        }

        private FrameworkElement EditorWithMacroButton(TextBox editor, string macro)
        {
            var macroButton = new Button { Content = "Edit..." };
            macroButton.Click += async (sender, args) =>
            {
                try
                {
                    editor.Text = await _macroEditor.EditAsync(macro, editor.Text, _getProfileOptions());
                }
                catch (Macros.MacroEvaluationException e)
                {
                    Errors.ShowWarning(e.Message);
                }
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40, GridUnitType.Pixel) });
            Grid.SetRow(editor, 0);
            Grid.SetColumn(editor, 0);
            grid.Children.Add(editor);
            Grid.SetRow(macroButton, 0);
            Grid.SetColumn(macroButton, 1);
            grid.Children.Add(macroButton);
            return grid;
        }
    }
}
