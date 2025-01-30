using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GorbushkaBot.Controllers
{
    public class TelegramBotService
    {
        private static readonly string BotToken = Environment.GetEnvironmentVariable("BOT_TOKEN")
            ?? throw new InvalidOperationException("BOT_TOKEN is not set");

        private readonly TelegramBotClient botClient;
        private static readonly Dictionary<long, (string step, int messageId)> UserSteps = new();

        public TelegramBotService()
        {
            botClient = new TelegramBotClient(BotToken);
        }

        public void Start()
        {
            botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync);
            Console.WriteLine("Бот запущен...");
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, System.Threading.CancellationToken cancellationToken)
        {
            if (update.Message is { } message)
            {
                await HandleMessage(botClient, message);
            }
            else if (update.CallbackQuery is { } callbackQuery)
            {
                await HandleCallbackQuery(botClient, callbackQuery);
            }
        }

        private async Task HandleMessage(ITelegramBotClient botClient, Message message)
        {
            long chatId = message.Chat.Id;

            if (message.Text == "/start")
            {
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
            new[] { InlineKeyboardButton.WithCallbackData("Перейти к верификации", "verify") }
        });

                Message sentMessage = await botClient.SendTextMessageAsync(chatId, "Добро пожаловать в систему!", replyMarkup: inlineKeyboard);
                UserSteps[chatId] = ("start", sentMessage.MessageId);
            }
            else if (UserSteps.ContainsKey(chatId))
            {
                var (step, messageId) = UserSteps[chatId];

                if (step == "fio")
                {
                    await UpdateVerificationStep(botClient, chatId, messageId, "passport", "Отправьте фото вашего паспорта", true);
                }
                else if (step == "passport" && message.Photo != null) // Обрабатываем фото
                {
                    await botClient.SendTextMessageAsync(chatId, "Фото получено!"); // Подтверждение
                    await UpdateVerificationStep(botClient, chatId, messageId, "role", "Выберите свою роль:", false,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Продавец", "seller") },
                            new[] { InlineKeyboardButton.WithCallbackData("Покупатель", "buyer") },
                            new[] { InlineKeyboardButton.WithCallbackData("Продавец и Покупатель", "both") }
                        }));
                }
                else if (step == "company_name")
                {
                    await UpdateVerificationStep(botClient, chatId, messageId, "company_activity", "Введите вид деятельности вашей компании:", true);
                }
                else if (step == "pavilion_number")
                {
                    await UpdateVerificationStep(botClient, chatId, messageId, "rental_contract", "Введите номер вашего договора аренды:", true);
                }
            }
        }


        private async Task HandleCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            long chatId = callbackQuery.Message.Chat.Id;
            int messageId = callbackQuery.Message.MessageId;
            string data = callbackQuery.Data;

            switch (data)
            {
                case "verify":
                    await UpdateVerificationStep(botClient, chatId, messageId, "fio", "Введите ваше ФИО:", false);
                    break;

                case "passport":
                    await UpdateVerificationStep(botClient, chatId, messageId, "role", "Выберите свою роль:", false,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Продавец", "seller") },
                            new[] { InlineKeyboardButton.WithCallbackData("Покупатель", "buyer") },
                            new[] { InlineKeyboardButton.WithCallbackData("Продавец и Покупатель", "both") }
                        }));
                    break;

                case "seller":
                case "both":
                    await UpdateVerificationStep(botClient, chatId, messageId, "market", "Вы с рынка?", false,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Да", "market_yes") },
                            new[] { InlineKeyboardButton.WithCallbackData("Нет", "market_no") }
                        }));
                    break;

                case "market_yes":
                    await UpdateVerificationStep(botClient, chatId, messageId, "pavilion_number", "Введите номер вашего павильона:", true);
                    break;

                case "market_no":
                    await UpdateVerificationStep(botClient, chatId, messageId, "company_name", "Введите название вашей компании:", true);
                    break;

                case "buyer":
                    await UpdateVerificationStep(botClient, chatId, messageId, "completed", "Заявка заполнена.\n\nВыберите действие:", false,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Заполнить заново", "verify") },
                            new[] { InlineKeyboardButton.WithCallbackData("Отправить", "submit") }
                        }));
                    break;

                case "rental_contract":
                case "company_activity":
                    await UpdateVerificationStep(botClient, chatId, messageId, "completed", "Заявка заполнена.\n\nВыберите действие:", false,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Заполнить заново", "verify") },
                            new[] { InlineKeyboardButton.WithCallbackData("Отправить", "submit") }
                        }));
                    break;

                case "back":
                    await GoBackStep(botClient, chatId, messageId);
                    break;
            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
        }

        private async Task UpdateVerificationStep(ITelegramBotClient botClient, long chatId, int messageId, string nextStep, string messageText, bool isTextInput, InlineKeyboardMarkup? keyboard = null)
        {
            if (keyboard == null)
            {
                keyboard = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Назад", "back") } });
            }

            await botClient.EditMessageTextAsync(chatId, messageId, messageText, replyMarkup: keyboard);
            UserSteps[chatId] = (nextStep, messageId);
        }

        private async Task GoBackStep(ITelegramBotClient botClient, long chatId, int messageId)
        {
            if (!UserSteps.ContainsKey(chatId)) return;

            var (currentStep, _) = UserSteps[chatId];

            var (previousStep, previousMessage, previousKeyboard) = currentStep switch
            {
                "passport" => ("fio", "Введите ваше ФИО:", null),
                "role" => ("passport", "Отправьте фото паспорта", null),
                "market" => ("role", "Выберите свою роль:", new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Продавец", "seller") },
                    new[] { InlineKeyboardButton.WithCallbackData("Покупатель", "buyer") },
                    new[] { InlineKeyboardButton.WithCallbackData("Продавец и Покупатель", "both") }
                })),
                "pavilion_number" => ("market", "Вы с рынка?", new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Да", "market_yes") },
                    new[] { InlineKeyboardButton.WithCallbackData("Нет", "market_no") }
                })),
                "company_name" => ("market", "Вы с рынка?", new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Да", "market_yes") },
                    new[] { InlineKeyboardButton.WithCallbackData("Нет", "market_no") }
                })),
                "rental_contract" => ("pavilion_number", "Введите номер вашего павильона:", null),
                "company_activity" => ("company_name", "Введите название вашей компании:", null),
                "completed" => ("role", "Выберите свою роль:", new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Продавец", "seller") },
                    new[] { InlineKeyboardButton.WithCallbackData("Покупатель", "buyer") },
                    new[] { InlineKeyboardButton.WithCallbackData("Продавец и Покупатель", "both") }
                })),
                _ => ("fio", "Введите ваше ФИО:", null)
            };

            await UpdateVerificationStep(botClient, chatId, messageId, previousStep, previousMessage, false, previousKeyboard);
        }


        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, System.Threading.CancellationToken cancellationToken)
        {
            Console.WriteLine($"Произошла ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
