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
                        UserStates[chatId] = "waiting_for_verification";
                        var keyboard = new ReplyKeyboardMarkup(new[]
                        {
                            new KeyboardButton("Верификация")
                        })
                        {
                            ResizeKeyboard = true
                        };

                        await botClient.SendTextMessageAsync(chatId, "Добро пожаловать в бот! Нажмите 'Верификация', чтобы начать процесс.", replyMarkup: keyboard);
                    }
                    else if (message.Text == "Верификация" && UserStates.TryGetValue(chatId, out var state) && state == "waiting_for_verification")
                    {
                        UserStates[chatId] = "waiting_for_photo";
                        var keyboard = new ReplyKeyboardMarkup(new[]
                        {
                            new KeyboardButton("Назад")
                        })
                        {
                            ResizeKeyboard = true
                        };

                        await botClient.SendTextMessageAsync(chatId, "Отправьте фото паспорта для начала верификации.", replyMarkup: keyboard);
                    }
                    else if (message.Text == "Назад" && UserStates.TryGetValue(chatId, out var prevState))
                    {
                        UserStates[chatId] = GetPreviousState(prevState);
                        var keyboard = new ReplyKeyboardMarkup(new[]
                        {
                            new KeyboardButton("Верификация")
                        })
                        {
                            ResizeKeyboard = true
                        };

                        await botClient.SendTextMessageAsync(chatId, "Вы вернулись на шаг назад.", replyMarkup: keyboard);
                    }
                    else if (UserStates.TryGetValue(chatId, out var currentState))
                    {
                        switch (currentState)
                        {
                            case "waiting_for_market_status":
                                if (message.Text == "Я с рынка" || message.Text == "Я не с рынка")
                                {
                                    UserStates[chatId] = message.Text == "Я с рынка" ? "waiting_for_pavilion_number" : "waiting_for_company_name";
                                    await botClient.SendTextMessageAsync(chatId, message.Text == "Я с рынка" ? "Введите номер павильона." : "Введите название вашей компании.");
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(chatId, "Пожалуйста, выберите один из вариантов: 'Я с рынка' или 'Я не с рынка'.", replyMarkup: GetMarketChoiceKeyboard());
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
                        await botClient.SendTextMessageAsync(chatId, "Вы с рынка или нет?", replyMarkup: GetMarketChoiceKeyboard());
                    }
                    else if (message.Type == MessageType.Document && UserStates.TryGetValue(chatId, out var stateAfterDoc) && stateAfterDoc == "waiting_for_photo")
                    {
                        var fileName = message.Document.FileName.ToLower();
                        var mimeType = message.Document.MimeType;

                        if (!mimeType.StartsWith("image/") || !(fileName.EndsWith(".jpg") || fileName.EndsWith(".jpeg") || fileName.EndsWith(".png")))
                        {
                            await botClient.SendTextMessageAsync(chatId, "Ошибка! Отправьте фотографию паспорта, а не документ.");
                            return;
                        }

                        UserStates[chatId] = "waiting_for_market_status";
                        await botClient.SendTextMessageAsync(chatId, "Вы с рынка или нет?", replyMarkup: GetMarketChoiceKeyboard());
                    }
                }
            }
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

        private static IReplyMarkup GetMarketChoiceKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton("Я с рынка"),
                new KeyboardButton("Я не с рынка"),
                new KeyboardButton("Назад")
            })
            { ResizeKeyboard = true };
        }

        private static string GetPreviousState(string currentState)
        {
            return currentState switch
            {
                "waiting_for_market_status" => "waiting_for_photo",
                "waiting_for_pavilion_number" => "waiting_for_market_status",
                "waiting_for_contract_number" => "waiting_for_pavilion_number",
                "waiting_for_company_name" => "waiting_for_market_status",
                "waiting_for_business_type" => "waiting_for_company_name",
                _ => "waiting_for_photo",
            };
        }

        private static Task ErrorHandler(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
