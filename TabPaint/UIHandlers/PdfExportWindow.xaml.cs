using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TabPaint.Services;

namespace TabPaint.Windows
{
    public partial class PdfExportWindow : Window
    {
        public ObservableCollection<PdfPageViewModel> Pages { get; set; } = new ObservableCollection<PdfPageViewModel>();
        public bool IsConfirmed { get; private set; }
        public string SavePath => PathTextBox.Text;

        private Point _dragStartPoint;

        public PdfExportWindow(List<FileTabItem> items)
        {
            InitializeComponent();
            DataContext = this;

            // Load pages
            int index = 1;
            foreach (var item in items)
            {
                Pages.Add(new PdfPageViewModel
                {
                    SourceItem = item,
                    PageTitle = item.FileName ?? (string)Application.Current.TryFindResource("L_Untitled"),
                    PageNumber = index++,
                    PreviewImage = item.PreviewImage, // Assuming PreviewImage exists
                    FullImage = item.ImageSource,     // Assuming ImageSource exists
                    PageInfo = $"{item.Width} x {item.Height} px"
                });
            }

            PageListBox.ItemsSource = Pages;
            PreviewItemsControl.ItemsSource = Pages;

            // Default save path
            string defaultName = items.FirstOrDefault()?.FileName ?? "Document";
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
                                              Path.GetFileNameWithoutExtension(defaultName) + ".pdf");
            PathTextBox.Text = defaultPath;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

        private void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = Path.GetFileName(PathTextBox.Text),
                InitialDirectory = Path.GetDirectoryName(PathTextBox.Text)
            };

            if (sfd.ShowDialog() == true)
            {
                PathTextBox.Text = sfd.FileName;
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PathTextBox.Text))
            {
                MessageBox.Show("Please select a save path.");
                return;
            }

            if (Pages.Count == 0)
            {
                MessageBox.Show("No pages to export.");
                return;
            }

            IsConfirmed = true;
            Close();
        }

        private void OnDeletePageClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PdfPageViewModel vm)
            {
                Pages.Remove(vm);
                UpdatePageNumbers();
            }
        }

        private void UpdatePageNumbers()
        {
            for (int i = 0; i < Pages.Count; i++)
            {
                Pages[i].PageNumber = i + 1;
            }
        }

        #region Drag and Drop Reordering

        private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ListBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point position = e.GetPosition(null);
                if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    ListBox listBox = sender as ListBox;
                    ListBoxItem listBoxItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);

                    if (listBoxItem != null)
                    {
                        PdfPageViewModel data = (PdfPageViewModel)listBox.ItemContainerGenerator.ItemFromContainer(listBoxItem);
                        DataObject dragData = new DataObject("PdfPageViewModel", data);
                        DragDrop.DoDragDrop(listBoxItem, dragData, DragDropEffects.Move);
                    }
                }
            }
        }

        private void ListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("PdfPageViewModel"))
            {
                PdfPageViewModel droppedData = e.Data.GetData("PdfPageViewModel") as PdfPageViewModel;
                PdfPageViewModel targetData = null;

                ListBox listBox = sender as ListBox;
                ListBoxItem listBoxItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);

                if (listBoxItem != null)
                {
                    targetData = (PdfPageViewModel)listBox.ItemContainerGenerator.ItemFromContainer(listBoxItem);
                }

                if (droppedData != null && targetData != null && droppedData != targetData)
                {
                    int oldIndex = Pages.IndexOf(droppedData);
                    int newIndex = Pages.IndexOf(targetData);

                    Pages.Move(oldIndex, newIndex);
                    UpdatePageNumbers();
                }
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }

        #endregion
    }

    public class PdfPageViewModel : DependencyObject
    {
        public FileTabItem SourceItem { get; set; }
        public string PageTitle { get; set; }
        public ImageSource PreviewImage { get; set; }
        public ImageSource FullImage { get; set; }
        public string PageInfo { get; set; }

        public int PageNumber
        {
            get { return (int)GetValue(PageNumberProperty); }
            set { SetValue(PageNumberProperty, value); }
        }
        public static readonly DependencyProperty PageNumberProperty =
            DependencyProperty.Register("PageNumber", typeof(int), typeof(PdfPageViewModel), new PropertyMetadata(0, OnPageNumberChanged));

        private static void OnPageNumberChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfPageViewModel vm)
            {
                vm.OnPropertyChanged(nameof(PageNumberText));
            }
        }

        public string PageNumberText => $"{Application.Current.TryFindResource("L_PdfExport_Page")} {PageNumber}";

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
