using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace TabPaint
{
    public static class SingleInstance
    {
        // 给你的程序起一个唯一的ID，用于识别管道和互斥体
        private const string UniqueId = "TabPaint_App_Mutex_UUID_91823091";

        private static Mutex _mutex;

        // 检查是否是第一个实例
        public static bool IsFirstInstance()
        {
            // 创建互斥体，如果你能拥有它，说明你是第一个
            _mutex = new Mutex(true, UniqueId, out bool createdNew);
            return createdNew;
        }

        // 第二个实例调用此方法：发送参数给第一个实例
        public static void SendArgsToFirstInstance(string[] args)
        {
            if (args == null || args.Length == 0) return;

            try
            {
                using (var client = new NamedPipeClientStream(".", UniqueId, PipeDirection.Out))
                {
                    client.Connect(500); // 连接超时时间 500ms
                    using (var writer = new StreamWriter(client))
                    {
                        // 这里简单处理，只发送第一个文件路径，如果支持多选可以改逻辑
                        writer.WriteLine(args[0]);
                        writer.Flush();
                    }
                }
            }
            catch (Exception)
            {
                // 忽略连接错误（防止第一实例刚好关闭）
            }
        }

        // 第一个实例调用此方法：监听后续实例的消息
        public static void ListenForArgs(Action<string> onFileReceived)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream(UniqueId, PipeDirection.In))
                        {
                            await server.WaitForConnectionAsync();
                            using (var reader = new StreamReader(server))
                            {
                                string filePath = await reader.ReadLineAsync();
                                if (!string.IsNullOrEmpty(filePath))
                                {
                                    onFileReceived?.Invoke(filePath);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 管道错误容错，防止崩溃
                        await Task.Delay(1000);
                    }
                }
            });
        }
    }
}
