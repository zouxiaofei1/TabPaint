using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace TabPaint
{
    public static class ClipboardHelper
    {
        private const int MaxRetries = 10;
        private const int RetryDelayMs = 100;

        /// <summary>
        /// 带有重试机制的设置剪贴板文本
        /// </summary>
        public static void SetTextWithRetry(string text)
        {
            ExecuteWithRetry(() => Clipboard.SetText(text));
        }

        /// <summary>
        /// 带有重试机制的设置剪贴板数据对象
        /// </summary>
        public static void SetDataObjectWithRetry(object data, bool copy)
        {
            ExecuteWithRetry(() => Clipboard.SetDataObject(data, copy));
        }

        /// <summary>
        /// 带有重试机制的获取剪贴板数据
        /// </summary>
        public static IDataObject? GetDataObjectWithRetry()
        {
            IDataObject? result = null;
            ExecuteWithRetry(() => {
                result = Clipboard.GetDataObject();
            });
            return result;
        }

        private static void ExecuteWithRetry(Action action)
        {
            int retryCount = 0;
            while (true)
            {
                try
                {
                    action();
                    return;
                }
                catch (COMException ex) when ((uint)ex.ErrorCode == 0x800401D0) // CLIPBRD_E_CANT_OPEN
                {
                    retryCount++;
                    if (retryCount >= MaxRetries)
                    {
                        throw; // 达到最大重试次数，向上抛出
                    }
                    System.Threading.Thread.Sleep(RetryDelayMs);
                }
                catch (Exception)
                {
                    throw; // 其他异常直接抛出
                }
            }
        }
    }
}
