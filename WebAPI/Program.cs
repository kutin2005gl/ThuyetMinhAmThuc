using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Services;
using WebAPI.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ITtsGeneratorService, TtsGeneratorService>();

// Dùng SQLite cho dev
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Tự động migrate và seed data khi khởi động
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS AppUsageEvents (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            GuestSessionId TEXT NOT NULL,
            EventType TEXT NOT NULL,
            EventValue TEXT NULL,
            CreatedAtUtc TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP)
        );
    ");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_AppUsageEvents_CreatedAtUtc_EventType ON AppUsageEvents (CreatedAtUtc, EventType);");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_AppUsageEvents_CreatedAtUtc_GuestSessionId ON AppUsageEvents (CreatedAtUtc, GuestSessionId);");
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.Run();
