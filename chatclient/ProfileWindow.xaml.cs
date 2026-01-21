using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

using System.Net.Http.Headers;


namespace ChatClient
{
    public partial class ProfileWindow : Window
    {
        private static readonly string BaseUrl = ServerConfig.ServerBaseUrl;
        private static readonly string HubUrl = ServerConfig.HubUrl;
        private readonly HttpClient _httpClient = new HttpClient();
        private HubConnection? _hub;

        public ProfileWindow()
        {
            InitializeComponent();
            LoadProfile();
            _ = EnsureHubConnectedAsync();
        }

        private class UserDto
        {
            public Guid Id { get; set; }
            public string Email { get; set; } = null!;
            public string Name { get; set; } = null!;
            public string? AvatarUrl { get; set; }
            public string? Bio { get; set; }
            public int Status { get; set; }
            public bool NotificationsEnabled { get; set; }
            public bool SoundEnabled { get; set; }
            public bool BannerEnabled { get; set; }
        }

        private class UpdateProfileRequest
        {
            public string Email { get; set; } = null!;
            public string Name { get; set; } = null!;
            public string? AvatarUrl { get; set; }
            public string? Bio { get; set; }
            public int Status { get; set; }
            public bool NotificationsEnabled { get; set; }
            public bool SoundEnabled { get; set; }
            public bool BannerEnabled { get; set; }
        }

        private class ChangeEmailRequest
        {
            public string OldEmail { get; set; } = null!;
            public string NewEmail { get; set; } = null!;
        }

        private class ChangePasswordRequest
        {
            public string Email { get; set; } = null!;
            public string OldPassword { get; set; } = null!;
            public string NewPassword { get; set; } = null!;
        }

        private async Task EnsureHubConnectedAsync()
        {
            try
            {
                // если уже подключены — ок
                if (_hub != null && _hub.State == HubConnectionState.Connected)
                    return;

                // если было старое подключение — аккуратно закрываем
                if (_hub != null)
                {
                    try { await _hub.StopAsync(); } catch { }
                    try { await _hub.DisposeAsync(); } catch { }
                    _hub = null;
                }

                var email = Session.Email ?? string.Empty; ;
                var hubUrl = $"{HubUrl}?user={Uri.EscapeDataString(email)}";

                _hub = new HubConnectionBuilder().WithUrl(hubUrl).WithAutomaticReconnect().Build();
                await _hub.StartAsync();
            }
            catch
            {
                Console.WriteLine($"Не удалось подключиться к SignalR ({HubUrl}).");
            }
        }

        private async Task TryPushStatusToHubAsync(int status)
        {
            try
            {
                await EnsureHubConnectedAsync();

                if (_hub != null && _hub.State == HubConnectionState.Connected)
                {
                    await _hub.InvokeAsync("SetStatus", status);
                }
            }
            catch
            {

            }
        }

        private async void LoadProfile()
        {
            try
            {
                StatusTextBlock.Text = "";
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/Profile/{Session.Email}");
                if (!response.IsSuccessStatusCode)
                {
                    StatusTextBlock.Text = "Не удалось загрузить профиль.";
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var user = JsonSerializer.Deserialize<UserDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (user == null)
                {
                    StatusTextBlock.Text = "Ошибка чтения профиля.";
                    return;
                }

                EmailTextBlock.Text = user.Email;
                NameTextBox.Text = user.Name;
                AvatarTextBox.Text = user.AvatarUrl;
                BioTextBox.Text = user.Bio;

                // Статус
                StatusComboBox.SelectedIndex = user.Status;

                NotificationsEnabledCheckBox.IsChecked = user.NotificationsEnabled;
                SoundEnabledCheckBox.IsChecked = user.SoundEnabled;
                BannerEnabledCheckBox.IsChecked = user.BannerEnabled;

                NotificationService.NotificationsEnabled = user.NotificationsEnabled;
                NotificationService.SoundEnabled = user.SoundEnabled;
                NotificationService.BannerEnabled = user.BannerEnabled;

                // Аватар
                if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
                {
                    try
                    {
                        var url = user.AvatarUrl.StartsWith("/") ? BaseUrl + user.AvatarUrl : user.AvatarUrl;

                        // чтобы не мешал кэш
                        url += (url.Contains("?") ? "&" : "?") + "v=" + DateTime.UtcNow.Ticks;

                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(url, UriKind.Absolute);
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.EndInit();

                        AvatarImage.Source = bmp;
                    }
                    catch
                    {
                        AvatarImage.Source = null;
                    }
                }
                else
                {
                    AvatarImage.Source = null;
                }

            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Ошибка: " + ex.Message;
            }
        }

        private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var req = new UpdateProfileRequest
                {
                    Email = Session.Email,
                    Name = NameTextBox.Text.Trim(),
                    AvatarUrl = string.IsNullOrWhiteSpace(AvatarTextBox.Text) ? null : AvatarTextBox.Text.Trim(),
                    Bio = string.IsNullOrWhiteSpace(BioTextBox.Text) ? null : BioTextBox.Text.Trim(),
                    Status = StatusComboBox.SelectedIndex < 0 ? 0 : StatusComboBox.SelectedIndex,
                    NotificationsEnabled = NotificationsEnabledCheckBox.IsChecked == true,
                    SoundEnabled = SoundEnabledCheckBox.IsChecked == true,
                    BannerEnabled = BannerEnabledCheckBox.IsChecked == true
                };

                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"{BaseUrl}/api/Profile/update", content);

