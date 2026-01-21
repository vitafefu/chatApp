using System.Net;
using System.Net.Mail;

namespace ChatServer.Services;

public class EmailService
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _password;

    public EmailService(IConfiguration config)
    {
        _host = config["Smtp:Host"] ?? "";
        _port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
        _user = config["Smtp:User"] ?? "";
        _password = config["Smtp:Password"] ?? "";

        if (string.IsNullOrWhiteSpace(_host) || string.IsNullOrWhiteSpace(_user) || string.IsNullOrWhiteSpace(_password)) throw new Exception("SMTP не настроен в appsettings.json (Smtp:Host/User/Password).");
    }

    public void SendConfirmEmail(string to, string token, string serverBaseUrl)
    {
        to = to.Trim();
        if (!MailAddress.TryCreate(to, out _)) throw new Exception("Невалидный email: " + to);
        var link = $"{serverBaseUrl}/api/Auth/confirm-email?token={token}";

        Send(to, "Подтверждение регистрации", $"Перейдите по ссылке:\n{link}");
    }

    public void SendPasswordEmail(string to, string password)
    {
        to = to.Trim();
        if (!MailAddress.TryCreate(to, out _)) throw new Exception("Невалидный email: " + to);

        Send(to, "Восстановление пароля", $"Ваш пароль: {password}");
    }

    private void Send(string to, string subject, string body)
    {
        using var client = new SmtpClient(_host, _port)
        {
            Credentials = new NetworkCredential(_user, _password),
            EnableSsl = true
        };

        using var mail = new MailMessage
        {
            From = new MailAddress(_user, "Chat App"),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        mail.To.Add(to);
        client.Send(mail);
    }
}
