namespace ChatClient
{
    public class UserDto
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";

        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; }
        public int Status { get; set; }
        public bool NotificationsEnabled { get; set; }
        public bool SoundEnabled { get; set; }
        public bool BannerEnabled { get; set; }
    }
}
