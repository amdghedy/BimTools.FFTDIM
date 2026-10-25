using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Modal results window shared by the audit, coverage, tolerance and batch commands.
    /// Built in pure C# (no XAML) so the project needs no markup compilation.
    /// Features:
    ///   · severity filter (all / errors / warnings / info),
    ///   · live text search over element, type, view and detail,
    ///   · a View column (populated by the batch audit),
    ///   · double-click a row (or "Select in model") to select and zoom to the element,
    ///   · export to CSV, HTML or JSON.
    /// All captions localise through <see cref="L"/>; the layout flips to right-to-left
    /// when the Arabic interface is active.
    /// </summary>
    public class AuditResultsWindow : Window
    {
        private readonly AuditResult _result;
        private readonly UIDocument _uidoc;
        private readonly DataGrid _grid;
        private readonly ComboBox _severityFilter;
        private readonly TextBox _search;
        private ICollectionView _view;

        public AuditResultsWindow(AuditResult result, UIDocument uidoc)
        {
            _result = result;
            _uidoc = uidoc;

            Title = string.Format(L.T("ResultsTitleFormat"), result.ViewName);
            Width = 980;
            Height = 620;
            MinWidth = 720;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF6, 0xF8));
            if (L.RightToLeft) FlowDirection = FlowDirection.RightToLeft;

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = result.SummaryText,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ---- Filter bar ------------------------------------------------------
            var filterBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };

            _severityFilter = new ComboBox { MinWidth = 140, Margin = new Thickness(0, 0, 12, 0) };
            _severityFilter.Items.Add(L.T("FilterAll"));
            _severityFilter.Items.Add(L.T("FilterError"));
            _severityFilter.Items.Add(L.T("FilterWarning"));
            _severityFilter.Items.Add(L.T("FilterInfo"));
            _severityFilter.SelectedIndex = 0;
            _severityFilter.SelectionChanged += (sender, args) => ApplyFilter();

            var searchLabel = new TextBlock
            {
                Text = L.T("SearchLabel"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _search = new TextBox { MinWidth = 220, VerticalContentAlignment = VerticalAlignment.Center };
            _search.TextChanged += (sender, args) => ApplyFilter();

            filterBar.Children.Add(_severityFilter);
            filterBar.Children.Add(searchLabel);
            filterBar.Children.Add(_search);
            Grid.SetRow(filterBar, 1);
            root.Children.Add(filterBar);

            // ---- Results grid ----------------------------------------------------
            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                SelectionMode = DataGridSelectionMode.Single,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD4, 0xD9)),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _grid.Columns.Add(MakeColumn(L.T("ColSeverity"), "Severity", 92));
            _grid.Columns.Add(MakeColumn(L.T("ColType"), "Type", 128));
            _grid.Columns.Add(MakeColumn(L.T("ColElement"), "ElementLabel", 230));
            _grid.Columns.Add(MakeColumn(L.T("ColValue"), "ValueText", 100));
            _grid.Columns.Add(MakeColumn(L.T("ColView"), "ViewName", 120));
            _grid.Columns.Add(MakeColumn(L.T("ColDetail"), "Detail", null));
            _grid.MouseDoubleClick += OnRowActivated;
            _view = CollectionViewSource.GetDefaultView(_result.Issues);
            _view.Filter = OnIssueFilter;
            _grid.ItemsSource = _view;
            Grid.SetRow(_grid, 2);
            root.Children.Add(_grid);

            // ---- Buttons ---------------------------------------------------------
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var select = MakeButton(L.T("SelectInModel"));
            select.Click += OnSelectClicked;
            var exportCsv = MakeButton(L.T("ExportCsv"));
            exportCsv.Click += (sender, args) => OnExportClicked("csv", 1);
            var exportHtml = MakeButton(L.T("ExportHtml"));
            exportHtml.Click += (sender, args) => OnExportClicked("html", 2);
            var exportJson = MakeButton(L.T("ExportJson"));
            exportJson.Click += (sender, args) => OnExportClicked("json", 3);
            var close = MakeButton(L.T("Close"));
            close.Click += (sender, args) => Close();
            close.IsCancel = true;
            buttons.Children.Add(select);
            buttons.Children.Add(exportCsv);
            buttons.Children.Add(exportHtml);
            buttons.Children.Add(exportJson);
            buttons.Children.Add(close);
            Grid.SetRow(buttons, 3);
            root.Children.Add(buttons);

            Content = root;
        }

        // ------------------------------------------------------------------
        //  Filtering
        // ------------------------------------------------------------------

        private void ApplyFilter()
        {
            if (_view != null) _view.Refresh();
        }

        private bool OnIssueFilter(object item)
        {
            var issue = item as DimensionIssue;
            if (issue == null) return true;

            switch (_severityFilter.SelectedIndex)
            {
                case 1:
                    if (issue.Severity != IssueSeverity.Error) return false;
                    break;
                case 2:
                    if (issue.Severity != IssueSeverity.Warning) return false;
                    break;
                case 3:
                    if (issue.Severity != IssueSeverity.Info) return false;
                    break;
            }

            string query = _search.Text;
            if (!string.IsNullOrWhiteSpace(query))
            {
                return Contains(issue.ElementLabel, query) ||
                       Contains(issue.Type.ToString(), query) ||
                       Contains(issue.ViewName, query) ||
                       Contains(issue.ValueText, query) ||
                       Contains(issue.Detail, query);
            }
            return true;
        }

        private static bool Contains(string text, string query)
        {
            return !string.IsNullOrEmpty(text) &&
                   text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ------------------------------------------------------------------
        //  Grid helpers
        // ------------------------------------------------------------------

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
                MinWidth = 104,
                MinHeight = 26,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(8, 3, 8, 3)
            };
        }

        // ------------------------------------------------------------------
        //  Actions
        // ------------------------------------------------------------------

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

        private void OnExportClicked(string extension, int filterIndex)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = L.T("SaveDialogTitle"),
                Filter = "CSV files (*.csv)|*.csv|HTML files (*.html)|*.html|JSON files (*.json)|*.json",
                FilterIndex = filterIndex,
                FileName = string.Format("FFTDIM_{0:yyyyMMdd_HHmm}.{1}", DateTime.Now, extension)
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                string path = ReportExporter.Write(dialog.FileName, _result);
                MessageBox.Show(this, string.Format(L.T("ReportSavedFormat"), path), "FFTDIM",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("ExportFailedFormat"), ex.Message), "FFTDIM",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
