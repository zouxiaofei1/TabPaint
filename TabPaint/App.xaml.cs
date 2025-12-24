using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;
namespace TabPaint
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        static void s<T>(T a)
        {
            System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string filePath;

            // 判断是否通过命令行传入文件路径
            if (e.Args is { Length: > 0 } && File.Exists(e.Args[0]))
            {
                filePath = e.Args[0];
            }
            else
            {
                // Visual Studio 调试默认打开
                filePath = @"E:\dev\1.png";
            }

            var window = new MainWindow(filePath);
            //window.Show();
        }

    }

}
