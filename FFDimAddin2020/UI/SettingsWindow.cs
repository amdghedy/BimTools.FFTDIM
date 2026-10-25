using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BimTools.FFTDIM.Core;

namespace BimTools.FFTDIM.UI
{
    /// <summary>
    /// FFD-08 · Settings &amp; Options dialog. Edits the persisted <see cref="AppSettings"/>
    /// that shape every FFTDIM command (audit thresholds, quick-dimension geometry, default
    /// report format, interface language). Built in pure C# (no XAML). Saving writes
    /// %APPDATA%\BimTools\FFTDIM\settings.json and re-applies the language immediately —
    /// ribbon captions pick the language up on the next Revit start.
    /// </summary>
    public class SettingsWindow : Window
    {
        private readonly AppSettings _settings;

        private readonly TextBox _zero = MakeNumericBox();
        private readonly TextBox _chainOffset = MakeNumericBox();
        private readonly TextBox _chainExtension = MakeNumericBox();
        private readonly TextBox _clearanceSkip = MakeNumericBox();
        private readonly TextBox _clearanceRadius = MakeNumericBox();

        private readonly CheckBox _includeSpot = new CheckBox { Margin = new Thickness(2, 3, 2, 3) };
        private readonly CheckBox _flagLinked = new CheckBox { Margin = new Thickness(2, 3, 2, 3) };
        private readonly CheckBox _preferWalls = new CheckBox { Margin = new Thickness(2, 3, 2, 3) };

        private readonly ComboBox _format = new ComboBox { MinWidth = 160 };
        private readonly ComboBox _language = new ComboBox { MinWidth = 160 };

        public SettingsWindow(AppSettings settings)
        {
            _settings = settings;

            Title = L.T("SettingsTitle");
            Width = 640;
            Height = 600;
            MinWidth = 560;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF6, 0xF8));
            if (L.RightToLeft) FlowDirection = FlowDirection.RightToLeft;

            // Seed the controls from the current settings.
            _zero.Text = settings.ZeroValueThresholdMm.ToString("0.0#", CultureInfo.InvariantCulture);
            _chainOffset.Text = settings.ChainOffsetMeters.ToString("0.0#", CultureInfo.InvariantCulture);
            _chainExtension.Text = settings.ChainExtensionMeters.ToString("0.0#", CultureInfo.InvariantCulture);
            _clearanceSkip.Text = settings.ClearanceSkipMm.ToString("0.0#", CultureInfo.InvariantCulture);
            _clearanceRadius.Text = settings.ClearanceSearchRadiusMeters.ToString("0.0#", CultureInfo.InvariantCulture);
            _includeSpot.IsChecked = settings.IncludeSpotDimensions;
            _flagLinked.IsChecked = settings.FlagLinkedReferences;
            _preferWalls.IsChecked = settings.PreferWallsOverGrids;

            _format.Items.Add("CSV (Excel)");
            _format.Items.Add("HTML (browser)");
            _format.Items.Add("JSON (machine-readable)");
            _format.SelectedIndex = (int)settings.DefaultReportFormat;

