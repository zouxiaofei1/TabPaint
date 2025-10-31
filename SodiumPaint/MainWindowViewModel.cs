using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SodiumPaint.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {


        public string SelectedColor { get; set; } = "Black";

        public ICommand SelectPenCommand => new RelayCommand(_ =>
        {
            // 选择画笔的逻辑
        });

        public ICommand SelectEraserCommand => new RelayCommand(_ =>
        {
            // 选择橡皮擦的逻辑
        });

        public ICommand SaveCommand => new RelayCommand(_ =>
        {
            // 保存图像的逻辑
        });

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


   

    }

    // 简单命令类
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged;
    }
}
