namespace ChatServer.Models;

public enum UserStatus
{
    Offline = 0,
    Online = 1,
    DoNotDisturb = 2
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string Name { get; set; } = null!;

    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Offline;

    // Настройки уведомлений
    public bool NotificationsEnabled { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public bool BannerEnabled { get; set; } = true;

    public bool EmailConfirmed { get; set; }
    public string? EmailConfirmToken { get; set; }
}
