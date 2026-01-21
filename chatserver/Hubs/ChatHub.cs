using ChatServer.Dtos;
using ChatServer.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.IO; 
using System.Linq;
using System.Text.Json;

namespace ChatServer
{
    public class ChatHub : Hub
    {
        // email по connectionId
        private static readonly ConcurrentDictionary<string, string> _connections =
            new ConcurrentDictionary<string, string>();

        // Простое хранилище сообщений
        private static readonly List<ChatMessage> _messages = new List<ChatMessage>();
        private static int _nextId = 0;

        // Файл диалогов
        private const string DialogsFilePath = "dialogs.json";

        // ключ = "email1|email2"
        private static readonly Dictionary<string, List<ChatMessage>> _dialogs = LoadDialogs();

        private string? GetUserEmail()
        {
            if (Context.GetHttpContext()?.Request.Query.TryGetValue("user", out var values) == true)
                return values.ToString();

            return null;
        }

        // Подключение и отключение
        public override async Task OnConnectedAsync()
        {
            var email = GetUserEmail();
            if (!string.IsNullOrEmpty(email))
            {
                _connections[Context.ConnectionId] = email!;

                var user = ChatServer.Models.UserStore.GetByEmail(email!);
                if (user != null)
                {
                    if (user.Status == UserStatus.Offline) 
                    {
                        user.Status = UserStatus.Online;
                    }

                    ChatServer.Models.UserStore.UpdateUser(user);

                    await Clients.All.SendAsync("UserRegistered", UserDto.FromUser(user));
                    await Clients.All.SendAsync("UserStatusChanged", user.Email, user.Status.ToString());
                }

            }

            await base.OnConnectedAsync();
        }


        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_connections.TryRemove(Context.ConnectionId, out var email))
            {
                // если у пользователя больше нет активных соединений
                if (!GetConnectionsByEmail(email).Any())
                {
                    var user = ChatServer.Models.UserStore.GetByEmail(email);
                    if (user != null)
                    {
                        user.Status = ChatServer.Models.UserStatus.Offline;
                        ChatServer.Models.UserStore.UpdateUser(user);

                        await Clients.All.SendAsync("UserStatusChanged", user.Email, user.Status.ToString());
                    }

                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task Logout()
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email))
                return;

