using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ChatClient
{
    public partial class LoginWindow : Window
    {
        private static readonly string BaseUrl = ServerConfig.ServerBaseUrl;
        private readonly HttpClient _httpClient = new HttpClient();

        public LoginWindow()
        {
            InitializeComponent();
        }

        // DTO для логина
        private class LoginRequest
        {
            public string Email { get; set; } = null!;
            public string Password { get; set; } = null!;
        }

        private class LoginResponse
        {
            public Guid Id { get; set; }
            public string Email { get; set; } = null!;
            public string Name { get; set; } = null!;
        }

        // DTO для регистрации
        private class RegisterRequest
        {
            public string Email { get; set; } = null!;
            public string Password { get; set; } = null!;
            public string Name { get; set; } = null!;
        }

        // При загрузке окна пробуем автоматический вход.
        private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var cfg = ClientConfig.Load();

            if (!string.IsNullOrWhiteSpace(cfg.Email) && !string.IsNullOrWhiteSpace(cfg.Password))
            {
                EmailTextBox.Text = cfg.Email;
                PasswordBox.Password = cfg.Password;
                await DoLoginAsync(auto: true);
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await DoLoginAsync(auto: false);
        }

        // Общий код логина (авто и ручной)
        private async Task DoLoginAsync(bool auto)
        {
            StatusTextBlock.Text = auto ? "Автовход..." : "Выполняется вход...";
            var email = EmailTextBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                StatusTextBlock.Text = "Введите email и пароль.";
                return;
            }

            try
            {
                var req = new LoginRequest
                {
                    Email = email,
                    Password = password
                };

                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseUrl}/api/Auth/login", content);

                if (!response.IsSuccessStatusCode)
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    if (auto) StatusTextBlock.Text = "";
                    else StatusTextBlock.Text = msg;
                    ClientConfig.Clear();
                    return;
                }


                var body = await response.Content.ReadAsStringAsync();
                var user = JsonSerializer.Deserialize<LoginResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (user == null)
                {
                    StatusTextBlock.Text = "Ошибка чтения ответа сервера.";
                    return;
                }

                // Сохраняем текущего пользователя
                Session.Email = user.Email;
                Session.Name = user.Name;

                // Всегда сохраняем для автоматического логина
                ClientConfig.Save(new ClientConfigModel
                {
                    Email = email,
                    Password = password
                });

                // Открываем окно чата
                var chatWindow = new MainWindow(user.Name);
                chatWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Ошибка: " + ex.Message;
            }
        }

        // Регистрация нового пользователя
        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Text = "Регистрация...";
            var email = EmailTextBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                StatusTextBlock.Text = "Введите email и пароль для регистрации.";
                return;
            }

            try
            {
                // простое имя (всё до @)
                var namePart = email.Split('@')[0];
                if (string.IsNullOrWhiteSpace(namePart))
                    namePart = email;

                var req = new RegisterRequest
                {
                    Email = email,
                    Password = password,
                    Name = namePart
                };

                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseUrl}/api/Auth/register", content);
                if (!response.IsSuccessStatusCode)
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    StatusTextBlock.Text = "Ошибка регистрации: " + msg;
                    return;
                }

                StatusTextBlock.Text = "Регистрация успешна, теперь можно войти.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Ошибка: " + ex.Message;
            }
        }

        // Кнопка восстановления
        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                StatusTextBlock.Text = "Введите email для восстановления.";
                return;
            }

            try
            {
                await _httpClient.PostAsync($"{BaseUrl}/api/Auth/restore", new StringContent($"{{\"email\":\"{email}\"}}", Encoding.UTF8, "application/json"));
                StatusTextBlock.Foreground = Brushes.Green;
                StatusTextBlock.Text = "Пароль отправлен на почту.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Foreground = Brushes.Red;
                StatusTextBlock.Text = "Ошибка: " + ex.Message;
            }
        }

    }
}

