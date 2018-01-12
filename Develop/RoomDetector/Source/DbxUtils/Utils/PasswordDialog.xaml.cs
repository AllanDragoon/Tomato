// PasswordDialog.xaml.cs

using System.Windows;

namespace DbxUtils.Utils
{
    /// <summary>
    /// Password dialog window class.
    /// </summary>
    public partial class PasswordDialog : Window
    {
        /// <summary>
        /// Password dialog constructor creates and initializes a new password dialog.
        /// </summary>
        /// <param name="fileName">Name of the DWG file requiring a password.</param>
        public PasswordDialog(string fileName)
        {
            // Initialize the window components.
            //
            InitializeComponent();

            // Display the filename in the dialog.
            //
            fileTextBox.Text = fileName;
        }

        /// <summary>
        /// Password property contains the text from the dialog password box.
        /// </summary>
        public string Password
        {
            get { return passwordBox.Password;  }
        }

        // Event handling method closes the dialog and returns true.
        //
        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
