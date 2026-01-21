using System;
using System.IO;
using System.Text.Json;

namespace ChatClient
{
    public class ClientConfigModel
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public static class ClientConfig
    {
        private static readonly string FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChatClient");
        private static readonly string FilePath = Path.Combine(FolderPath, "config.json");

        public static ClientConfigModel Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new ClientConfigModel();

                var json = File.ReadAllText(FilePath);
                var cfg = JsonSerializer.Deserialize<ClientConfigModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return cfg ?? new ClientConfigModel();
            }
            catch
            {
                return new ClientConfigModel();
            }
        }

        public static void Save(ClientConfigModel config)
        {
            try
            {
                if (!Directory.Exists(FolderPath))
                    Directory.CreateDirectory(FolderPath);

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch { }
        }
    }
}
