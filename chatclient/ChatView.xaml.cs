using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChatClient
{
    public partial class ChatView : UserControl
    {
        public event EventHandler? SendRequested;
        public event EventHandler? AttachRequested;
        public event EventHandler? ProfileRequested;
        public event EventHandler? LogoutRequested;
        public event EventHandler<ChatMessageEventArgs>? MessageDoubleClicked;
        public event EventHandler<ChatMessageEventArgs>? MessageEditRequested;
        public event EventHandler<ChatMessageEventArgs>? MessageDeleteRequested;

        public ChatView()
        {
            InitializeComponent();
        }

        public void ScrollMessagesToBottom()
        {
            if (MessagesListBox.Items.Count == 0)
                return;

            var last = MessagesListBox.Items[MessagesListBox.Items.Count - 1];
            MessagesListBox.ScrollIntoView(last);
        }

        private void OnSendClick(object sender, System.Windows.RoutedEventArgs e)
        {
            SendRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnAttachClick(object sender, System.Windows.RoutedEventArgs e)
        {
            AttachRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnProfileClick(object sender, System.Windows.RoutedEventArgs e)
        {
            ProfileRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnLogoutClick(object sender, System.Windows.RoutedEventArgs e)
        {
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnMessageKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnMessagesListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MessagesListBox.SelectedItem is ChatMessageView message)
            {
                MessageDoubleClicked?.Invoke(this, new ChatMessageEventArgs(message));
            }
        }

        private void OnEditMessageClick(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is Button { Tag: ChatMessageView message })
            {
                MessageEditRequested?.Invoke(this, new ChatMessageEventArgs(message));
            }
        }

        private void OnDeleteMessageClick(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is Button { Tag: ChatMessageView message })
            {
                MessageDeleteRequested?.Invoke(this, new ChatMessageEventArgs(message));
            }
        }
    }

    public class ChatMessageEventArgs : EventArgs
    {
        public ChatMessageEventArgs(ChatMessageView message)
        {
            Message = message;
        }

        public ChatMessageView Message { get; }
    }
}
