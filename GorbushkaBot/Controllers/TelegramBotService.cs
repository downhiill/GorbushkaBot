using System;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GorbushkaBot.Controllers
{
    public class TelegramBotService
    {
        private static readonly string BotToken = Environment.GetEnvironmentVariable("BOT_TOKEN") ?? throw new InvalidOperationException("BOT_TOKEN is not set");
        private readonly TelegramBotClient botClient;

        public TelegramBotService()
        {
            // Инициализация TelegramBotClient с токеном
            botClient = new TelegramBotClient(BotToken);
        }

        // Метод для старта бота
        public void Start()
        {
            // Запуск получения сообщений
            botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync);
            Console.WriteLine("Бот запущен...");
        }

        // Обработчик обновлений (сообщений)
        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, System.Threading.CancellationToken cancellationToken)
        {
            if (update.Message is not { } message) return;

            // Отправка приветственного сообщения с кнопкой
            if (message.Text == "/start")
            {
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Перейти к верификации", "verify")
                    }
                });

                await botClient.SendTextMessageAsync(message.Chat.Id, "Добро пожаловать в систему!", replyMarkup: inlineKeyboard);
            }
            // Обработка нажатия кнопки
            else if (message.Text == "verify")
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Переход к верификации...");
                // Здесь можно добавить логику для верификации
            }
        }

        // Обработчик ошибок
        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, System.Threading.CancellationToken cancellationToken)
        {
            Console.WriteLine($"Произошла ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
