using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ChatClient
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private string _userName = string.Empty;
        private string _connectionStatus = string.Empty;
        private Brush? _connectionStatusBrush;
        private string _messageText = string.Empty;
        private ObservableCollection<ChatMessageView> _messages = new();
        private bool _isConversationSelected;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string UserName
        {
            get => _userName;
            set
            {
                if (_userName == value) return;
                _userName = value;
                OnPropertyChanged();
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                if (_connectionStatus == value) return;
                _connectionStatus = value;
                OnPropertyChanged();
            }
        }

        public Brush? ConnectionStatusBrush
        {
            get => _connectionStatusBrush;
            set
            {
                if (_connectionStatusBrush == value) return;
                _connectionStatusBrush = value;
                OnPropertyChanged();
            }
        }

        public string MessageText
        {
            get => _messageText;
            set
            {
                if (_messageText == value) return;
                _messageText = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ChatMessageView> Messages
        {
            get => _messages;
            set
            {
                if (_messages == value) return;
                _messages = value;
                OnPropertyChanged();
            }
        }
        public bool IsConversationSelected
        {
            get => _isConversationSelected;
            set
            {
                if (_isConversationSelected == value) return;
                _isConversationSelected = value;
                OnPropertyChanged();
            }
        }
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}