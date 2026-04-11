using System;

namespace AutoCaptureTool
{
    public static class AppConsts
    {
        /// <summary>
        /// 短暂延迟，用于点击、选择后的微小等待
        /// </summary>
        public const int DelayShort = 100;

        /// <summary>
        /// 普通操作延迟，如关闭窗口后的等待
        /// </summary>
        public const int DelayNormal = 500;

        /// <summary>
        /// 窗口打开延迟，等待子窗口弹出并渲染
        /// </summary>
        public const int DelayWindowOpen = 500;

        /// <summary>
        /// UI 初始化延迟，等待主窗口加载完成
        /// </summary>
        public const int DelayUIInit = 1000;

        /// <summary>
        /// 任务之间的间隔延迟
        /// </summary>
        public const int DelayBetweenTasks = 500;

        /// <summary>
        /// 较长延迟，用于等待 UI 重绘或动画完成
        /// </summary>
        public const int DelayLong = 500;

        /// <summary>
        /// 方案切换延迟，如切换语言或主题后的等待
        /// </summary>
        public const int DelayBetweenSchemes = 1000;

        /// <summary>
        /// 主窗口获取超时时间
        /// </summary>
        public static readonly TimeSpan TimeoutMainWindow = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 进程退出等待超时
        /// </summary>
        public const int TimeoutProcessExit = 500;

        /// <summary>
        /// 查找窗口的最大重试次数
        /// </summary>
        public const int MaxRetryFindWindow = 10;
    }
}
