using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using static TabPaint.MainWindow;
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

            string filePath = "";

            // 1. 修改判断逻辑：同时支持 File.Exists 和 Directory.Exists
            if (e.Args is { Length: > 0 })
            {
                string inputPath = e.Args[0];
                if (File.Exists(inputPath) || Directory.Exists(inputPath))
                {
                    filePath = inputPath;
                }
            }
            else
            {
#if DEBUG
                // 在这里取消注释你想要测试的路径，Release模式下这段代码会被自动忽略
                 filePath = @"E:\dev\"; //10图片
               //filePath = @"E:\dev\res\"; // 150+图片
                //filePatg = @"E:\dev\res\camera\"; // 1000+4k照片
                // filePath = @"E:\dev\res\pic\"; // 7000+图片文件夹
#endif
            }
            TimeRecorder t = new TimeRecorder(); t.Reset(); t.Toggle();
            var window = new MainWindow(filePath);
            window.Show();
            t.Toggle();
        }

    }

}