            _language.Items.Add("English");
            _language.Items.Add("العربية (Arabic)");
            _language.SelectedIndex = (int)settings.Language;

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(12)
            };
            var stack = new StackPanel();

            stack.Children.Add(MakeGroup(L.T("GroupAudit"),
                MakeOptionRow(L.T("OptZeroThreshold"), _zero),
                _includeSpot,
                _flagLinked));

            stack.Children.Add(MakeGroup(L.T("GroupQuick"),
                MakeOptionRow(L.T("OptChainOffset"), _chainOffset),
                MakeOptionRow(L.T("OptChainExtension"), _chainExtension),
                MakeOptionRow(L.T("OptClearanceSkip"), _clearanceSkip),
                MakeOptionRow(L.T("OptClearanceRadius"), _clearanceRadius),
                _preferWalls));

            stack.Children.Add(MakeGroup(L.T("GroupReports"),
                MakeOptionRow(L.T("OptDefaultFormat"), _format)));

            stack.Children.Add(MakeGroup(L.T("GroupInterface"),
                MakeOptionRow(L.T("OptLanguage"), _language)));

            var pathNote = new TextBlock
            {
                Text = AppSettingsStore.FilePath,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x57, 0x60, 0x6A)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 10, 2, 0)
            };
            stack.Children.Add(pathNote);

            scroll.Content = stack;

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(12, 0, 12, 12)
            };
            var restore = MakeButton(L.T("RestoreDefaults"));
            restore.Click += OnRestoreDefaults;
            var ok = MakeButton(L.T("Save"));
            ok.IsDefault = true;
            ok.Click += OnSave;
            var cancel = MakeButton(L.T("Cancel"));
            cancel.IsCancel = true;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(scroll, 0);
            Grid.SetRow(buttons, 1);
            root.Children.Add(scroll);
            root.Children.Add(buttons);
            buttons.Children.Add(restore);
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            Content = root;
        }

        // ------------------------------------------------------------------
        //  Construction helpers
        // ------------------------------------------------------------------

        private static TextBox MakeNumericBox()
        {
            return new TextBox
            {
                MinWidth = 110,
                MaxWidth = 160,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2)
            };
        }

        private static GroupBox MakeGroup(string header, params UIElement[] rows)
        {
            var panel = new StackPanel { Margin = new Thickness(4, 6, 4, 8) };
            foreach (UIElement row in rows) panel.Children.Add(row);
            return new GroupBox { Header = header, Content = panel, Margin = new Thickness(0, 0, 0, 10) };
        }

        private static Grid MakeOptionRow(string label, FrameworkElement control)
        {
            var grid = new Grid { Margin = new Thickness(2, 3, 2, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var text = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 0, 10, 0)
            };
            Grid.SetColumn(text, 0);
            Grid.SetColumn(control, 1);
            grid.Children.Add(text);
            grid.Children.Add(control);
            return grid;
        }

        private static Button MakeButton(string label)
        {
            return new Button
            {
                Content = label,
                MinWidth = 110,
                MinHeight = 26,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(8, 3, 8, 3)
            };
        }

        // ------------------------------------------------------------------
        //  Handlers
        // ------------------------------------------------------------------

        private void OnRestoreDefaults(object sender, RoutedEventArgs args)
        {
            var defaults = new AppSettings();
            _zero.Text = defaults.ZeroValueThresholdMm.ToString("0.0#", CultureInfo.InvariantCulture);
            _chainOffset.Text = defaults.ChainOffsetMeters.ToString("0.0#", CultureInfo.InvariantCulture);
            _chainExtension.Text = defaults.ChainExtensionMeters.ToString("0.0#", CultureInfo.InvariantCulture);
            _clearanceSkip.Text = defaults.ClearanceSkipMm.ToString("0.0#", CultureInfo.InvariantCulture);
            _clearanceRadius.Text = defaults.ClearanceSearchRadiusMeters.ToString("0.0#", CultureInfo.InvariantCulture);
            _includeSpot.IsChecked = defaults.IncludeSpotDimensions;
            _flagLinked.IsChecked = defaults.FlagLinkedReferences;
            _preferWalls.IsChecked = defaults.PreferWallsOverGrids;
            _format.SelectedIndex = (int)defaults.DefaultReportFormat;
            _language.SelectedIndex = (int)defaults.Language;
        }

        private void OnSave(object sender, RoutedEventArgs args)
        {
            double zero, chainOffset, chainExtension, clearanceSkip, clearanceRadius;

            if (!TryParse(_zero.Text, 0.01, 100.0, out zero))
            {
                Warn(L.T("OptZeroThreshold"), _zero.Text);
                return;
            }
            if (!TryParse(_chainOffset.Text, 0.0, 10.0, out chainOffset))
            {
                Warn(L.T("OptChainOffset"), _chainOffset.Text);
                return;
            }
            if (!TryParse(_chainExtension.Text, 0.0, 5.0, out chainExtension))
            {
                Warn(L.T("OptChainExtension"), _chainExtension.Text);
                return;
            }
            if (!TryParse(_clearanceSkip.Text, 0.0, 5000.0, out clearanceSkip))
            {
                Warn(L.T("OptClearanceSkip"), _clearanceSkip.Text);
                return;
            }
            if (!TryParse(_clearanceRadius.Text, 0.5, 100.0, out clearanceRadius))
            {
                Warn(L.T("OptClearanceRadius"), _clearanceRadius.Text);
                return;
            }

            _settings.ZeroValueThresholdMm = zero;
            _settings.IncludeSpotDimensions = _includeSpot.IsChecked == true;
            _settings.FlagLinkedReferences = _flagLinked.IsChecked == true;
            _settings.ChainOffsetMeters = chainOffset;
            _settings.ChainExtensionMeters = chainExtension;
            _settings.ClearanceSkipMm = clearanceSkip;
            _settings.ClearanceSearchRadiusMeters = clearanceRadius;
            _settings.PreferWallsOverGrids = _preferWalls.IsChecked == true;
            _settings.DefaultReportFormat = (ReportOutputFormat)_format.SelectedIndex;
            _settings.Language = (InterfaceLanguage)_language.SelectedIndex;

            AppSettingsStore.Save(_settings);
            L.Apply(_settings);
            DialogResult = true;
        }

        private static bool TryParse(string text, double min, double max, out double value)
        {
            value = 0;
            double parsed;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
                !double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
            {
                return false;
            }
            if (double.IsNaN(parsed) || double.IsInfinity(parsed)) return false;
            value = Math.Max(min, Math.Min(max, parsed));
            return true;
        }

        private void Warn(string optionLabel, string badValue)
        {
            MessageBox.Show(this, string.Format(L.T("InvalidNumberFormat"), badValue) +
                "\n\n" + optionLabel, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
