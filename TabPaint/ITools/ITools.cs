using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static TabPaint.MainWindow;

//
//TabPaint主程序
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        // 定义高亮颜色
        private readonly Brush PurpleHighlightBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9333EA"));
        private readonly Brush PurpleBackgroundBrush = new SolidColorBrush(Color.FromArgb(40, 136, 108, 228)); // 15% 透明度的紫色背景

        private void UpdateToolSelectionHighlight()
        {
            var toolControls = new System.Windows.Controls.Control[] { PickColorButton, EraserButton, SelectButton, FillButton, TextButton, BrushToggle, PenButton };

            System.Windows.Controls.Control target = _router.CurrentTool switch
            {
                EyedropperTool => PickColorButton,
                FillTool => FillButton,
                SelectTool => SelectButton,
                TextTool => TextButton,
                PenTool when _ctx.PenStyle == BrushStyle.Eraser => EraserButton,
                PenTool when _ctx.PenStyle == BrushStyle.Pencil => PenButton,
                PenTool => BrushToggle,
                _ => null
            };


            foreach (var ctrl in toolControls)
            {
                
                if (ctrl == null) continue;
                bool isTarget = (ctrl == target);
                ctrl.Tag = isTarget;
                if (_router.CurrentTool == _tools.Pen && _ctx.PenStyle != BrushStyle.Eraser && _ctx.PenStyle != BrushStyle.Pencil)
                {
                    BrushToggle.BorderBrush= PurpleHighlightBrush;
                    BrushToggle.Background= PurpleBackgroundBrush;
                }
                // 2. 关键：清除之前可能存在的本地颜色赋值，让 Style 重新接管控制权
                ctrl.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
                ctrl.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            }

        }
        




        public interface ITool
        {
            string Name { get; }
            System.Windows.Input.Cursor Cursor { get; }
            void Cleanup(ToolContext ctx);
            void StopAction(ToolContext ctx);
            void OnPointerDown(ToolContext ctx, Point viewPos);
            void OnPointerMove(ToolContext ctx, Point viewPos);
            void OnPointerUp(ToolContext ctx, Point viewPos);
            void OnKeyDown(ToolContext ctx, System.Windows.Input.KeyEventArgs e);
        }

        public abstract class ToolBase : ITool
        {
            public abstract string Name { get; }
            public virtual System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Arrow;
            public virtual void OnPointerDown(ToolContext ctx, Point viewPos) { }
            public virtual void OnPointerMove(ToolContext ctx, Point viewPos) { }
            public virtual void OnPointerUp(ToolContext ctx, Point viewPos) { }
            public virtual void OnKeyDown(ToolContext ctx, System.Windows.Input.KeyEventArgs e) { }
            public virtual void Cleanup(ToolContext ctx) { }
            public virtual void StopAction(ToolContext ctx) { }
        }

        public class ToolContext
        {
            public CanvasSurface Surface { get; }
            public UndoRedoManager Undo { get; }
            public Color PenColor { get; set; } = Colors.Black;
            public Color EraserColor { get; set; } = Colors.White;
            public double PenThickness { get; set; } = 5.0;
            public Image ViewElement { get; } // 例如 DrawImage
            public WriteableBitmap Bitmap => Surface.Bitmap;
            public Image SelectionPreview { get; } // 预览层
            public Canvas SelectionOverlay { get; }
            public Canvas EditorOverlay { get; }
            public BrushStyle PenStyle { get; set; } = BrushStyle.Pencil;

            // 文档状态
            // public string CurrentFilePath { get; set; } = string.Empty;
            public bool IsDirty { get; set; } = false;
            private readonly IInputElement _captureElement;
            public ToolContext(CanvasSurface surface, UndoRedoManager undo, Image viewElement, Image previewElement, Canvas overlayElement, Canvas EditorElement, IInputElement captureElement)
            {
                Surface = surface;
                Undo = undo;
                ViewElement = viewElement;
                SelectionPreview = previewElement;
                SelectionOverlay = overlayElement; // ← 保存引用
                EditorOverlay = EditorElement;
                _captureElement = captureElement;
            }

            // 视图坐标 -> 像素坐标
            public Point ToPixel(Point viewPos)
            {
                var bmp = Surface.Bitmap;
                double sx = bmp.PixelWidth / ViewElement.ActualWidth;
                double sy = bmp.PixelHeight / ViewElement.ActualHeight;
                return new Point(viewPos.X * sx, viewPos.Y * sy);
            }

            public void CapturePointer() { _captureElement?.CaptureMouse(); }

            public void ReleasePointerCapture() { _captureElement?.ReleaseMouseCapture(); }
        }


        public class ToolRegistry//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        {
            public ITool Pen { get; } = new PenTool();
            //public ITool Eraser { get; } = new EraserTool();
            public ITool Eyedropper { get; } = new EyedropperTool();
            public ITool Fill { get; } = new FillTool();
            public ITool Select { get; } = new SelectTool();//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            public ITool Text { get; } = new TextTool();
        }

        public class InputRouter /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        {
            private readonly ToolContext _ctx;
            public ITool CurrentTool { get; private set; }

            public InputRouter(ToolContext ctx, ITool defaultTool)
            {
                _ctx = ctx;
                CurrentTool = defaultTool;

                _ctx.ViewElement.MouseDown += (s, e) => CurrentTool.OnPointerDown(_ctx, e.GetPosition(_ctx.ViewElement));
                _ctx.ViewElement.MouseMove += ViewElement_MouseMove;
                _ctx.ViewElement.MouseUp += (s, e) => CurrentTool.OnPointerUp(_ctx, e.GetPosition(_ctx.ViewElement));
            }

            public void ViewElement_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
            {


                var position = e.GetPosition(_ctx.ViewElement);
                if (_ctx.Surface.Bitmap != null)
                {

                    Point px = _ctx.ToPixel(position);
                    ((MainWindow)System.Windows.Application.Current.MainWindow).MousePosition = $"X:{(int)px.X} Y:{(int)px.Y}";
                }
                CurrentTool.OnPointerMove(_ctx, position);
            }// 定义高亮颜色
            private readonly Brush PurpleHighlightBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#886CE4"));
            private readonly Brush PurpleBackgroundBrush = new SolidColorBrush(Color.FromArgb(40, 136, 108, 228)); // 15% 透明度的紫色背景


   

            public void SetTool(ITool tool)
            {

                if (CurrentTool == tool) return; // Optional: Don't do work if it's the same tool.
                CurrentTool?.Cleanup(_ctx);
                CurrentTool = tool;
               
                a.s("Set to:" + CurrentTool.ToString());

                _ctx.ViewElement.Cursor = tool.Cursor;
                //var mainWindow = (MainWindow)Application.Current.MainWindow;

                ((MainWindow)System.Windows.Application.Current.MainWindow).SetPenResizeBarVisibility((tool is PenTool && _ctx.PenStyle != BrushStyle.Pencil));
                ((MainWindow)System.Windows.Application.Current.MainWindow).UpdateToolSelectionHighlight();
            }
            public void ViewElement_MouseDown(object sender, MouseButtonEventArgs e)
    => CurrentTool?.OnPointerDown(_ctx, e.GetPosition(_ctx.ViewElement));

            public void ViewElement_MouseUp(object sender, MouseButtonEventArgs e)
                => CurrentTool?.OnPointerUp(_ctx, e.GetPosition(_ctx.ViewElement));
            public void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
            {//快捷键
                CurrentTool.OnKeyDown(_ctx, e);
            }
        }


    }
}