using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Media;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Win32;
using System.IO;

namespace ChatClient
{
    public partial class MainWindow : Window
    {
        private HubConnection? _connection;
        private readonly ChatViewModel _chatViewModel = new();
        // Сообщения текущего открытого диалога
        private readonly ObservableCollection<ChatMessageView> _currentMessages = new();

        // Список контактов
        private readonly ObservableCollection<ContactView> _contacts = new();

        // Email текущего собеседника
        private string? _currentDialogEmail;

        public MainWindow() : this(Session.Name ?? string.Empty)
        {
        }

        public MainWindow(string userName)
        {
            InitializeComponent();

            _chatViewModel.UserName = userName;
            _chatViewModel.Messages = _currentMessages;
            _chatViewModel.ConnectionStatusBrush = Brushes.Gray;
            _chatViewModel.IsConversationSelected = false;
            DataContext = _chatViewModel;
            ChatView.DataContext = _chatViewModel;
            ChatView.SendRequested += SendButton_Click;
            ChatView.AttachRequested += AttachButton_Click;
            ChatView.ProfileRequested += ProfileButton_Click;
            ChatView.LogoutRequested += LogoutButton_Click;
            ChatView.MessageDoubleClicked += OnMessageDoubleClicked;
            ChatView.MessageEditRequested += OnMessageEditRequested;
            ChatView.MessageDeleteRequested += OnMessageDeleteRequested;

            // Привязываем источники данных

            ContactsListBox.ItemsSource = _contacts;

            InitializeConnection();

            // Загрузка контактов
            _ = LoadContactsAsync();

            Activated += (_, _) => NotificationService.IsAppActive = true;
            Deactivated += (_, _) => NotificationService.IsAppActive = false;
            StateChanged += (_, _) => NotificationService.IsAppActive = WindowState != WindowState.Minimized;

        }

        // Иницилизация подключения
        private static readonly string ServerBaseUrl = ServerConfig.ServerBaseUrl;
        private static readonly string HubUrl = ServerConfig.HubUrl;

        private void InitializeConnection()
        {

            // email текущего пользователя
            var email = Session.Email ?? string.Empty;

            var urlWithUser = $"{HubUrl}?user={Uri.EscapeDataString(email)}";

            _connection = new HubConnectionBuilder().WithUrl(urlWithUser).WithAutomaticReconnect().Build();

            // Старый общий чат
            _connection.On<string, string>("ReceiveMessage", (user, message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _currentMessages.Add(new ChatMessageView
                    {
                        FromEmail = user,
                        ToEmail = "(всем)",
                        Text = message,
                        Timestamp = DateTime.Now,
                        DeliveryStatus = MessageDeliveryStatus.Sent
                    });
                });
            });

            // Личные сообщения и операции с ними
            _connection.On<object>("ReceivePrivateMessage", OnReceivePrivateMessage);
            _connection.On<int, string>("MessageStatusChanged", OnMessageStatusChanged);
            _connection.On<int, string>("MessageEdited", OnMessageEdited);
            _connection.On<int>("MessageDeleted", OnMessageDeleted);

            _connection.On<UserDto>("UserProfileChanged", (u) =>
            {
                Dispatcher.Invoke(() =>
                {
                    // обновляем контакт в списке
                    var c = _contacts.FirstOrDefault(x =>
                        x.Email.Equals(u.Email, StringComparison.OrdinalIgnoreCase));
                    if (c != null)
                    {
                        c.Name = u.Name;
                        if (!string.IsNullOrWhiteSpace(u.AvatarUrl))
                        {
                            // Перезагрузка картинки
                            var url = (u.AvatarUrl.StartsWith("/") ? ServerBaseUrl + u.AvatarUrl : u.AvatarUrl);
                            url += (url.Contains("?") ? "&" : "?") + "v=" + DateTime.UtcNow.Ticks;
                            c.AvatarPath = url;
                        }
                        else
                        {
                            c.AvatarPath = null;
                        }
                    }

                    // Если обновился текущий выбранный контакт, то принудительно обновляем отображение
                    if (ContactsListBox.SelectedItem == c)
                    {
                        ContactsListBox.Items.Refresh();
                    }
                });
            });

