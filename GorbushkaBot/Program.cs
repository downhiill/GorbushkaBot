using GorbushkaBot.AppDbContext;
using GorbushkaBot.Controllers;
using GorbushkaBot.Service;
using Microsoft.EntityFrameworkCore;

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
    var credentialPath = "GOOGLE_CREDENTIALS_PATH"; // Укажите правильный путь
    var spreadsheetId = "GOOGLE_SHEET_ID"; // Укажите правильный ID таблицы
    var userApplicationService = provider.GetRequiredService<UserApplicationService>();
    var userAcceptService = provider.GetRequiredService<UserAcceptService>();

    return new GoogleSheetsService(credentialPath, spreadsheetId, userApplicationService, userAcceptService);
});
builder.Services.AddScoped<TelegramBotService>(serviceProvider =>
{
    var userApplicationService = serviceProvider.GetRequiredService<UserApplicationService>();
    var applicationService = serviceProvider.GetRequiredService<ApplicationService>();
    var googleSheetsService = serviceProvider.GetRequiredService<GoogleSheetsService>();
    var googleDriveService = serviceProvider.GetRequiredService<GoogleDriveService>();
    var stepManager = serviceProvider.GetRequiredService<StepManager>();
    var stepManagerAdmin = serviceProvider.GetRequiredService<StepManagerAdmin>();
    var keyboardManager = serviceProvider.GetRequiredService<KeyboardManager>();
    var errorHandler = serviceProvider.GetRequiredService<ErrorHandler>();

    return new TelegramBotService(
        userApplicationService,
        applicationService,
        googleSheetsService,
        googleDriveService,
        stepManager,
        stepManagerAdmin,
        keyboardManager,
        errorHandler
    );
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
