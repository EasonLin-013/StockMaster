var builder = WebApplication.CreateBuilder(args);

// 註冊 CORS 服務
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddHttpClient(); // 確保有註冊 HttpClient

var app = builder.Build();

// 啟用 CORS (必須放在 MapControllers 之前)
app.UseCors("AllowAll");

app.MapControllers();
app.Run();
