using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GorbushkaBot.Controllers
{
    public class TelegramBotService
    {
        private static readonly string BotToken = Environment.GetEnvironmentVariable("BOT_TOKEN") ?? throw new InvalidOperationException("BOT_TOKEN is not set");
        private static readonly string ConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL") ?? throw new InvalidOperationException("DATABASE_URL is not set");
        private readonly TelegramBotClient botClient;
        private static readonly ConcurrentDictionary<long, string> UserStates = new();
        private static readonly ConcurrentDictionary<long, string> UserPreviousStates = new(); // Хранение предыдущих состояний

        public TelegramBotService()
        {
            botClient = new TelegramBotClient(BotToken);
        }

        public void Start()
        {
            botClient.StartReceiving(UpdateHandler, ErrorHandler);
            Console.WriteLine("Бот запущен...");
        }

        private async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message)
            {
                var message = update.Message;
                var chatId = message.Chat.Id;

                if (message.Type == MessageType.Text)
                {
                    if (message.Text == "/start")
                    {
                        UserStates[chatId] = "waiting_for_verification";
                        var keyboard = new InlineKeyboardMarkup(new[]
                        {
                            new InlineKeyboardButton("Верификация") { CallbackData = "start_verification" }
                        });

                        await botClient.SendTextMessageAsync(chatId, "Добро пожаловать в бот! Нажмите 'Верификация', чтобы начать процесс.", replyMarkup: keyboard);
                    }
                    else if (message.Text == "Назад" && UserPreviousStates.TryGetValue(chatId, out var prevState))
                    {
                        // Возвращаемся к предыдущему состоянию
                        UserStates[chatId] = prevState;
                        UserPreviousStates[chatId] = prevState;
                        await SendStepMessage(chatId);
                    }
                    else if (UserStates.TryGetValue(chatId, out var currentState))
                    {
                        switch (currentState)
                        {
                            case "waiting_for_verification":
                                if (message.Text == "Верификация")
                                {
                                    UserStates[chatId] = "waiting_for_photo";
                                    await SendStepMessage(chatId);
                                }
                                break;
                            case "waiting_for_photo":
                                if (message.Text == "Назад")
                                {
                                    UserStates[chatId] = "waiting_for_verification"; // Возвращаемся на шаг верификации
                                    await SendStepMessage(chatId);
                                }
                                break;
                            case "waiting_for_market_status":
                                if (message.Text == "Я с рынка" || message.Text == "Я не с рынка")
                                {
                                    UserStates[chatId] = message.Text == "Я с рынка" ? "waiting_for_pavilion_number" : "waiting_for_company_name";
                                    await SendStepMessage(chatId);
                                }
                                break;
                            case "waiting_for_pavilion_number":
                                if (IsValidPavilionNumber(message.Text))
                                {
                                    UserStates[chatId] = "waiting_for_contract_number";
                                    await SendStepMessage(chatId);
                                }
                                break;
                            case "waiting_for_contract_number":
                                if (IsValidContractNumber(message.Text))
                                {
                                    UserStates[chatId] = "completed";
                                    await botClient.SendTextMessageAsync(chatId, "Спасибо! Ваша заявка отправлена на проверку.");
                                }
                                break;
                        }
                    }
                }
                else if (message.Type == MessageType.Photo && UserStates.TryGetValue(chatId, out var stateAfterPhoto) && stateAfterPhoto == "waiting_for_photo")
                {
                    var fileId = message.Photo[^1].FileId;
                    var fileSize = message.Photo[^1].FileSize;

                    if (fileSize < 10000)
                    {
                        await botClient.SendTextMessageAsync(chatId, "Ошибка! Фото слишком маленькое, отправьте более четкое изображение паспорта.");
                        return;
                    }

                    UserStates[chatId] = "waiting_for_market_status";
                    await SendStepMessage(chatId);
                }
            }
        }

        private async Task SendStepMessage(long chatId)
        {
            var currentState = UserStates[chatId];

            InlineKeyboardMarkup keyboard;
            string messageText;

            switch (currentState)
            {
                case "waiting_for_verification":
                    messageText = "Отправьте фото паспорта для начала верификации.";
                    keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new InlineKeyboardButton("Назад") { CallbackData = "go_back" }
                    });
                    break;
                case "waiting_for_photo":
                    messageText = "Отправьте фото паспорта для начала верификации.";
                    keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new InlineKeyboardButton("Назад") { CallbackData = "go_back" }
                    });
                    break;
                case "waiting_for_market_status":
                    messageText = "Вы с рынка или нет?";
                    keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new InlineKeyboardButton("Я с рынка") { CallbackData = "market_status_yes" },
                        new InlineKeyboardButton("Я не с рынка") { CallbackData = "market_status_no" },
                        new InlineKeyboardButton("Назад") { CallbackData = "go_back" }
                    });
                    break;
                case "waiting_for_pavilion_number":
                    messageText = "Введите номер павильона.";
                    keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new InlineKeyboardButton("Назад") { CallbackData = "go_back" }
                    });
                    break;
                case "waiting_for_contract_number":
                    messageText = "Введите номер договора аренды.";
                    keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new InlineKeyboardButton("Назад") { CallbackData = "go_back" }
                    });
                    break;
                case "completed":
                    messageText = "Спасибо! Ваша заявка отправлена на проверку.";
                    keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new InlineKeyboardButton("Назад") { CallbackData = "go_back" }
                    });
                    break;
                default:
                    messageText = "Произошла ошибка. Попробуйте снова.";
                    keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new InlineKeyboardButton("Назад") { CallbackData = "go_back" }
                    });
                    break;
            }

            await botClient.SendTextMessageAsync(chatId, messageText, replyMarkup: keyboard);
        }

        private bool IsValidPavilionNumber(string pavilionNumber)
        {
            return Regex.IsMatch(pavilionNumber, "^[A-Za-z0-9]+$");
        }

        private bool IsValidContractNumber(string contractNumber)
        {
            return Regex.IsMatch(contractNumber, "^[A-Za-z0-9]+$");
        }

        private bool IsValidCompanyName(string companyName)
        {
            return !string.IsNullOrWhiteSpace(companyName);
        }

        private bool IsValidBusinessType(string businessType)
        {
            var validBusinessTypes = new[] { "Торговля", "Услуги", "Производство" };
            return validBusinessTypes.Contains(businessType);
        }

        private static Task ErrorHandler(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
