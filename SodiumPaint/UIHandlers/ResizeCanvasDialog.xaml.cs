using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SodiumPaint
{
    public partial class ResizeCanvasDialog : Window
    {
        // 用于向外传递结果的公共属性
        public int ImageWidth { get; private set; }
        public int ImageHeight { get; private set; }

        public ResizeCanvasDialog(int currentWidth, int currentHeight)
        {
            InitializeComponent();
            this.ImageWidth = currentWidth;
            this.ImageHeight = currentHeight;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 窗口加载时，用当前尺寸填充文本框
            WidthTextBox.Text = ImageWidth.ToString();
            HeightTextBox.Text = ImageHeight.ToString();
            WidthTextBox.Focus();
            WidthTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 验证输入
            if (!int.TryParse(WidthTextBox.Text, out int newWidth) || newWidth <= 0)
            {
                MessageBox.Show("请输入有效的正整数宽度。", "输入无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(HeightTextBox.Text, out int newHeight) || newHeight <= 0)
            {
                MessageBox.Show("请输入有效的正整数高度。", "输入无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 将验证后的值存入公共属性
            ImageWidth = newWidth;
            ImageHeight = newHeight;

            // 设置 DialogResult 为 true，这会自动关闭窗口并向调用者表示成功
            this.DialogResult = true;
        }
    }
}
