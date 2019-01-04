using System;
using System.Windows;
using System.Windows.Input;

namespace Authenticator.GUI.Commands
{
    public class ShowWindowCommand : ICommand
    {
        public event EventHandler CanExecuteChanged = delegate { };
        public Window Window { get; set; }

        public ShowWindowCommand(Window window)
        {
            Window = window;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            if (Window is AuthenticatorWindow currentWindow)
            {
                currentWindow.ShowWindow();
            }
        }
    }
}
