var builder = WebApplication.CreateBuilder(args);

// 解決 CORS：讓前端網頁可以連到這個 API
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddHttpClient(); 

var app = builder.Build();

app.UseCors(); 
app.MapControllers();

// 設定 Render 雲端環境需要的 Port
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Run($"http://0.0.0.0:{port}");