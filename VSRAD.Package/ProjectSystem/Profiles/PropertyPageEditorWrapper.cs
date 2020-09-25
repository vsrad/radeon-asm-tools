﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VSRAD.Package.Utils;

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
        private readonly Macros.DirtyProfileMacroEditor _macroEditor;
        private readonly GetPropertyValueDelegate _getValue;
        private readonly SetPropertyValueDelegate _setValue;
        private readonly UpdateDescriptionDelegate _updateDescription;
        private readonly GetProfileOptionsDelegate _getProfileOptions;

        private PropertyPage _selectedPage;

        public PropertyPageEditorWrapper(
            Grid propertyPageGrid,
            Macros.DirtyProfileMacroEditor macroEditor,
            GetPropertyValueDelegate getValue,
            SetPropertyValueDelegate setValue,
            UpdateDescriptionDelegate updateDescription,
            GetProfileOptionsDelegate getProfileOptions)
        {
            _propertyPageGrid = propertyPageGrid;
            _macroEditor = macroEditor;
            _getValue = getValue;
            _setValue = setValue;
            _updateDescription = updateDescription;
            _getProfileOptions = getProfileOptions;
            _propertyPageGrid.MouseMove += DisplayDescription;
            _propertyPageGrid.MouseLeave += (s, e) => _updateDescription("");
        }

        private void DisplayDescription(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.Source is UIElement element)
            {
                int index = Grid.GetRow(element);
                _updateDescription(_selectedPage.Properties[index].FullDescription);
            }
        }

        public void SetupPropertyPageGrid(PropertyPage selectedPage)
        {
            _selectedPage = selectedPage;

            _propertyPageGrid.RowDefinitions.Clear();
            _propertyPageGrid.Children.Clear();

            foreach (var property in selectedPage.Properties)
            {
                var nameControl = new TextBlock
                {
                    Text = property.DisplayName,
                    Height = 22.0,
                    Padding = new Thickness(0, 3, 0, 0),
                    Margin = new Thickness(5)
                };

                var valueControl = GetPropertyValueControl(selectedPage, property);
                valueControl.VerticalAlignment = VerticalAlignment.Center;
                valueControl.Margin = new Thickness(5);

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
                case bool boolValue when property.BinaryChoice != null:
                    var optBox = new ComboBox();
                    optBox.SelectionChanged += (s, e) => _setValue(page, property, optBox.SelectedIndex == 0);
                    optBox.Items.Add(property.BinaryChoice.Value.True);
                    optBox.Items.Add(property.BinaryChoice.Value.False);
                    optBox.SelectedItem = boolValue ? property.BinaryChoice.Value.True : property.BinaryChoice.Value.False;
                    return optBox;
                case bool boolValue:
                    var boolBox = new CheckBox() { IsChecked = boolValue };
                    boolBox.Checked += (s, e) => _setValue(page, property, true);
                    boolBox.Unchecked += (s, e) => _setValue(page, property, false);
                    return boolBox;
                case object _ when value.GetType().IsEnum:
                    var enumBox = new ComboBox();
                    var type = value.GetType();
                    enumBox.ItemsSource = Enum.GetValues(type);
                    enumBox.SelectedItem = value;
                    enumBox.SelectionChanged += (s, e) => _setValue(page, property, Enum.Parse(type, enumBox.SelectedItem.ToString()));
                    return enumBox;
                default:
                    var textBox = new TextBox { Text = value?.ToString() ?? "" };
                    textBox.TextChanged += (s, e) => _setValue(page, property, textBox.Text);
                    return property.Macro != null ? EditorWithMacroButton(textBox, property.Macro) : textBox;
            }
        }

        private FrameworkElement EditorWithMacroButton(TextBox editor, string macro)
        {
            var macroButton = new Button { Content = "Edit..." };
            //macroButton.Click += (sender, args) => VSPackage.TaskFactory.RunAsyncWithErrorHandling(async () =>
            //    editor.Text = await _macroEditor.EditAsync(macro, editor.Text, _getProfileOptions()));

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