using System;
using System.Diagnostics;
using System.Windows;

namespace Authenticator
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            { 
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
                e.Handled = true;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"AboutWindow.RequestNavigate Error: {ex.Message}");
            }
        }
    }
}
