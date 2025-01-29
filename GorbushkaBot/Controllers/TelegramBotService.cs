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
                        UserStates[chatId] = "waiting_for_photo";
                        await botClient.SendTextMessageAsync(chatId, "Отправьте фото паспорта для начала верификации.");
                    }
                    else if (UserStates.TryGetValue(chatId, out var state))
                    {
                        switch (state)
                        {
                            case "waiting_for_market_status":
                                if (message.Text == "Я с рынка" || message.Text == "Я не с рынка")
                                {
                                    UserStates[chatId] = message.Text == "Я с рынка" ? "waiting_for_pavilion_number" : "waiting_for_company_name";
                                    await botClient.SendTextMessageAsync(chatId, message.Text == "Я с рынка" ? "Введите номер павильона." : "Введите название вашей компании.");
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(chatId, "Пожалуйста, выберите один из вариантов: 'Я с рынка' или 'Я не с рынка'.");
                                }
                                break;
                            case "waiting_for_pavilion_number":
                                if (IsValidPavilionNumber(message.Text))
                                {
                                    UserStates[chatId] = "waiting_for_contract_number";
                                    await botClient.SendTextMessageAsync(chatId, "Введите номер договора аренды.");
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(chatId, "Ошибка! Введите корректный номер павильона (только цифры).");
                                }
                                break;
                            case "waiting_for_contract_number":
                                if (IsValidContractNumber(message.Text))
                                {
                                    UserStates[chatId] = "completed";
                                    await botClient.SendTextMessageAsync(chatId, "Спасибо! Ваша заявка отправлена на проверку.");
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(chatId, "Ошибка! Введите корректный номер договора аренды.");
                                }
                                break;
                            case "waiting_for_company_name":
                                if (IsValidCompanyName(message.Text))
                                {
                                    UserStates[chatId] = "waiting_for_business_type";
                                    await botClient.SendTextMessageAsync(chatId, "Введите вид деятельности вашей компании.");
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(chatId, "Ошибка! Название компании не может быть пустым.");
                                }
                                break;
                            case "waiting_for_business_type":
                                if (IsValidBusinessType(message.Text))
                                {
                                    UserStates[chatId] = "completed";
                                    await botClient.SendTextMessageAsync(chatId, "Спасибо! Ваша заявка отправлена на проверку.");
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(chatId, "Ошибка! Введите корректный вид деятельности.");
                                }
                                break;
                        }
                    }
                }
                else if (message.Type == MessageType.Photo && UserStates.TryGetValue(chatId, out var currentState) && currentState == "waiting_for_photo")
                {
                    var fileId = message.Photo[^1].FileId;
                    UserStates[chatId] = "waiting_for_market_status";
                    await botClient.SendTextMessageAsync(chatId, "Вы с рынка или нет?", replyMarkup: GetMarketChoiceKeyboard());
                }
            }
        }

        private bool IsValidPavilionNumber(string pavilionNumber)
        {
            return Regex.IsMatch(pavilionNumber, "^\\d+$");
        }

        private bool IsValidContractNumber(string contractNumber)
        {
            // Предположим, что номер договора аренды должен быть числовым или иметь определенную структуру
            return Regex.IsMatch(contractNumber, "^[A-Za-z0-9]+$");  // Пример для alphanumeric строк
        }

        private bool IsValidCompanyName(string companyName)
        {
            return !string.IsNullOrWhiteSpace(companyName);
        }

        private bool IsValidBusinessType(string businessType)
        {
            // Добавьте список допустимых видов деятельности, если нужно
            var validBusinessTypes = new[] { "Торговля", "Услуги", "Производство" };
            return validBusinessTypes.Contains(businessType);
        }


        private static IReplyMarkup GetMarketChoiceKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
            new KeyboardButton("Я с рынка"),
            new KeyboardButton("Я не с рынка")
        })
            { ResizeKeyboard = true };
        }

        private static Task ErrorHandler(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
