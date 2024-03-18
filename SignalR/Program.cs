using SignalRApp;



WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
//builder.WebHost.UseUrls("http://0.0.0.0:8080");

WebApplication app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<ChatHub>("/chat");

app.Run();
