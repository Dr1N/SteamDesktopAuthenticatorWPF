using System;
using System.Windows;
using System.Windows.Input;

namespace Authenticator
{
    /// <summary>
    /// Interaction logic for DialogWindow.xaml
    /// </summary>
    public partial class SimpleDialogWindow : Window
    {
        public string Answer { get; private set; }

        public SimpleDialogWindow(string question)
        {
            InitializeComponent();
            QuestionBlock.Text = question;
        }

        protected override void OnClosed(EventArgs e)
        {
            Answer = AnswerBox.Text.Trim();
            base.OnClosed(e);
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
