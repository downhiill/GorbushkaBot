using GorbushkaBot.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Telegram bot service
builder.Services.AddSingleton<TelegramBotService>();

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
