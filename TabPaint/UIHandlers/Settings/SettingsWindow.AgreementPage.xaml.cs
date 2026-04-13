using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TabPaint.Services;

namespace TabPaint.Pages
{
    public partial class AgreementPage : UserControl
    {
        public AgreementPage()
        {
            InitializeComponent();
            this.Tag = "Agreement";
            this.Loaded += (s, e) => UpdatePinState();
        }

        private void Grid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            UpdatePinState();
        }

        private void UpdatePinState()
        {
            var win = Window.GetWindow(this) as SettingsWindow;
            if (win != null)
            {
                bool isPinned = win.IsPagePinned("Agreement");
                BtnPinTop.IsChecked = isPinned;
                TxtPin.Text = isPinned ? LocalizationManager.GetString("L_Settings_UnpinPage") : LocalizationManager.GetString("L_Settings_PinPage");
                BtnPinTop.ToolTip = TxtPin.Text;

                string pinData = isPinned
                    ? "M2,5.27L3.28,4L20,20.72L18.73,22L12.8,16.07V22H11.2V16H6V14L8,12V11.27L2,5.27M16,12L18,14V16H16.17L12.8,12.63V4H14V2H7V4H8V11.17L6.11,9.28L7.27,8.12L16,16.85V12H16Z"
                    : "M16,12V4H17V2H7V4H8V12L6,14V16H11.2V22H12.8V16H18V14L16,12M8.8,14L10,12.8V4H14V12.8L15.2,14H8.8Z";

                PinIcon.Data = Geometry.Parse(pinData);
                PinIconTop.Data = Geometry.Parse(pinData);
            }
        }

        private void MenuPin_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this) as SettingsWindow;
            win?.TogglePinPage("Agreement");
            UpdatePinState();
        }
    }
}