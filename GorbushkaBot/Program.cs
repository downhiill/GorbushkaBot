using GorbushkaBot.AppDbContext;
using GorbushkaBot.Controllers;
using GorbushkaBot.Service;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Telegram bot service
builder.Services.AddScoped<UserApplicationService>();
builder.Services.AddScoped<UserAcceptService>();
builder.Services.AddScoped<ApplicationService>();
builder.Services.AddScoped<GoogleSheetsService>(provider =>
{
    var credentialPath = Environment.GetEnvironmentVariable("GOOGLE_CREDENTIALS_PATH"); // Укажите правильный путь
    var spreadsheetId = Environment.GetEnvironmentVariable("GOOGLE_SHEET_ID"); // Укажите правильный ID таблицы
    var spreedsheetcategoriesId = "1UovBQNNaA5sEKu9AhyZTqjJq9ORd_AlwKgK5x4525pI";
    var userApplicationService = provider.GetRequiredService<UserApplicationService>();
    var userAcceptService = provider.GetRequiredService<UserAcceptService>();

    return new GoogleSheetsService(credentialPath, spreadsheetId,spreedsheetcategoriesId, userApplicationService, userAcceptService);
});
builder.Services.AddScoped<TelegramBotService>(serviceProvider =>
{
    var userApplicationService = serviceProvider.GetRequiredService<UserApplicationService>();
    var applicationService = serviceProvider.GetRequiredService<ApplicationService>();
    var userAcceptService = serviceProvider.GetRequiredService<UserAcceptService>();
    var applicationDbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

    return new TelegramBotService(
        userApplicationService,
        applicationService,
        userAcceptService,
        applicationDbContext
    );
});

builder.Services.AddSingleton<TelegramBotClient>(provider =>
{
    var botToken = Environment.GetEnvironmentVariable("BOT_TOKEN"); // Замените на ваш реальный токен бота
    return new TelegramBotClient(botToken);
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
