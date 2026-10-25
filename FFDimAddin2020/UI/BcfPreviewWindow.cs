using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimTools.FFTDIM.Core;

namespace BimTools.FFTDIM.UI
{
    /// <summary>
    /// FFD-12 · read-only preview of the topics of an imported BCF 2.1 package. Lists
    /// title / status / author / date / resolved component counts, shows the selected
    /// topic's description and comments, and selects the resolved host-model elements
    /// of the selected topic in the model for re-verification. Built in pure C# (no
    /// XAML); captions localise through <see cref="L"/> with right-to-left support.
    /// </summary>
    public class BcfPreviewWindow : Window
    {
        private readonly List<BcfTopicRecord> _topics;
        private readonly UIDocument _uidoc;
        private readonly DataGrid _grid;
        private readonly TextBox _detail;

        public BcfPreviewWindow(List<BcfTopicRecord> topics, string filePath, UIDocument uidoc)
        {
            _topics = topics;
            _uidoc = uidoc;

            Title = string.Format(L.T("BcfTitleFormat"), System.IO.Path.GetFileName(filePath));
            Width = 900;
            Height = 600;
            MinWidth = 680;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF6, 0xF8));
            if (L.RightToLeft) FlowDirection = FlowDirection.RightToLeft;

            int resolvedTotal = topics.Sum(t => t.ResolvedCount);
            int refsTotal = topics.Sum(t => t.ComponentRefs.Count);

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = string.Format(L.T("BcfSummaryFormat"),
                    topics.Count, refsTotal, resolvedTotal, filePath),
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var hint = new TextBlock
            {
                Text = L.T("BcfHint"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x57, 0x60, 0x6A)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(hint, 1);
            root.Children.Add(hint);

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
                Margin = new Thickness(0, 0, 0, 8)
            };
            _grid.Columns.Add(MakeColumn(L.T("ColTopic"), "Title", 260));
            _grid.Columns.Add(MakeColumn(L.T("ColStatus"), "Status", 90));
            _grid.Columns.Add(MakeColumn(L.T("ColAuthor"), "Author", 130));
            _grid.Columns.Add(MakeColumn(L.T("ColDate"), "CreationDate", 130));
            _grid.Columns.Add(MakeColumn(L.T("ColComponents"), "ResolvedText", 120));
            _grid.ItemsSource = topics;
            _grid.SelectionChanged += OnTopicSelected;
            Grid.SetRow(_grid, 2);
            root.Children.Add(_grid);

            _detail = new TextBox
            {
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalContentAlignment = VerticalAlignment.Top,
                AcceptsReturn = true,
                Height = 110,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD4, 0xD9)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(_detail, 3);
            root.Children.Add(_detail);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var select = MakeButton(L.T("BcfSelectResolved"));
            select.Click += OnSelectResolved;
            var close = MakeButton(L.T("Close"));
            close.Click += (sender, args) => Close();
            close.IsCancel = true;
            buttons.Children.Add(select);
            buttons.Children.Add(close);
            Grid.SetRow(buttons, 4);
            root.Children.Add(buttons);

            Content = root;

            if (_grid.Items.Count > 0) _grid.SelectedIndex = 0;
        }

        // ------------------------------------------------------------------
        //  Actions
        // ------------------------------------------------------------------

        private void OnTopicSelected(object sender, SelectionChangedEventArgs args)
        {
            var topic = _grid.SelectedItem as BcfTopicRecord;
            if (topic == null)
            {
                _detail.Text = "";
                return;
            }

            var builder = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(topic.Description))
            {
                builder.AppendLine(topic.Description);
            }
            foreach (string comment in topic.Comments)
            {
                builder.AppendLine("— " + comment);
            }
            if (topic.ComponentRefs.Count > 0)
            {
                builder.AppendLine();
                builder.AppendFormat(L.T("BcfRefsFormat"), topic.ComponentRefs.Count, topic.ResolvedCount);
                builder.AppendLine();
                builder.Append(L.T("BcfRefsHint"));
            }
            _detail.Text = builder.ToString();
        }

        private void OnSelectResolved(object sender, RoutedEventArgs args)
        {
            var topic = _grid.SelectedItem as BcfTopicRecord;
            if (topic == null || topic.ResolvedIds.Count == 0)
            {
                MessageBox.Show(this, L.T("BcfNoResolved"), "FFTDIM",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _uidoc.Selection.SetElementIds(topic.ResolvedIds);
                _uidoc.ShowElements(topic.ResolvedIds);
            }
            catch
            {
                // ShowElements can fail for elements not visible in the current view;
                // selection still applies.
            }
        }

        // ------------------------------------------------------------------
        //  Helpers
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
    }
}
