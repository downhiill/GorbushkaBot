using GorbushkaBot.AppDbContext;
using GorbushkaBot.Service;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Telegram bot service
builder.Services.AddScoped<UserApplicationService>();
builder.Services.AddScoped<GoogleSheetsService>(provider =>
{
    var credentialPath = "GOOGLE_CREDENTIALS_PATH"; // Укажите правильный путь
    var spreadsheetId = "GOOGLE_SHEET_ID"; // Укажите правильный ID таблицы
    var userApplicationService = provider.GetRequiredService<UserApplicationService>();

    return new GoogleSheetsService(credentialPath, spreadsheetId, userApplicationService);
});
builder.Services.AddSingleton<TelegramBotService>(serviceProvider =>
{
    var userApplicationService = serviceProvider.GetRequiredService<UserApplicationService>();
    return new TelegramBotService(userApplicationService); // Передаем userApplicationService в конструктор TelegramBotService
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Start the bot in a separate task
var botService = app.Services.GetRequiredService<TelegramBotService>();
var botTask = Task.Run(() => botService.Start());  // Запуск бота в фоновом потоке

app.Run();