            foreach (var pair in _connections.Where(p => p.Value.Equals(email, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _connections.TryRemove(pair.Key, out _);
            }

            var user = ChatServer.Models.UserStore.GetByEmail(email);
            if (user == null)
                return;

            user.Status = ChatServer.Models.UserStatus.Offline;
            ChatServer.Models.UserStore.UpdateUser(user);

            await Clients.All.SendAsync("UserStatusChanged", user.Email, user.Status.ToString());
        }

        private IEnumerable<string> GetConnectionsByEmail(string email)
        {
            return _connections.Where(p => p.Value == email).Select(p => p.Key);
        }

        // Общий чат
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        // Личные сообщения
        public async Task SendPrivateMessage(string toEmail, string text, bool isFile, string? fileName, byte[]? fileContent)

        {
            var fromEmail = GetUserEmail();
            if (string.IsNullOrEmpty(fromEmail))
                return;

            var msg = new ChatMessage
            {
                Id = Interlocked.Increment(ref _nextId),
                FromEmail = fromEmail!,
                ToEmail = toEmail,
                Text = text,
                Timestamp = DateTime.UtcNow,
                IsFile = isFile,
                FileName = fileName,
                FileContent = fileContent,
                Status = MessageStatus.Sent
            };

            lock (_messages)
            {
                _messages.Add(msg);
            }

            // Добавляем в общий диалог для пары пользователей
            var key = GetDialogKey(fromEmail!, toEmail);

            lock (_dialogs)
            {
                if (!_dialogs.TryGetValue(key, out var list))
                {
                    list = new List<ChatMessage>();
                    _dialogs[key] = list;
                }

                list.Add(msg);
            }
            SaveDialogs();

            // Отправляем отправителю и получателю
            var targets = GetConnectionsByEmail(msg.FromEmail).Concat(GetConnectionsByEmail(msg.ToEmail)).Distinct();
            await Clients.Clients(targets).SendAsync("ReceivePrivateMessage", msg);

        }


        // История диалога между текущим пользователем и другим
        public Task<List<ChatMessage>> GetDialogMessages(string withEmail)
        {
            var currentUserEmail = GetUserEmail() ?? "";

            if (string.IsNullOrWhiteSpace(currentUserEmail) || string.IsNullOrWhiteSpace(withEmail))
                return Task.FromResult(new List<ChatMessage>());

            List<ChatMessage> result;

            lock (_dialogs)
            {
                // Берём вообще все сообщения из всех диалогов
                result = _dialogs.Values.SelectMany(list => list).Where(m =>
                        // current -> with
                        string.Equals(m.FromEmail, currentUserEmail, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(m.ToEmail, withEmail, StringComparison.OrdinalIgnoreCase)
                        ||
                        // with -> current
                        string.Equals(m.FromEmail, withEmail, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(m.ToEmail, currentUserEmail, StringComparison.OrdinalIgnoreCase)
                    ).OrderBy(m => m.Timestamp).ToList();
            }
            return Task.FromResult(result);
        }



        private static string GetDialogKey(string user1, string user2)
        {
            return string.CompareOrdinal(user1, user2) < 0 ? $"{user1}|{user2}" : $"{user2}|{user1}";
        }

        // Загружаем диалоги из файла при старте приложения
        private static Dictionary<string, List<ChatMessage>> LoadDialogs()
        {
            try
            {
                if (!File.Exists(DialogsFilePath))
                    return new Dictionary<string, List<ChatMessage>>(StringComparer.OrdinalIgnoreCase);

                var json = File.ReadAllText(DialogsFilePath);

                var allMessages = JsonSerializer.Deserialize<List<ChatMessage>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ChatMessage>();

                if (allMessages.Count > 0)
                {
                    _nextId = allMessages.Max(m => m.Id);
                }

                var dict = new Dictionary<string, List<ChatMessage>>(StringComparer.OrdinalIgnoreCase);

                foreach (var m in allMessages)
                {
                    var key = GetDialogKey(m.FromEmail, m.ToEmail);
                    if (!dict.TryGetValue(key, out var list))
                    {
                        list = new List<ChatMessage>();
                        dict[key] = list;
                    }

                    list.Add(m);
                }
                return dict;
            }
            catch
            {
                return new Dictionary<string, List<ChatMessage>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        // Сохраняем все диалоги в один json-файл
        private static void SaveDialogs()
        {
            try
            {
                List<ChatMessage> allMessages;

                lock (_dialogs)
                {
                    allMessages = _dialogs.Values.SelectMany(list => list).OrderBy(m => m.Timestamp).ToList();
                }

                var json = JsonSerializer.Serialize(allMessages, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(DialogsFilePath, json);
            }
            catch { }
        }

        public async Task MarkDelivered(int messageId)
        {
            ChatMessage? msg;
            lock (_dialogs)
            {
                msg = _dialogs.Values.SelectMany(x => x).FirstOrDefault(m => m.Id == messageId);
                if (msg == null) return;

                msg.Status = MessageStatus.Delivered;
                SaveDialogs();
            }

            // отправляем только двум участникам диалога
            var targets = GetConnectionsByEmail(msg.FromEmail).Concat(GetConnectionsByEmail(msg.ToEmail)).Distinct().ToList();

            await Clients.Clients(targets).SendAsync("MessageStatusChanged", messageId, "Delivered");
        }

        public async Task MarkRead(int messageId)
        {
            ChatMessage? msg;
            lock (_dialogs)
            {
                msg = _dialogs.Values.SelectMany(x => x).FirstOrDefault(m => m.Id == messageId);
                if (msg == null) return;

                msg.Status = MessageStatus.Read;
                SaveDialogs();
            }

            // отправляем только двум участникам диалога
            var targets = GetConnectionsByEmail(msg.FromEmail).Concat(GetConnectionsByEmail(msg.ToEmail)).Distinct().ToList();
            await Clients.Clients(targets).SendAsync("MessageStatusChanged", messageId, "Read");
        }


        public async Task EditMessage(int messageId, string newText)
        {
            lock (_dialogs)
            {
                var msg = _dialogs.Values.SelectMany(x => x).FirstOrDefault(m => m.Id == messageId);
                if (msg != null)
                {
                    msg.Text = newText;
                    msg.Timestamp = DateTime.UtcNow;
                    SaveDialogs();
                }
                else return;
            }
            await Clients.All.SendAsync("MessageEdited", messageId, newText);
        }

        public async Task DeleteMessage(int messageId)
        {
            bool removed = false;
            lock (_dialogs)
            {
                foreach (var list in _dialogs.Values)
                {
                    var msg = list.FirstOrDefault(m => m.Id == messageId);
                    if (msg != null)
                    {
                        list.Remove(msg);
                        removed = true;
                        break;
                    }
                }

                if (removed)
                    SaveDialogs();
            }

            if (removed)
                await Clients.All.SendAsync("MessageDeleted", messageId);
        }

        public async Task SetStatus(int status)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email))
                return;

            var user = UserStore.GetByEmail(email);
            if (user == null)
                return;

            user.Status = (UserStatus)status;
            UserStore.UpdateUser(user);

            await Clients.All.SendAsync("UserStatusChanged", user.Email, user.Status.ToString());
        }

        public async Task BroadcastUserProfileChanged(UserDto user)
        {
            await Clients.All.SendAsync("UserProfileChanged", user);
        }
    }
}
