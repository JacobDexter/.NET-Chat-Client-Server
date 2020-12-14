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
            this.Height = 510.0f;
            this.Width = 1080.0f;

            _client = client;
        }

        public void UpdateChatWindow(string message)
        {
            MessageBox.Dispatcher.Invoke(() =>
            {
                ListBoxItem item = new ListBoxItem
                {
                    Content = message,
                    Focusable = false
                };
                MessageBox.Items.Add(item);
                MessageBox.ScrollIntoView(item);
            });
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if(InputField.Text.Length > 0 && !string.IsNullOrWhiteSpace(InputField.Text))//check if text box has anything in it or just whitespace
            {
                _client.TCPSendMessage(InputField.Text);
                InputField.Clear();
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            _client.Connect("127.0.0.1", 4444);
            _client.Login();
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            _client.DisconnectClient();
        }

        private void SetNickname_Click(object sender, RoutedEventArgs e)
        {
            _client.SetNickname(Nickname.Text);
            Nickname.Clear();
        }
        
        public void RefreshClientList(string[] clients, string[] ips)
        {
            ClientList.Dispatcher.Invoke(() =>
            {
                ClientList.Items.Clear();

                for (int i = 0; i < clients.Length; i++)
                {
                    ListBoxItem item = new ListBoxItem();
                    item.Content = clients[i] + " - " + ips[0];
                    item.Focusable = false;
                    ClientList.Items.Add(item);
                }
            });
        }
    }
}
