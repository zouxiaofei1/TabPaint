using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace AutoCaptureTool.Tasks
{
    public interface ICaptureTask
    {
        string Name { get; }
        CaptureMode TargetMode { get; }
        void Execute(Window mainWindow, UIA3Automation automation, int processId, string themeName, string langName);
    }
}
