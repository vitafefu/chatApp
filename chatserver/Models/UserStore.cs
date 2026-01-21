using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChatServer.Models
{
    public static class UserStore
    {
        private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "users.json");

        private static readonly object _lock = new();

        private static List<User> _users = new();

        // Статический конструктор — вызывается один раз при первом обращении к UserStore
        static UserStore()
        {
            LoadFromFile();
        }

        public static IEnumerable<User> GetAll()
        {
            lock (_lock)
            {
                return _users.ToList();
            }
        }

        public static User? GetByEmail(string email)
        {
            lock (_lock)
            {
                return _users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static bool AddUser(User user)
        {
            lock (_lock)
            {
                if (_users.Any(u => u.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                _users.Add(user);
                SaveToFile();
                return true;
            }
        }

        public static bool UpdateUser(User user)
        {
            lock (_lock)
            {
                var existing = _users.FirstOrDefault(u => u.Id == user.Id);

                if (existing == null)
                    return false;

                existing.Email = user.Email;
                existing.Password = user.Password;
                existing.Name = user.Name;
                existing.AvatarUrl = user.AvatarUrl;
                existing.Bio = user.Bio;
                existing.Status = user.Status;
                existing.NotificationsEnabled = user.NotificationsEnabled;
                existing.SoundEnabled = user.SoundEnabled;
                existing.BannerEnabled = user.BannerEnabled;
                existing.EmailConfirmed = user.EmailConfirmed;
                existing.EmailConfirmToken = user.EmailConfirmToken;


                SaveToFile();
                return true;
            }
        }

        private static void LoadFromFile()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    _users = new List<User>();
                    return;
                }

                var json = File.ReadAllText(FilePath);
                var users = JsonSerializer.Deserialize<List<User>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                _users = users ?? new List<User>();
            }
            catch
            {
                _users = new List<User>();
            }
        }

        private static void SaveToFile()
        {
            try
            {
                var json = JsonSerializer.Serialize(_users, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        public static void DeleteByEmail(string email)
        {
            var user = GetByEmail(email);
            if (user == null) return;

            _users.Remove(user);
            SaveToFile();
        }

    }
}
