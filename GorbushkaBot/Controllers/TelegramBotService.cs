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
        private static readonly Dictionary<long, string> UserSteps = new Dictionary<long, string>(); // Хранение текущего шага пользователя

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

            // Обработка команды /start
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
            // Обработка нажатия кнопки "verify"
            else if (message.Text == "verify")
            {
                await StartVerification(botClient, message.Chat.Id);
            }
            // Обработка нажатия кнопки "back"
            else if (message.Text == "back")
            {
                if (UserSteps.TryGetValue(message.Chat.Id, out var step))
                {
                    await HandleStepBack(botClient, message.Chat.Id, step);
                }
            }
            // Обработка ввода текста пользователем (например, ФИО)
            else if (UserSteps.ContainsKey(message.Chat.Id))
            {
                switch (UserSteps[message.Chat.Id])
                {
                    case "fio":
                        UserSteps[message.Chat.Id] = "passport_photo";
                        await AskForPassportPhoto(botClient, message.Chat.Id);
                        break;
                    case "passport_photo":
                        UserSteps[message.Chat.Id] = "role";
                        await AskForRoleSelection(botClient, message.Chat.Id);
                        break;
                    case "role":
                        // Финализация верификации
                        await FinalizeVerification(botClient, message.Chat.Id);
                        break;
                }
            }
        }

        // Начало верификации
        private async Task StartVerification(ITelegramBotClient botClient, long chatId)
        {
            UserSteps[chatId] = "fio";
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Назад", "back")
                }
            });

            await botClient.SendTextMessageAsync(chatId, "Шаг 1: Введите ваше ФИО.", replyMarkup: inlineKeyboard);
        }

        // Запрос на отправку фото паспорта
        private async Task AskForPassportPhoto(ITelegramBotClient botClient, long chatId)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Назад", "back")
                }
            });

            await botClient.SendTextMessageAsync(chatId, "Шаг 2: Отправьте фото вашего паспорта.", replyMarkup: inlineKeyboard);
        }

        // Запрос на выбор роли
        private async Task AskForRoleSelection(ITelegramBotClient botClient, long chatId)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Продавец", "seller"),
                    InlineKeyboardButton.WithCallbackData("Покупатель", "buyer"),
                    InlineKeyboardButton.WithCallbackData("Продавец и покупатель", "both")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Назад", "back")
                }
            });

            await botClient.SendTextMessageAsync(chatId, "Шаг 3: Выберите вашу роль (Продавец, Покупатель или Продавец и Покупатель).", replyMarkup: inlineKeyboard);
        }

        // Завершение верификации
        private async Task FinalizeVerification(ITelegramBotClient botClient, long chatId)
        {
            await botClient.SendTextMessageAsync(chatId, "Верификация завершена успешно!");
            UserSteps.Remove(chatId); // Удаляем шаги пользователя, завершив верификацию
        }

        // Обработчик ошибок
        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, System.Threading.CancellationToken cancellationToken)
        {
            Console.WriteLine($"Произошла ошибка: {exception.Message}");
            return Task.CompletedTask;
        }

        // Обработка кнопки "Назад" для возврата на предыдущий шаг
        private async Task HandleStepBack(ITelegramBotClient botClient, long chatId, string step)
        {
            if (step == "fio")
            {
                UserSteps[chatId] = "verify"; // Возвращаемся к шагу верификации
                await StartVerification(botClient, chatId);
            }
            else if (step == "passport_photo")
            {
                UserSteps[chatId] = "fio"; // Возвращаемся к ФИО
                await AskForPassportPhoto(botClient, chatId);
            }
            else if (step == "role")
            {
                UserSteps[chatId] = "passport_photo"; // Возвращаемся к фото паспорта
                await AskForPassportPhoto(botClient, chatId);
            }
        }
    }
}
