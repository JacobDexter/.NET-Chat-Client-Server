using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Client
{
    /// <summary>
    /// Interaction logic for ClientForm.xaml
    /// </summary>
    public partial class ClientForm : Window
    {
        private Client _client;

        public ClientForm(Client client)
        {
            InitializeComponent();

            _client = client;
        }

        public void UpdateChatWindow(string message)
        {
            MessageBox.Dispatcher.Invoke(() =>
            {
                MessageBox.Text += message + Environment.NewLine;
                MessageBox.ScrollToEnd();
            });
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if(InputField.Text.Length > 0 && !string.IsNullOrWhiteSpace(InputField.Text))//check if text box has anything in it or just whitespace
            {
                _client.SendMessage(InputField.Text);
                InputField.Clear();
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
