using ChatServer.Models;

namespace ChatServer.Dtos;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public UserStatus Status { get; set; }
    public bool NotificationsEnabled { get; set; }
    public bool SoundEnabled { get; set; }
    public bool BannerEnabled { get; set; }

    public static UserDto FromUser(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Name = user.Name,
        AvatarUrl = user.AvatarUrl,
        Bio = user.Bio,
        Status = user.Status,
        NotificationsEnabled = user.NotificationsEnabled,
        SoundEnabled = user.SoundEnabled,
        BannerEnabled = user.BannerEnabled
    };
}
