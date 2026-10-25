using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Autodesk.Revit.DB;
using BimTools.FFTDIM.Core;

namespace BimTools.FFTDIM.UI
{
    /// <summary>
    /// View selection dialog for the Batch Audit Views command (FFD-09). Lists every
    /// auditable view of the document (plans, sections, elevations, drafting, 3D —
    /// templates/schedules/sheets/legends excluded) with a checkbox each, plus
    /// select-all / select-none helpers. Built in pure C# (no XAML).
    /// </summary>
    public class ViewPickerWindow : Window
    {
        /// <summary>ListBox item wrapper: keeps the view together with its checkbox state.</summary>
        private class ViewItem
        {
            public View View { get; set; }
            public string Label { get; set; }
            public bool IsSelected { get; set; }
        }

        private readonly List<ViewItem> _items;

        /// <summary>Views whose checkbox was ticked when the dialog was accepted.</summary>
        public List<View> SelectedViews
        {
            get
            {
                return _items.Where(i => i.IsSelected).Select(i => i.View).ToList();
            }
        }

        public ViewPickerWindow(List<View> views)
        {
            _items = views.Select(v => new ViewItem
            {
                View = v,
                Label = v.Name + "   (" + v.ViewType + ")",
                IsSelected = false
            }).ToList();

            Title = L.T("PickerTitle");
            Width = 560;
            Height = 640;
            MinWidth = 480;
            MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF6, 0xF8));
            if (L.RightToLeft) FlowDirection = FlowDirection.RightToLeft;

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var info = new TextBlock
            {
                Text = L.T("PickerInfo"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(info, 0);
            root.Children.Add(info);

            var list = new ListBox
            {
                ItemsSource = _items,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD4, 0xD9)),
                Margin = new Thickness(0, 0, 0, 10),
                ScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            list.ItemTemplate = BuildItemTemplate();
            Grid.SetRow(list, 1);
            root.Children.Add(list);

            var helpers = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var all = MakeButton(L.T("SelectAll"));
            all.Click += (sender, args) => SetAll(true);
            var none = MakeButton(L.T("SelectNone"));
            none.Click += (sender, args) => SetAll(false);
            helpers.Children.Add(all);
            helpers.Children.Add(none);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var run = MakeButton(L.T("RunAudit"));
            run.IsDefault = true;
            run.Click += OnRun;
            var cancel = MakeButton(L.T("Cancel"));
            cancel.IsCancel = true;
            buttons.Children.Add(run);
            buttons.Children.Add(cancel);

            var bottom = new DockPanel();
            DockPanel.SetDock(helpers, Dock.Left);
            bottom.Children.Add(helpers);
            bottom.Children.Add(buttons);
            Grid.SetRow(bottom, 2);
            root.Children.Add(bottom);

            Content = root;
        }

        private static DataTemplate BuildItemTemplate()
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(CheckBox));
            factory.SetValue(CheckBox.MarginProperty, new Thickness(2, 3, 2, 3));
            factory.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsSelected")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            factory.SetBinding(CheckBox.ContentProperty, new Binding("Label"));
            template.VisualTree = factory;
            return template;
        }

        private void SetAll(bool selected)
        {
            foreach (ViewItem item in _items) item.IsSelected = selected;
        }

        private void OnRun(object sender, RoutedEventArgs args)
        {
            if (SelectedViews.Count == 0)
            {
                MessageBox.Show(this, L.T("PickerInfo"), Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
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
    }
}
