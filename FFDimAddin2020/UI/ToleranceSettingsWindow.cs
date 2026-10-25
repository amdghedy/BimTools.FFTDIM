using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using BimTools.FFTDIM.Core;

namespace BimTools.FFTDIM.UI
{
    /// <summary>
    /// Editor for tolerance rules. Values are metres; a limit of 0 disables that bound.
    /// On OK the (mutated) list is persisted by the calling command via ToleranceRuleStore.
    /// </summary>
    public class ToleranceSettingsWindow : Window
    {
        private readonly List<ToleranceRule> _rules;
        private readonly DataGrid _grid;

        public ToleranceSettingsWindow(List<ToleranceRule> rules)
        {
            _rules = rules;

            Title = L.T("TolTitle");
            Width = 780;
            Height = 430;
            MinWidth = 640;
            MinHeight = 320;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF6, 0xF8));
            if (L.RightToLeft) FlowDirection = FlowDirection.RightToLeft;

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var info = new TextBlock
            {
                Text = "Each rule checks every dimension whose references include elements of the selected scope. " +
                       "Values are in metres. Set a limit to 0 to disable that bound. A dimension is flagged once " +
                       "per violated rule. Defaults are layout envelopes, not code requirements — adapt them to " +
                       "the standard governing your project.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(info, 0);
            root.Children.Add(info);

            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                ItemsSource = _rules,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD4, 0xD9)),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var nameColumn = new DataGridTextColumn { Header = "Rule name", Binding = NewBinding("Name"), IsReadOnly = true, Width = new DataGridLength(280) };
            var minColumn = new DataGridTextColumn { Header = "Min (m)", Binding = NewBinding("MinMeters"), Width = new DataGridLength(80) };
            var maxColumn = new DataGridTextColumn { Header = "Max (m)", Binding = NewBinding("MaxMeters"), Width = new DataGridLength(80) };
            var scopeColumn = new DataGridTextColumn { Header = "Scope", Binding = NewBinding("Scope"), IsReadOnly = true, Width = new DataGridLength(100) };
            var enabledColumn = new DataGridCheckBoxColumn { Header = "On", Binding = NewBinding("Enabled"), Width = new DataGridLength(50) };

            _grid.Columns.Add(nameColumn);
            _grid.Columns.Add(minColumn);
            _grid.Columns.Add(maxColumn);
            _grid.Columns.Add(scopeColumn);
            _grid.Columns.Add(enabledColumn);
            Grid.SetRow(_grid, 1);
            root.Children.Add(_grid);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var restore = new Button
            {
                Content = "Restore defaults",
                MinWidth = 120,
                MinHeight = 26,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 24, 0)
            };
            restore.Click += OnRestoreDefaults;
            var ok = MakeButton("OK");
            ok.IsDefault = true;
            ok.Click += OnAccept;
            var cancel = MakeButton("Cancel");
            cancel.IsCancel = true;
            buttons.Children.Add(restore);
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            Content = root;
        }

        private static Binding NewBinding(string path)
        {
            return new Binding(path)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
        }

        private static Button MakeButton(string label)
        {
            return new Button
            {
                Content = label,
                MinWidth = 100,
                MinHeight = 26,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(8, 3, 8, 3)
            };
        }

        private void OnRestoreDefaults(object sender, RoutedEventArgs args)
        {
            List<ToleranceRule> defaults = ToleranceRuleStore.DefaultRules();
            _rules.Clear();
            foreach (ToleranceRule rule in defaults) _rules.Add(rule);
            _grid.ItemsSource = null;
            _grid.ItemsSource = _rules;
        }

        private void OnAccept(object sender, RoutedEventArgs args)
        {
            // Flush any in-progress cell edit into the bound objects before closing.
            _grid.CommitEdit(DataGridEditingUnit.Row, true);
            DialogResult = true;
        }
    }
}
