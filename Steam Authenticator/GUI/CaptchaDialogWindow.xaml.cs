using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Authenticator
{
    /// <summary>
    /// Interaction logic for CaptchaDialogWindow.xaml
    /// </summary>
    public partial class CaptchaDialogWindow : Window
    {
        private readonly string CaptchaUrlBase = @"https://steamcommunity.com/public/captcha.php?gid=";

        private string CaptchaUrl { get; set; }
        public string Answer { get; private set; }

        public CaptchaDialogWindow(string gid)
        {
            InitializeComponent();
            QuestionBlock.Text = "Enter Text From Image";
            CaptchaUrl = CaptchaUrlBase + gid;

            Loaded += CaptchaDialogWindow_Loaded;
        }

        private void CaptchaDialogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BitmapImage bitmap = new BitmapImage();
            try
            {
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(CaptchaUrl, UriKind.Absolute);
                bitmap.EndInit();
                CaptchaImage.Source = bitmap;
            }
            catch (InvalidOperationException ex)
            {
                App.Logger.Error($"CaptchaDialogWindow.Loaded Captcha Initialization Error: {ex.Message}");
                AnswerBox.Text = "Error";
                AnswerBox.IsEnabled = false;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