            _connection.On<UserDto>("UserRegistered", (u) =>
            {
                Dispatcher.Invoke(() =>
                {
                    // не добавляем самого себя
                    if (u.Email.Equals(Session.Email, StringComparison.OrdinalIgnoreCase))
                        return;

                    // если уже есть, то обновим имя/аватар
                    var existing = _contacts.FirstOrDefault(x => x.Email.Equals(u.Email, StringComparison.OrdinalIgnoreCase));

                    string? avatar = null;
                    if (!string.IsNullOrWhiteSpace(u.AvatarUrl))
                    {
                        var url = (u.AvatarUrl.StartsWith("/") ? ServerBaseUrl + u.AvatarUrl : u.AvatarUrl);
                        url += (url.Contains("?") ? "&" : "?") + "v=" + DateTime.UtcNow.Ticks;
                        avatar = url;
                    }

                    if (existing != null)
                    {
                        existing.Name = u.Name;
                        existing.AvatarPath = avatar;
                        existing.Status = u.Status == 1 ? "Online" : (u.Status == 2 ? "DoNotDisturb" : "Offline");
                        ContactsListBox.Items.Refresh();
                        return;
                    }

                    // новый контакт
                    _contacts.Add(new ContactView
                    {
                        Email = u.Email,
                        Name = u.Name,
                        AvatarPath = avatar,
                        Status = u.Status == 1 ? "Online" : (u.Status == 2 ? "DoNotDisturb" : "Offline")
                    });
                });
            });