                if (!response.IsSuccessStatusCode)
                {
                    StatusTextBlock.Foreground = Brushes.Red;
                    StatusTextBlock.Text = "Не удалось сохранить профиль.";
                    return;
                }

                // локально обновим
                Session.Name = req.Name;
                Session.Status = req.Status switch
                {
                    1 => "Online",
                    2 => "DoNotDisturb",
                    _ => "Offline"
                };

                NotificationService.NotificationsEnabled = req.NotificationsEnabled;
                NotificationService.SoundEnabled = req.SoundEnabled;
                NotificationService.BannerEnabled = req.BannerEnabled;

                await TryPushStatusToHubAsync(req.Status);

                StatusTextBlock.Foreground = Brushes.Green;
                StatusTextBlock.Text = "Профиль сохранён.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Foreground = Brushes.Red;
                StatusTextBlock.Text = "Ошибка: " + ex.Message;
            }
        }

        private async void ChangeEmailButton_Click(object sender, RoutedEventArgs e)
        {
            var newEmail = NewEmailTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(newEmail))
            {
                StatusTextBlock.Text = "Введите новый email.";
                return;
            }

            try
            {
                var req = new ChangeEmailRequest
                {
                    OldEmail = Session.Email,
                    NewEmail = newEmail
                };

                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{BaseUrl}/api/Profile/change-email", content);

                if (!response.IsSuccessStatusCode)
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    StatusTextBlock.Foreground = Brushes.Red;
                    StatusTextBlock.Text = "Не удалось изменить email: " + msg;
                    return;
                }

                // обновляем сессию
                Session.Email = newEmail;
                EmailTextBlock.Text = newEmail;

                // переподключаем Hub под новым email
                await EnsureHubConnectedAsync();

                StatusTextBlock.Foreground = Brushes.Green;
                StatusTextBlock.Text = "Email изменён.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Foreground = Brushes.Red;
                StatusTextBlock.Text = "Ошибка: " + ex.Message;
            }
        }

        private async void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            var oldPass = OldPasswordBox.Password;
            var newPass = NewPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(oldPass) || string.IsNullOrWhiteSpace(newPass))
            {
                StatusTextBlock.Text = "Введите старый и новый пароль.";
                return;
            }

            try
            {
                var req = new ChangePasswordRequest
                {
                    Email = Session.Email,
                    OldPassword = oldPass,
                    NewPassword = newPass
                };

                var json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{BaseUrl}/api/Profile/change-password", content);

                if (!response.IsSuccessStatusCode)
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    StatusTextBlock.Foreground = Brushes.Red;
                    StatusTextBlock.Text = "Не удалось изменить пароль: " + msg;
                    return;
                }

                StatusTextBlock.Foreground = Brushes.Green;
                StatusTextBlock.Text = "Пароль изменён.";
                OldPasswordBox.Password = "";
                NewPasswordBox.Password = "";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Foreground = Brushes.Red;
                StatusTextBlock.Text = "Ошибка: " + ex.Message;
            }
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_hub != null)
                {
                    try { await _hub.StopAsync(); } catch { }
                    try { await _hub.DisposeAsync(); } catch { }
                    _hub = null;
                }
            }
            catch { }

            Close();
        }

        private async void ChooseAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Выберите аватар",
                Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                using var form = new MultipartFormDataContent();

                await using var stream = File.OpenRead(dlg.FileName);
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                form.Add(fileContent, "file", Path.GetFileName(dlg.FileName));
                form.Add(new StringContent(Session.Email), "email");

                var resp = await _httpClient.PostAsync($"{BaseUrl}/api/Profile/avatar", form);
                if (!resp.IsSuccessStatusCode)
                {
                    StatusTextBlock.Text = "Не удалось загрузить аватар.";
                    return;
                }

                var body = await resp.Content.ReadAsStringAsync();
                var user = JsonSerializer.Deserialize<UserDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (user == null || string.IsNullOrWhiteSpace(user.AvatarUrl))
                {
                    StatusTextBlock.Text = "Сервер не вернул AvatarUrl.";
                    return;
                }

                // показать URL в поле
                AvatarTextBox.Text = user.AvatarUrl;

                // показать картинку
                var url = user.AvatarUrl.StartsWith("/") ? BaseUrl + user.AvatarUrl : user.AvatarUrl;
                url += (url.Contains("?") ? "&" : "?") + "v=" + DateTime.UtcNow.Ticks;

                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(url, UriKind.Absolute);
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                bmp.EndInit();
                AvatarImage.Source = bmp;

                StatusTextBlock.Text = "Аватар обновлён.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Ошибка: " + ex.Message;
            }
        }
    }
}
