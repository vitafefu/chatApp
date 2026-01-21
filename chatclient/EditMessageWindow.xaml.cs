using System.Windows;

namespace ChatClient
{
    public partial class EditMessageWindow : Window
    {
        public EditMessageWindow(string messageText)
        {
            InitializeComponent();
            MessageTextBox.Text = messageText;
            MessageTextBox.SelectAll();
            MessageTextBox.Focus();
        }

        public string MessageText => MessageTextBox.Text;

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}