            _connection.On<string, string>("UserStatusChanged", (email, status) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var contact = _contacts.FirstOrDefault(c =>
                        c.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
                    if (contact != null)
                    {
                        contact.Status = status;
                    }
                });
            });
            ConnectToServer(urlWithUser);
        }

        private async void ConnectToServer(string hubUrl)
        {
            try
            {
                

                await _connection.StartAsync();
                _chatViewModel.ConnectionStatus = "Подключено";
                _chatViewModel.ConnectionStatusBrush = Brushes.Green;

               
            }
            catch (Exception ex)
            {
                _chatViewModel.ConnectionStatus = "Подключено";
                _chatViewModel.ConnectionStatusBrush = Brushes.Green;
               
            }
        }

        // Загрузка контактов
        private async Task LoadContactsAsync()
        {
            try
            {
                using var client = new HttpClient();
                var response = await client.GetAsync($"{ServerBaseUrl}/api/Auth/users");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var users = JsonSerializer.Deserialize<List<UserDto>>(json, options) ?? new();
                _contacts.Clear();
                foreach (var u in users.Where(u => u.Email != Session.Email))
                {
                    string? avatar = null;
                    if (!string.IsNullOrWhiteSpace(u.AvatarUrl))
                    {
                        avatar = $"{ServerBaseUrl}{u.AvatarUrl}";
                    }
                    _contacts.Add(new ContactView
                    {
                        Email = u.Email,
                        Name = u.Name,
                        AvatarPath = avatar,
                        Status = NormalizeStatus(u.Status)
                    });
                }
            }
            catch { }
        }

        // Входящие сообщения
        private async void OnReceivePrivateMessage(object raw)
        {
            var json = JsonSerializer.Serialize(raw);
            var msg = JsonSerializer.Deserialize<ChatMessageView>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (msg == null) return;

            Dispatcher.Invoke(() =>
            {
                // добавляем в текущий список сообщений, если подходит под открытый диалог
                if (_currentDialogEmail != null &&
                  (msg.FromEmail == _currentDialogEmail ||
                  msg.ToEmail == _currentDialogEmail))
                {
                    _currentMessages.Add(msg);
                    ScrollMessagesToBottom();
                }
            });

            // Уведомления только для входящих
            var isIncoming = string.Equals(msg.ToEmail, Session.Email, StringComparison.OrdinalIgnoreCase);
            var isActiveDialog = string.Equals(_currentDialogEmail, msg.FromEmail, StringComparison.OrdinalIgnoreCase);

            Dispatcher.Invoke(() =>
            {
                var isWindowActive = this.IsActive && this.WindowState != WindowState.Minimized;

                if (isIncoming && (!isWindowActive || !isActiveDialog))
                {
                    var isIncoming = string.Equals(msg.ToEmail, Session.Email, StringComparison.OrdinalIgnoreCase);
                    if (isIncoming)
                    {
                        if (Session.Status == "DoNotDisturb")
                            return;
                        NotificationService.Show(msg);
                    }
                }
            });


            // отметить доставлено/прочитано только для входящих
            if (isIncoming)
            {
                try
                {
                    await _connection.InvokeAsync("MarkDelivered", msg.Id);
                    await _connection.InvokeAsync("MarkRead", msg.Id);
                }
                catch { }
            }
        }

        private void OnMessageStatusChanged(int id, string status)
        {
            Dispatcher.Invoke(() =>
            {
                var msg = _currentMessages.FirstOrDefault(m => m.Id == id);
                if (msg != null)
                {
                    msg.DeliveryStatus = ParseDeliveryStatus(status);
                }
            });
        }


        private void OnMessageEdited(int id, string newText)
        {
            Dispatcher.Invoke(() =>
            {
                var msg = _currentMessages.FirstOrDefault(m => m.Id == id);
                if (msg == null)
                    return;

                msg.Text = newText;
                msg.IsEdited = true;
            });
        }


        private void OnMessageDeleted(int id)
        {
            Dispatcher.Invoke(() =>
            {
                var msg = _currentMessages.FirstOrDefault(m => m.Id == id);
                if (msg != null)
                {
                    _currentMessages.Remove(msg);
                }
            });
        }

        // Отправка сообщений
        private async void SendButton_Click(object? sender, EventArgs e)
        {
            var text = _chatViewModel.MessageText;
            if (string.IsNullOrWhiteSpace(text) || _connection == null || string.IsNullOrEmpty(_currentDialogEmail))
                return;

            try
            {
                await _connection.InvokeAsync("SendPrivateMessage", _currentDialogEmail, text, false, null, null);
                _chatViewModel.MessageText = string.Empty;
                ScrollMessagesToBottom();

            }
            catch (Exception ex)
            {
                _currentMessages.Add(new ChatMessageView
                {
                    Text = $"Ошибка при отправке: {ex.Message}",
                    FromEmail = "system",
                    Timestamp = DateTime.Now
                });
            }
        }



        // Контакты и диалоги
        private async void ContactsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var contact = ContactsListBox.SelectedItem as ContactView;
            if (contact == null)
            {
                _currentDialogEmail = null;
                NotificationService.ActiveDialogEmail = null;
                _chatViewModel.IsConversationSelected = false;
                _currentMessages.Clear();
                return;
            }

            if (_connection == null || _connection.State != HubConnectionState.Connected)
                return;

            _currentDialogEmail = contact.Email;
            NotificationService.ActiveDialogEmail = _currentDialogEmail;
            _chatViewModel.IsConversationSelected = true;

            try
            {
                var messages = await _connection.InvokeAsync<List<ChatMessageView>>("GetDialogMessages", _currentDialogEmail);
                _currentMessages.Clear();
                foreach (var m in messages)
                    _currentMessages.Add(m);

                var unread = messages.Where(m => m.ToEmail == Session.Email && m.DeliveryStatus != MessageDeliveryStatus.Read).Select(m => m.Id).ToList();

                foreach (var id in unread)
                {
                    try
                    {
                        await _connection.InvokeAsync("MarkRead", id);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _currentMessages.Add(new ChatMessageView
                {
                    FromEmail = "system",
                    Text = $"Ошибка при загрузке диалога: {ex.Message}",
                    Timestamp = DateTime.Now
                });
            }
        }

        private void FindContactButton_Click(object sender, RoutedEventArgs e)
        {
            var query = SearchContactTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(query))
                return;

            var found = _contacts.FirstOrDefault(c => c.Email.Contains(query, StringComparison.OrdinalIgnoreCase) || c.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

            if (found != null)
            {
                ContactsListBox.SelectedItem = found;
                ContactsListBox.ScrollIntoView(found);
            }
        }

        // Профиль
        private void ProfileButton_Click(object? sender, EventArgs e)
        {
            var profileWindow = new ProfileWindow { Owner = this };
            profileWindow.ShowDialog();
            _chatViewModel.UserName = Session.Name ?? string.Empty;
            _ = LoadContactsAsync();
        }


        // Выход
        private async void LogoutButton_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_connection != null)
                {
                    try
                    {
                        await _connection.InvokeAsync("Logout");
                    }
                    catch
                    {
                    }
                    await _connection.StopAsync();
                    await _connection.DisposeAsync();
                    _connection = null;
                }
            }
            catch { }

            ClientConfig.Clear();
            Session.Email = "";
            Session.Name = "";
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }

        // Приклепление файлов
        private async void AttachButton_Click(object? sender, EventArgs e)
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
                return;

            if (string.IsNullOrEmpty(_currentDialogEmail))
            {
                
                return;
            }

            var dlg = new OpenFileDialog
            {
                Title = "Выберите файл",
                Filter = "Все файлы (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true)
                return;

            var filePath = dlg.FileName;
            var fileName = Path.GetFileName(filePath);
            byte[] bytes;

            try
            {
                bytes = File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                _currentMessages.Add(new ChatMessageView
                {
                    FromEmail = "system",
                    Text = $"Не удалось прочитать файл: {ex.Message}",
                    Timestamp = DateTime.Now
                });
                return;
            }

            try
            {
                await _connection.InvokeAsync("SendPrivateMessage", _currentDialogEmail, "", true, fileName, bytes);
            }
            catch (Exception ex)
            {
                _currentMessages.Add(new ChatMessageView
                {
                    FromEmail = "system",
                    Text = $"Ошибка при отправке файла: {ex.Message}",
                    Timestamp = DateTime.Now
                });
            }
        }

        private void OnMessageDoubleClicked(object? sender, ChatMessageEventArgs e)
        {
            var msg = e.Message;


            if (!msg.IsFile || msg.FileContent == null || msg.FileContent.Length == 0)
                return;

            var sfd = new SaveFileDialog
            {
                FileName = msg.FileName ?? "file",
                Title = "Сохранить файл"
            };

            if (sfd.ShowDialog() != true)
                return;

            try
            {
                File.WriteAllBytes(sfd.FileName, msg.FileContent);
            }
            catch (Exception ex)
            {
                _currentMessages.Add(new ChatMessageView
                {
                    FromEmail = "system",
                    Text = $"Не удалось сохранить файл: {ex.Message}",
                    Timestamp = DateTime.Now
                });
            }
        }

        private async void OnMessageEditRequested(object? sender, ChatMessageEventArgs e)
        {
            await EditMessageAsync(e.Message);
        }

        private async void OnMessageDeleteRequested(object? sender, ChatMessageEventArgs e)
        {
            await DeleteMessageAsync(e.Message);
        }

        private async Task EditMessageAsync(ChatMessageView msg)
        {
            if (!msg.FromEmail.Equals(Session.Email, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Можно редактировать только свои сообщения.");
                return;
            }

            var editWindow = new EditMessageWindow(msg.Text)
            {
                Owner = this
            };

            if (editWindow.ShowDialog() != true)
                return;

            var newText = editWindow.MessageText;

            if (string.IsNullOrWhiteSpace(newText) || newText == msg.Text)
                return;

            try
            {
                await _connection.InvokeAsync("EditMessage", msg.Id, newText);
                msg.Text = newText;
                msg.IsEdited = true;
            }
            catch (Exception ex)
            {
                _currentMessages.Add(new ChatMessageView
                {
                    FromEmail = "system",
                    Text = $"Ошибка при редактировании: {ex.Message}",
                    Timestamp = DateTime.Now
                });
            }
        }

        private async Task DeleteMessageAsync(ChatMessageView msg)
        {
            if (!msg.FromEmail.Equals(Session.Email, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Можно удалять только свои сообщения.");
                return;
            }

            if (MessageBox.Show("Удалить сообщение?", "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await _connection.InvokeAsync("DeleteMessage", msg.Id);
            }
            catch (Exception ex)
            {
                _currentMessages.Add(new ChatMessageView
                {
                    FromEmail = "system",
                    Text = $"Ошибка при удалении: {ex.Message}",
                    Timestamp = DateTime.Now
                });
            }
        }

        private async Task ReloadCurrentDialogAsync()
        {
            if (string.IsNullOrEmpty(_currentDialogEmail) || _connection == null || _connection.State != HubConnectionState.Connected)
                return;
            try
            {
                var messages = await _connection.InvokeAsync<List<ChatMessageView>>("GetDialogMessages", _currentDialogEmail);
                _currentMessages.Clear();
                foreach (var m in messages)
                    _currentMessages.Add(m);
                ScrollMessagesToBottom();
            }
            catch (Exception ex)
            {
                _currentMessages.Add(new ChatMessageView
                {
                    FromEmail = "system",
                    Text = $"Ошибка при обновлении диалога: {ex.Message}",
                    Timestamp = DateTime.Now
                });
            }
        }

        private static string NormalizeStatus(object? s)
        {
            if (s == null) return "Offline";

            // если вдруг пришло числом
            if (s is int n)
                return n switch { 1 => "Online", 2 => "DoNotDisturb", _ => "Offline" };

            var str = s.ToString()?.Trim() ?? "";
            if (int.TryParse(str, out var n2))
                return n2 switch { 1 => "Online", 2 => "DoNotDisturb", _ => "Offline" };

            if (str.Equals("DoNotDisturb", StringComparison.OrdinalIgnoreCase)) return "DoNotDisturb";
            if (str.Equals("Online", StringComparison.OrdinalIgnoreCase)) return "Online";
            return "Offline";
        }

        private static MessageDeliveryStatus ParseDeliveryStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return MessageDeliveryStatus.Sent;

            return Enum.TryParse(status, true, out MessageDeliveryStatus parsed)
                ? parsed
                : MessageDeliveryStatus.Sent;
        }

        private void ScrollMessagesToBottom()
        {
            if (_currentMessages.Count == 0) return;
            ChatView.ScrollMessagesToBottom();
        }
    }
}
