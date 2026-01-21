using ChatServer;
using ChatServer.Dtos;
using ChatServer.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ChatServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IHubContext<ChatHub> _hub;

    public ProfileController(IWebHostEnvironment env, IHubContext<ChatHub> hub)
    {
        _env = env;
        _hub = hub;
    }


    // Получить профиль по email
    [HttpGet("{email}")]
    public ActionResult<UserDto> GetProfile(string email)
    {
        var user = UserStore.GetByEmail(email);
        if (user == null)
            return NotFound("Пользователь не найден.");

        return Ok(UserDto.FromUser(user));
    }

    // Обновить профиль
    [HttpPut("update")]
    public async Task<ActionResult<UserDto>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var user = UserStore.GetByEmail(request.Email);
        if (user == null)
            return NotFound("Пользователь не найден.");

        user.Name = request.Name;
        user.AvatarUrl = request.AvatarUrl;
        user.Bio = request.Bio;
        user.Status = request.Status;
        user.NotificationsEnabled = request.NotificationsEnabled;
        user.SoundEnabled = request.SoundEnabled;
        user.BannerEnabled = request.BannerEnabled;

        UserStore.UpdateUser(user);

        // чтобы обновили аватар/имя без перезапуска)
        await _hub.Clients.All.SendAsync("UserProfileChanged", UserDto.FromUser(user));
        return Ok(UserDto.FromUser(user));
    }

    // Смена email
    [HttpPost("change-email")]
    public ActionResult<UserDto> ChangeEmail([FromBody] ChangeEmailRequest request)
    {
        var user = UserStore.GetByEmail(request.OldEmail);
        if (user == null)
            return NotFound("Пользователь не найден.");

        if (!request.NewEmail.Contains("@"))
            return BadRequest("Некорректный email.");

        var other = UserStore.GetByEmail(request.NewEmail);
        if (other != null && other.Id != user.Id)
            return BadRequest("Пользователь с таким email уже существует.");

        user.Email = request.NewEmail;
        UserStore.UpdateUser(user);

        return Ok(UserDto.FromUser(user));
    }

    // Смена пароля
    [HttpPost("change-password")]
    public ActionResult ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = UserStore.GetByEmail(request.Email);
        if (user == null)
            return NotFound("Пользователь не найден.");

        if (user.Password != request.OldPassword)
            return BadRequest("Старый пароль неверен.");

        user.Password = request.NewPassword;
        UserStore.UpdateUser(user);

        return Ok("Пароль успешно изменён.");
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file, [FromForm] string email)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Файл не передан");

        var user = UserStore.GetByEmail(email);
        if (user == null)
            return NotFound("Пользователь не найден");

        // wwwroot/avatars
        var avatarsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"), "avatars");
        Directory.CreateDirectory(avatarsDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext)) ext = ".png";

        var fileName = $"{user.Id}{ext}";
        var fullPath = Path.Combine(avatarsDir, fileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        user.AvatarUrl = $"/avatars/{fileName}";
        UserStore.UpdateUser(user);

        var dto = UserDto.FromUser(user);
        await _hub.Clients.All.SendAsync("UserProfileChanged", dto);
        return Ok(dto);


    }
}
