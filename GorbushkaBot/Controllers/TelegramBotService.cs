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
        private readonly TelegramBotClient botClient;
        private static readonly ConcurrentDictionary<long, string> UserStates = new();
        private static readonly ConcurrentDictionary<long, string> UserPreviousStates = new(); // Хранение предыдущих состояний
        private static readonly ConcurrentDictionary<long, int> UserMessageIds = new(); // Сохранение messageId

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

                        var sentMessage = await botClient.SendTextMessageAsync(chatId, "Добро пожаловать в бот! Нажмите 'Верификация', чтобы начать процесс.", replyMarkup: keyboard);
                        UserMessageIds[chatId] = sentMessage.MessageId;  // Сохраняем messageId
                    }
                    else if (message.Text == "Назад" && UserPreviousStates.TryGetValue(chatId, out var prevState))
                    {
                        // Возвращаемся к предыдущему состоянию
                        UserStates[chatId] = prevState;
                        await SendStepMessage(chatId);  // Отправляем сообщение с нужными кнопками
                    }
                    else if (UserStates.TryGetValue(chatId, out var currentState))
                    {
                        switch (currentState)
                        {
                            case "waiting_for_verification":
                                if (message.Text == "Верификация")
                                {
                                    UserPreviousStates[chatId] = currentState;  // Сохраняем текущий шаг
                                    UserStates[chatId] = "waiting_for_photo";
                                    await SendStepMessage(chatId);
                                }
                                break;
                            case "waiting_for_photo":
                                if (message.Text == "Назад")
                                {
                                    UserPreviousStates[chatId] = currentState;
                                    UserStates[chatId] = "waiting_for_verification"; // Возвращаемся на шаг верификации
                                    await SendStepMessage(chatId);
                                }
                                break;
                            case "waiting_for_market_status":
                                if (message.Text == "Я с рынка" || message.Text == "Я не с рынка")
                                {
                                    UserPreviousStates[chatId] = currentState;
                                    UserStates[chatId] = message.Text == "Я с рынка" ? "waiting_for_pavilion_number" : "waiting_for_company_name";
                                    await SendStepMessage(chatId);
                                }
                                break;
                            case "waiting_for_pavilion_number":
                                if (IsValidPavilionNumber(message.Text))
                                {
                                    UserPreviousStates[chatId] = currentState;
                                    UserStates[chatId] = "waiting_for_contract_number";
                                    await SendStepMessage(chatId);
                                }
                                break;
                            case "waiting_for_contract_number":
                                if (IsValidContractNumber(message.Text))
                                {
                                    UserPreviousStates[chatId] = currentState;
                                    UserStates[chatId] = "completed";
                                    await botClient.SendTextMessageAsync(chatId, "Спасибо! Ваша заявка отправлена на проверку.");
                                }
                                break;
                        }
                    }
                }

                // Обработка фото
                if (message.Type == MessageType.Photo && UserStates[chatId] == "waiting_for_photo")
                {
                    UserPreviousStates[chatId] = "waiting_for_photo";
                    UserStates[chatId] = "waiting_for_market_status";
                    await SendStepMessage(chatId);
                }
            }

            // Обработка нажатия кнопки
            if (update.Type == UpdateType.CallbackQuery)
            {
                var callbackQuery = update.CallbackQuery;
                var chatId = callbackQuery.Message.Chat.Id;
                var callbackData = callbackQuery.Data;

                if (callbackData == "start_verification")
                {
                    UserStates[chatId] = "waiting_for_photo";
                    await SendStepMessage(chatId);
                }
                else if (callbackData == "go_back" && UserPreviousStates.TryGetValue(chatId, out var prevState))
                {
                    UserStates[chatId] = prevState;
                    await SendStepMessage(chatId);  // Возвращаемся на предыдущий шаг
                }
                else if (callbackData == "market_status_yes")
                {
                    UserStates[chatId] = "waiting_for_pavilion_number";
                    await SendStepMessage(chatId);
                }
                else if (callbackData == "market_status_no")
                {
                    UserStates[chatId] = "waiting_for_company_name";
                    await SendStepMessage(chatId);
                }
            }
        }

        private async Task SendStepMessage(long chatId)
        {
            var currentState = UserStates[chatId];
            var (messageText, keyboard) = GetMessageAndKeyboardForState(currentState);

            // Обновляем сообщение с сохраненным messageId
            if (UserMessageIds.ContainsKey(chatId))
            {
                await botClient.EditMessageTextAsync(chatId, messageId: UserMessageIds[chatId], text: messageText, replyMarkup: keyboard);
            }
            else
            {
                var sentMessage = await botClient.SendTextMessageAsync(chatId, messageText, replyMarkup: keyboard);
                UserMessageIds[chatId] = sentMessage.MessageId; // Сохраняем messageId
            }
        }

        private (string messageText, InlineKeyboardMarkup keyboard) GetMessageAndKeyboardForState(string currentState)
        {
            string messageText = string.Empty;
            InlineKeyboardMarkup keyboard;

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

            return (messageText, keyboard);
        }

        private bool IsValidPavilionNumber(string pavilionNumber)
        {
            return Regex.IsMatch(pavilionNumber, "^[A-Za-z0-9]+$");
        }

        private bool IsValidContractNumber(string contractNumber)
        {
            return Regex.IsMatch(contractNumber, "^[A-Za-z0-9]+$");
        }

        private static Task ErrorHandler(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
