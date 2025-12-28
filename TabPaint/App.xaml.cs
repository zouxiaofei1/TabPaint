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
                filePath = @"E:\dev\0000.png";//默认
                //filePath = @"E:\dev\res\0000.png";//150+图片
                //filePath = @"E:\dev\res\pic\00A21CF65912690AD4AFA8C2E86D9FEC.jpg";//7000+图片文件夹
                //filePath = @"E:\dev\misc\1761874502657.jpg";//BUG图片

            }

            var window = new MainWindow(filePath);
            //window.Show();
        }

    }

}
