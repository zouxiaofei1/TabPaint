//
//EventHandler.Menu.cs
//fileedit两菜单
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using TabPaint.Controls;
using TabPaint.UIHandlers;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void FitToWindow_Click(object sender, RoutedEventArgs e) =>   FitToWindow();

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            double targetScale = zoomscale / ZoomTimes;

            System.Windows.Point centerPoint = new System.Windows.Point(ScrollContainer.ViewportWidth / 2, ScrollContainer.ViewportHeight / 2);
            _hasUserManuallyZoomed = true;
            StartSmoothZoom(targetScale, centerPoint);
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            double targetScale = zoomscale * ZoomTimes;

            System.Windows.Point centerPoint = new System.Windows.Point(ScrollContainer.ViewportWidth / 2, ScrollContainer.ViewportHeight / 2);
            _hasUserManuallyZoomed = true;
            StartSmoothZoom(targetScale, centerPoint);
        }
        private void OnStatusBarZoomChanged(object sender, ZoomRoutedEventArgs e)
        {
            double newScale = e.NewZoom;
            zoomscale = Math.Clamp(newScale, MinZoom, MaxZoom);
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
            UpdateSliderBarValue(zoomscale);
        }
        private void ZoomMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = MyStatusBar.ZoomComboBox;

            if (combo != null && combo.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                double selectedScale = Convert.ToDouble(item.Tag);
                zoomscale = Math.Clamp(selectedScale, MinZoom, MaxZoom);
                ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
                UpdateSliderBarValue(zoomscale);
            }
        }

        private void OnStatusCommandBarToggleRequested(object sender, RoutedEventArgs e)
        {
            ApplyStatusCommandBarExpandedState(!_isStatusCommandBarExpanded, adjustWindowHeight: true);
        }

        private void ApplyStatusCommandBarExpandedState(bool isExpanded, bool adjustWindowHeight)
        {
            if (StatusCommandBar == null) return;
            if (_isStatusCommandBarExpanded == isExpanded &&
                ((StatusCommandBar.Visibility == Visibility.Visible) == isExpanded))
            {
                return;
            }

            _isStatusCommandBarExpanded = isExpanded;
            StatusCommandBar.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;

            if (adjustWindowHeight && WindowState == WindowState.Normal)
            {
                if (isExpanded)
                {
                    Height += StatusCommandBarHeightDelta;
                }
                else
                {
                    Height = Math.Max(MinHeight, Height - StatusCommandBarHeightDelta);
                }
            }

            var settings = SettingsManager.Instance.Current;
            if (settings.IsStatusCommandBarExpanded != isExpanded)
            {
                settings.IsStatusCommandBarExpanded = isExpanded;
                SettingsManager.Instance.Save();
            }

            if (isExpanded && StatusCommandTextBox != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    StatusCommandTextBox.Focus();
                    StatusCommandTextBox.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private void OnStatusCommandTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            e.Handled = true;
            ShowToast("L_Main_Toast_Info");
        }

    }
}