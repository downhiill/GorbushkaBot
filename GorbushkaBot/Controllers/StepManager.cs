using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GorbushkaBot.Controllers
{
    public class StepManager
    {
        private static readonly Dictionary<long, (string step, int messageId)> UserSteps = new();
        private readonly TelegramBotClient botClient;

        public StepManager(TelegramBotClient botClient)
        {
            this.botClient = botClient;
        }

        public void SaveStep(long chatId, string step, int messageId)
        {
            UserSteps[chatId] = (step, messageId);
        }

        public async Task HandleMessage(ITelegramBotClient botClient, long chatId, Message message)
        {
            if (!UserSteps.ContainsKey(chatId)) return;

            var (step, messageId) = UserSteps[chatId];

            if (step == "fio")
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(message.Text, "^[А-Яа-яA-Za-z ]+$"))
                {
                    await botClient.SendTextMessageAsync(chatId, "Ошибка: ФИО должно содержать только буквы и пробелы.");
                    return;
                }
                await UpdateVerificationStep(botClient, chatId, messageId, "passport", "Отправьте фото вашего паспорта", true);
            }
            else if (step == "passport")
            {
                if (message.Photo == null)
                {
                    await botClient.SendTextMessageAsync(chatId, "Ошибка: Отправьте именно фото, а не текст или документ.");
                    return;
                }
                await botClient.SendTextMessageAsync(chatId, "Фото получено!");
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
            else if (step == "company_activity")
            {
                await UpdateVerificationStep(botClient, chatId, messageId, "completed", "Заявка заполнена.\n\nВыберите действие:", false,
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("Заполнить заново", "verify") },
                        new[] { InlineKeyboardButton.WithCallbackData("Отправить", "submit") }
                    }));
            }
            else if (step == "rental_contract")
            {
                await UpdateVerificationStep(botClient, chatId, messageId, "completed", "Заявка заполнена.\n\nВыберите действие:", false,
                    new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("Заполнить заново", "verify") },
                        new[] { InlineKeyboardButton.WithCallbackData("Отправить", "submit") }
                    }));
            }
        }

        public async Task HandleCallbackQuery(ITelegramBotClient botClient, long chatId, CallbackQuery callbackQuery)
        {
            string data = callbackQuery.Data;

            switch (data)
            {
                case "verify":
                    await UpdateVerificationStep(botClient, chatId, callbackQuery.Message.MessageId, "fio", "Введите ваше ФИО:", false);
                    break;

                case "seller":
                case "both":
                    await UpdateVerificationStep(botClient, chatId, callbackQuery.Message.MessageId, "market", "Вы с рынка?", false,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Да", "market_yes") },
                            new[] { InlineKeyboardButton.WithCallbackData("Нет", "market_no") }
                        }));
                    break;

                case "market_yes":
                    await UpdateVerificationStep(botClient, chatId, callbackQuery.Message.MessageId, "pavilion_number", "Введите номер вашего павильона:", true);
                    break;

                case "market_no":
                    await UpdateVerificationStep(botClient, chatId, callbackQuery.Message.MessageId, "company_name", "Введите название вашей компании:", true);
                    break;

                case "buyer":
                    await UpdateVerificationStep(botClient, chatId, callbackQuery.Message.MessageId, "completed", "Заявка заполнена.\n\nВыберите действие:", false,
                        new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Заполнить заново", "verify") },
                            new[] { InlineKeyboardButton.WithCallbackData("Отправить", "submit") }
                        }));
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
            SaveStep(chatId, nextStep, messageId);
        }
    }
}
