using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimTools.FFTDIM.Core;

namespace BimTools.FFTDIM.UI
{
    /// <summary>
    /// Modal results window shared by the audit, coverage and tolerance commands.
    /// Built in pure C# (no XAML) so the project needs no markup compilation.
    /// Double-clicking a row (or clicking "Select in model") selects and zooms to the
    /// corresponding element in the open document.
    /// </summary>
    public class AuditResultsWindow : Window
    {
        private readonly AuditResult _result;
        private readonly UIDocument _uidoc;
        private readonly DataGrid _grid;

        public AuditResultsWindow(AuditResult result, UIDocument uidoc)
        {
            _result = result;
            _uidoc = uidoc;

            Title = "FFTDIM — Audit results — " + result.ViewName;
            Width = 940;
            Height = 580;
            MinWidth = 700;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF6, 0xF8));

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = result.SummaryText,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                SelectionMode = DataGridSelectionMode.Single,
                ItemsSource = result.Issues,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD4, 0xD9)),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _grid.Columns.Add(MakeColumn("Severity", "Severity", 92));
            _grid.Columns.Add(MakeColumn("Type", "Type", 128));
            _grid.Columns.Add(MakeColumn("Element", "ElementLabel", 240));
            _grid.Columns.Add(MakeColumn("Value", "ValueText", 110));
            _grid.Columns.Add(MakeColumn("Detail", "Detail", null));
            _grid.MouseDoubleClick += OnRowActivated;
            Grid.SetRow(_grid, 1);
            root.Children.Add(_grid);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var select = MakeButton("Select in model");
            select.Click += OnSelectClicked;
            var export = MakeButton("Export CSV…");
            export.Click += OnExportClicked;
            var close = MakeButton("Close");
            close.Click += (sender, args) => Close();
            close.IsCancel = true;
            buttons.Children.Add(select);
            buttons.Children.Add(export);
            buttons.Children.Add(close);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            Content = root;
        }

        private static DataGridTextColumn MakeColumn(string header, string path, double? width)
        {
            var column = new DataGridTextColumn { Header = header, Binding = new Binding(path) };
            if (width.HasValue) column.Width = new DataGridLength(width.Value);
            return column;
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

        private void OnRowActivated(object sender, System.Windows.Input.MouseButtonEventArgs args)
        {
            SelectCurrentRow();
        }

        private void OnSelectClicked(object sender, RoutedEventArgs args)
        {
            SelectCurrentRow();
        }

        private void SelectCurrentRow()
        {
            var issue = _grid.SelectedItem as DimensionIssue;
            if (issue == null || issue.ElementId == null || issue.ElementId == ElementId.InvalidElementId) return;

            var ids = new List<ElementId> { issue.ElementId };
            _uidoc.Selection.SetElementIds(ids);
            try
            {
                _uidoc.ShowElements(ids);
            }
            catch
            {
                // ShowElements can fail for elements not visible in the current view; selection still applies.
            }
        }

        private void OnExportClicked(object sender, RoutedEventArgs args)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "FFTDIM — save audit report",
                Filter = "CSV files (*.csv)|*.csv|HTML files (*.html)|*.html",
                FileName = string.Format("FFTDIM_{0:yyyyMMdd_HHmm}.csv", DateTime.Now)
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                string path = ReportExporter.Write(dialog.FileName, _result);
                MessageBox.Show(this, "Report saved to:\n" + path, "FFTDIM",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not write the report:\n\n" + ex.Message, "FFTDIM",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
