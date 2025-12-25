
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//TabPaint事件处理cs
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnCanvasDragOver(object sender, System.Windows.DragEventArgs e)
        {
            // 检查拖动的是否是文件
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                // 设置鼠标效果为“复制”图标，告知系统和用户这里可以放下
                e.Effects = System.Windows.DragDropEffects.Copy;
                e.Handled = true; // 标记事件已处理
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        private void OnCanvasDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string filePath = files[0];
                    try
                    {
                        // 1. 加载图片文件
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(filePath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad; // 必须 Load，否则文件会被占用
                        bitmap.EndInit();

                        _router.SetTool(_tools.Select);

                        if (_tools.Select is SelectTool st) // 强转成 SelectTool
                        {
                            st.InsertImageAsSelection(_ctx, bitmap);
                        }
                        e.Handled = true;
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show("无法识别的图片格式: " + ex.Message);
                    }
                }
            }
        }
        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Get the position relative to the scaled CanvasWrapper
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseDown(pos, e);
        }

        private void OnCanvasMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseMove(pos, e);
        }

        private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseUp(pos, e);
        }

        private void OnCanvasMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _router.CurrentTool?.StopAction(_ctx);
        }

        private void OnScrollContainerMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_router.CurrentTool is SelectTool selTool && selTool._selectionData != null)
            {
                // 1. 检查点击的是否是左键（通常右键用于弹出菜单，不应触发提交）
                if (e.ChangedButton != MouseButton.Left) return;

                // 2. 深度判定：点击来源是否属于滚动条的任何组成部分
                if (IsVisualAncestorOf<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource as DependencyObject))
                {
                    return; // 点击在滚动条上（轨道、滑块、箭头等），不执行提交
                }

                // 获取逻辑坐标
                Point pt = e.GetPosition(CanvasWrapper);

                // 3. 判定：点击是否不在选区内，且不在缩放句柄上
                bool hitHandle = selTool.HitTestHandle(pt, selTool._selectionRect) != SelectTool.ResizeAnchor.None;
                bool hitInside = selTool.IsPointInSelection(pt);

                if (!hitHandle && !hitInside)
                {
                    // 执行提交
                    selTool.CommitSelection(this._ctx);
                    selTool.CleanUp(this._ctx);

                    // 如果不希望 Canvas 接收这次点击（例如防止开始一次新的拖拽），可以拦截
                    // e.Handled = true; 
                }
            }
        }

    }
}