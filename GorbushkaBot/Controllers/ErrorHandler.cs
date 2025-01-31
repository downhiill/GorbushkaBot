using System;
using System.Threading.Tasks;
using Telegram.Bot;

namespace GorbushkaBot.Controllers
{
    public class ErrorHandler
    {
        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, System.Threading.CancellationToken cancellationToken)
        {
            Console.WriteLine($"Произошла ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
