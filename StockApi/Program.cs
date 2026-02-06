var builder = WebApplication.CreateBuilder(args);

// 註冊 CORS 服務，允許跨來源存取
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddHttpClient();

var app = builder.Build();

// 必須放在這裡：在 Routing 之後，Authorization 之前
app.UseRouting();
app.UseCors(); 
app.UseAuthorization();

app.MapControllers();

app.Run();