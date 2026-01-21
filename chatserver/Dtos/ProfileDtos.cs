using ChatServer.Models;

namespace ChatServer.Dtos;

public class UpdateProfileRequest
{
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public UserStatus Status { get; set; }
    public bool NotificationsEnabled { get; set; }
    public bool SoundEnabled { get; set; }
    public bool BannerEnabled { get; set; }
}

public class ChangeEmailRequest
{
    public string OldEmail { get; set; } = null!;
    public string NewEmail { get; set; } = null!;
}

public class ChangePasswordRequest
{
    public string Email { get; set; } = null!;
    public string OldPassword { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}
