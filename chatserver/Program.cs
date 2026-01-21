using ChatServer;
using ChatServer.Services;

var builder = WebApplication.CreateBuilder(args);
var port = builder.Configuration.GetValue<int?>("Server:Port") ?? 5000;

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
});

builder.Services.AddSingleton<EmailService>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.UseStaticFiles();
app.MapControllers();

app.MapHub<ChatHub>("/chat");

app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = app.Urls.Any() ? string.Join(", ", app.Urls) : $"http://0.0.0.0:{port}";
    app.Logger.LogInformation("Chat server listening on {Addresses}", addresses);
});

app.Run();
