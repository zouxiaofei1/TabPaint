using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static TabPaint.MainWindow;

//
//拖拽事件处理cs(目前只包括全局遮罩的那个)
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnGlobalDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabPaintSelectionDrag"))
            {
                HideDragOverlay();
                e.Effects = DragDropEffects.None; // 此时在窗口内显示禁止符号，或者改为 Move/Copy 也可以，只要不弹窗
                e.Handled = true;
                return;
            }
            // 1. 屏蔽程序内部拖拽 (如标签页排序)
            if (e.Data.GetDataPresent("TabPaintInternalDrag"))
            {
                Point pos = e.GetPosition(this);
                if (pos.Y < 210)
                {
                    HideDragOverlay();
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                    return;
                }
            }

            // 2. 检查是否有文件拖入
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] allFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
                // 简单的图片过滤
                var imageFiles = allFiles?.Where(f => IsImageFile(f)).ToArray();

                if (imageFiles != null && imageFiles.Length > 0)
                {
                    Point pos = e.GetPosition(this); 
                    if (pos.Y <= 100)
                    {
                        e.Effects = DragDropEffects.Move;
                        ShowDragOverlay("切换工作区", "将清空当前画布并打开新文件夹");
                    }
                    // B. 其他区域 (菜单栏、工具栏、ImageBar、画布)
                    else
                    {
                        e.Effects = DragDropEffects.Copy;

                        // B-1. 多文件 -> 强制作为新标签页打开
                        if (imageFiles.Length > 1)
                        {
                            ShowDragOverlay("批量打开", $"将 {imageFiles.Length} 张图片作为新标签页打开");
                        }
                        // B-2. 单文件 -> 根据位置决定是“添加到列表”还是“插入画布”
                        else
                        {
                            if (pos.Y < 210)
                            {
                                ShowDragOverlay("添加到列表", "将图片作为新标签页加入");
                            }
                            // 如果拖到了下方的画布区，倾向于“插入图片到当前画作”
                            else
                            {
                                ShowDragOverlay("插入图片", "在当前位置插入图片");
                            }
                        }
                    }
                }
                else
                {
                    // 拖进来的不是图片
                    e.Effects = DragDropEffects.None;
                    HideDragOverlay();
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
                HideDragOverlay();
            }

            e.Handled = true;
        }
        private async void OnGlobalDrop(object sender, DragEventArgs e)
        {
            HideDragOverlay(); // 必须第一时间隐藏

            // 1. 屏蔽内部拖拽
            if (e.Data.GetDataPresent("TabPaintInternalDrag"))
            {
                e.Handled = true;
                return;
            }

            // 2. 处理文件
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                // 再次过滤，确保安全
                var imageFiles = files?.Where(f => IsImageFile(f)).ToArray();

                if (imageFiles != null && imageFiles.Length > 0)
                {
                    Point pos = e.GetPosition(this);

                    // A. 标题栏区域 -> 切换工作区
                    if (pos.Y <= 100)
                    {
                        // 逻辑：取第一个文件进行切换
                        await SwitchWorkspaceToNewFile(imageFiles[0]);
                    }
                    // B. 其他区域
                    else
                    {
                        // B-1. 多文件 -> 全部新建标签页
                        if (imageFiles.Length > 1)
                        {
                            await OpenFilesAsNewTabs(imageFiles);
                        }
                        // B-2. 单文件 -> 视位置而定
                        else
                        {
                            string filePath = imageFiles[0];

                            if (pos.Y < 200)
                            {
                                await OpenFilesAsNewTabs(new string[] { filePath });
                            }
                            // 如果在画布区域，插入图片
                            else
                            {
                                InsertImageToCanvas(filePath);
                            }
                        }
                    }
                }
                e.Handled = true;
            }
        }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }
        private DispatcherTimer _dragWatchdog;
        private void DragWatchdog_Tick(object sender, EventArgs e)
        {
            // 如果遮罩本来就是隐藏的，或者窗口关了，停止定时器
            if (!_isDragOverlayVisible || this.WindowState == WindowState.Minimized)
            {
                _dragWatchdog.Stop();
                return;
            }

            POINT cursorScreenPos;
            GetCursorPos(out cursorScreenPos);

            // 获取窗口物理位置
            Point p1 = this.PointToScreen(new Point(0, 0));
            Point p2 = this.PointToScreen(new Point(this.ActualWidth, this.ActualHeight));

            // 为了防止边界闪烁，可以给窗口范围加一点“缓冲余量”(Padding)，比如向外扩 5 像素
            // 只有鼠标真的离开窗口 5 像素远了，才强制隐藏
            double padding = 5.0;

            bool isInside = (cursorScreenPos.X >= p1.X - padding && cursorScreenPos.X <= p2.X + padding &&
                             cursorScreenPos.Y >= p1.Y - padding && cursorScreenPos.Y <= p2.Y + padding);

            if (!isInside)
            {
                // 鼠标由于移动太快，已经逃逸到窗口外，但 DragLeave 没触发
                // 手动进行“垃圾回收”
                HideDragOverlay();
            }
        }

        private void OnGlobalDragLeave(object sender, DragEventArgs e)
        {
            // 获取当前窗口
            var window = Window.GetWindow(this);
            if (window == null) return;

            // 获取鼠标在屏幕上的物理坐标 (Pixel)
            POINT cursorScreenPos;
            GetCursorPos(out cursorScreenPos);

            Point p1 = window.PointToScreen(new Point(0, 0));
            Point p2 = window.PointToScreen(new Point(window.ActualWidth, window.ActualHeight));

            // 计算物理像素下的窗口范围
            double left = p1.X;
            double top = p1.Y;
            double right = p2.X;
            double bottom = p2.Y;
            bool isInside = (cursorScreenPos.X >= left && cursorScreenPos.X <= right &&
                             cursorScreenPos.Y >= top && cursorScreenPos.Y <= bottom);

            if (!isInside)
            {
                HideDragOverlay();
            }
        }
        private void ShowDragOverlay(string title, string subText)
        {
            // 更新文字内容
            DragOverlayText.Text = title;
            DragOverlaySubText.Text = subText;

            // 如果已经在显示中，就不重复播放动画，直接返回
            if (_isDragOverlayVisible) return;

            _isDragOverlayVisible = true;

            // 播放淡入动画
            Storyboard fadeIn = (Storyboard)this.Resources["FadeInDragOverlay"];
            fadeIn.Begin(); _dragWatchdog.Start();
        }

        private void HideDragOverlay()
        {
            if (!_isDragOverlayVisible) return;

            _isDragOverlayVisible = false;

            // 播放淡出动画
            Storyboard fadeOut = (Storyboard)this.Resources["FadeOutDragOverlay"];
            fadeOut.Begin(); _dragWatchdog.Stop();
        }

        private void InsertImageToCanvas(string filePath)
        {
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // 必须 OnLoad 才能解除文件占用
                bitmap.EndInit();
                bitmap.Freeze(); // 性能优化

                // 切换到选择工具
                _router.SetTool(_tools.Select);

                if (_tools.Select is SelectTool st)
                {
                    st.InsertImageAsSelection(_ctx, bitmap);
                }
            }
            catch (Exception ex)
            {
                ShowToast("无法插入图片: " + ex.Message);
            }
        }
    }
}