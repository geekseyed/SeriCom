using System.Windows;
using System.Windows.Controls;

namespace ComTransfer
{
    /// <summary>
    /// Password input window for confirmation
    /// </summary>
    public partial class PasswordWindow : Window
    {
        /// <summary>
        /// window title
        /// </summary>
        public string PasswordCaption { get; set; }
        /// <summary>
        /// Hint text
        /// </summary>
        public string PasswordMessage { get; set; }
        /// <summary>
        /// target password
        /// </summary>
        private readonly string PasswordTarget;
        /// <summary>
        /// entered password
        /// </summary>
        private string PasswordInput;
        /// <summary>
        /// whether hints are allowed
        /// </summary>
        public bool IsHintAvailable { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="owner">owner window</param>
        /// <param name="message">hint message</param>
        /// <param name="password">password text</param>
        /// <param name="caption">window title</param>
        /// <param name="isHint">whether hints are allowed</param>
        public PasswordWindow(Window owner, string message, string password, string caption, bool isHint)
        {
            Loaded += PasswordWindow_Loaded;

            Owner = owner;
            PasswordCaption = caption;
            PasswordMessage = message;
            PasswordTarget = password;
            PasswordInput = string.Empty;
            IsHintAvailable = isHint;

            DataContext = this;

            InitializeComponent();

            PasswordBox_Normal.Focus();
        }

        /// <summary>
        /// Window loaded
        /// </summary>
        /// <param name="sender">event sender</param>
        /// <param name="e">event arguments</param>
        private void PasswordWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PasswordTarget))
            {
                Close();
            }
        }

        /// <summary>
        /// Show the password window
        /// </summary>
        /// <param name="owner">owner window</param>
        /// <param name="message">hint message</param>
        /// <param name="password">password text</param>
        /// <param name="caption">window title</param>
        /// <param name="isHint">whether hints are allowed</param>
        /// <returns>whether confirmed</returns>
        public static bool? Show(Window owner, string message, string password, string caption = null, bool isHint = false)
        {
            return new PasswordWindow(owner, message, password, caption, isHint).ShowDialog();
        }

        /// <summary>
        /// OK button click
        /// </summary>
        /// <param name="sender">event sender</param>
        /// <param name="e">event arguments</param>
        private void Button_Submit_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordInput == PasswordTarget)
            {
                DialogResult = true;
            }
            else
            {
                Toast.IsEnabled = false;
                Toast.IsEnabled = true;
            }
        }

        /// <summary>
        /// Cancel button click
        /// </summary>
        /// <param name="sender">event sender</param>
        /// <param name="e">event arguments</param>
        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        /// <summary>
        /// Password box change event handler
        /// </summary>
        /// <param name="sender">event sender</param>
        /// <param name="e">event arguments</param>
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordInput = ((PasswordBox)sender).Password;
        }

        /// <summary>
        /// TextBox change event handler
        /// </summary>
        /// <param name="sender">event sender</param>
        /// <param name="e">event arguments</param>
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PasswordInput = ((TextBox)sender).Text;
        }

        /// <summary>
        /// Toggle password visibility button click
        /// </summary>
        /// <param name="sender">event sender</param>
        /// <param name="e">event arguments</param>
        private void Button_View_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordBox_View.IsVisible)
            {
                PasswordBox_View.Visibility = Visibility.Collapsed;
                PasswordBox_Normal.Password = PasswordInput;
                PasswordBox_Normal.Focus();
                Button_Toggle.Tag = true;
            }
            else
            {
                PasswordBox_View.Visibility = Visibility.Visible;
                PasswordBox_View.Text = PasswordInput;
                PasswordBox_View.Focus();
                Button_Toggle.Tag = false;
            }
        }
    }
}
