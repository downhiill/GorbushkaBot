using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Sheets.v4;
using GorbushkaBot.Model;
using GorbushkaBot.AppDbContext;
using Telegram.Bot.Types;

namespace GorbushkaBot.Service
{
    // Сервис для работы с Google Sheets
    public class GoogleSheetsService
    {
        private readonly SheetsService _service;
        private readonly string _spreadsheetId;
        public readonly string _spreedsheetcategoriesId;
        private const string Range = "Заявки!A2:P";
        private readonly UserApplicationService _userApplicationService;
        private readonly UserAcceptService _userAcceptService;
        private readonly ApplicationService _applicationService;

        public GoogleSheetsService(string credentialPath, string spreadsheetId,string spreedsheetcategoriesId, UserApplicationService userApplicationService, UserAcceptService userAcceptService)
        {
            var credential = GoogleCredential.FromFile(credentialPath)
                .CreateScoped(SheetsService.Scope.Spreadsheets);

            _service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "GorbushkaBot"
            });

            _spreadsheetId = spreadsheetId;
            _spreedsheetcategoriesId = spreedsheetcategoriesId;
            _userApplicationService = userApplicationService; 
            _userAcceptService = userAcceptService;
        }

        public async Task AppendDataAsync(Dictionary<string, string> userData, string folderUrl, long chatId, string telegramUsername = null)
        {
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { new List<object>
                {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    userData["face_photo"],
                    userData["fio"],
                    userData["phone_number"],
                    userData["passport_number"],
                    userData["role"],
                    userData["passport_issue_date"],
                    userData.GetValueOrDefault("passport_issue_date_end", "Не указано"),
                    userData.GetValueOrDefault("registration_address", ""),
                    userData["passport_photo"],
                    userData.GetValueOrDefault("propiska_photo", ""),
                    userData.GetValueOrDefault("pavilion_number", ""),
                    userData.GetValueOrDefault("rental_contract", ""),
                    userData["pavilion_photo"],
                    userData.GetValueOrDefault("soiuz_number", "Не указано"),
                    chatId,
                    telegramUsername ?? "Не указано" 
                }}
            };

            var request = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, Range);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();

            
        }
        public async Task<List<(string sheetName, string gid)>> GetSheetNamesAsync()
        {
            // Получаем метаданные таблицы
            var request = _service.Spreadsheets.Get(_spreedsheetcategoriesId);
            var response = await request.ExecuteAsync();

            // Извлекаем список названий листов и их GID
            var sheetNamesAndGids = response.Sheets
                .Select(sheet => (sheet.Properties.Title, sheet.Properties.SheetId.ToString()))
                .ToList();

            return sheetNamesAndGids;
        }

        public async Task AppendUserDataAsync(Dictionary<string, string> userData, string folderUrl, long chatId, string telegramUsername = null)
        {
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { new List<object>
                {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    userData["face_photo"],
                    userData["fio"],
                    userData["phone_number"],
                    userData["passport_number"],
                    userData["role"],
                    userData["passport_issue_date"],
                    userData.GetValueOrDefault("passport_issue_date_end", "Не указано"),
                    userData.GetValueOrDefault("registration_address", ""),
                    userData["passport_photo"],
                    userData.GetValueOrDefault("propiska_photo", ""),
                    userData.GetValueOrDefault("pavilion_number", ""),
                    userData.GetValueOrDefault("rental_contract", ""),
                    userData["pavilion_photo"],
                    userData.GetValueOrDefault("soiuz_number", "Не указано"),
                    telegramUsername ?? "Не указано",
                    chatId
                }}
            };

            var request = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, "Пользователи!A2:P");
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();

            
        }

        public async Task ProcessBlackListAsync()
        {
            try
            {
                // Получаем данные из листа "БлекЛист"
                var range = "БлекЛист!A2:U"; // Поменяйте на правильный диапазон
                var request = _service.Spreadsheets.Values.Get(_spreadsheetId, range);
                var response = await request.ExecuteAsync();

                var rows = response.Values;

                if (rows == null || rows.Count == 0)
                {
                    Console.WriteLine("Черный список пуст.");
                    return;
                }

                foreach (var row in rows)
                {
                    try
                    {
                        Console.WriteLine($"Обрабатываем строку: {string.Join(", ", row)}");

                        if (row.Count <= 20)
                        {
                            Console.WriteLine("Ошибка: строка слишком короткая!");
                            continue;
                        }

                        // Проверяем, что row[15] и row[20] не null
                        var telegramId = row[15]?.ToString()?.Trim();
                        if (string.IsNullOrEmpty(telegramId))
                        {
                            Console.WriteLine("Ошибка: Telegram ID отсутствует или пуст.");
                            continue;
                        }

                        if (!long.TryParse(telegramId, out long chatId))
                        {
                            Console.WriteLine($"Ошибка: неверный формат Telegram ID ({telegramId})");
                            continue;
                        }

                        // Обработка флага доступа
                        bool hasPaidAccess = false;
                        if (row[20] != null && bool.TryParse(row[20].ToString(), out bool result))
                        {
                            hasPaidAccess = result;
                        }
                        else
                        {
                            Console.WriteLine($"Ошибка при обработке флага доступа в строке: {string.Join(", ", row)}");
                            continue;
                        }

                        // Блокировка или разблокировка пользователя
                        if (!hasPaidAccess)
                        {
                            await BlockUserAsync(chatId.ToString());
                        }
                        else
                        {
                            await UnblockUserAsync(chatId.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при обработке строки: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обработке черного списка: {ex.Message}");
            }
        }


        private async Task BlockUserAsync(string telegramId)
        {
            try
            {
                Console.WriteLine($"Блокируем пользователя {telegramId}");

                if (!long.TryParse(telegramId, out var chatId))
                {
                    Console.WriteLine($"Неверный формат ID пользователя: {telegramId}");
                    return;
                }

                // Проверяем, что _applicationService инициализирован
                if (_applicationService == null)
                {
                    Console.WriteLine("Ошибка: _applicationService не инициализирован.");
                    return;
                }

                await _userApplicationService.AddUserToBlacklistAsync(chatId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при блокировке пользователя: {ex.Message}");
            }
        }

        private async Task UnblockUserAsync(string telegramId)
        {
            try
            {
                Console.WriteLine($"Разблокируем пользователя {telegramId}");

                if (!long.TryParse(telegramId, out var chatId))
                {
                    Console.WriteLine($"Неверный формат ID пользователя: {telegramId}");
                    return;
                }

                // Проверяем, что _applicationService инициализирован
                if (_applicationService == null)
                {
                    Console.WriteLine("Ошибка: _applicationService не инициализирован.");
                    return;
                }

                await _userApplicationService.RemoveUserFromBlacklistAsync(chatId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при разблокировке пользователя: {ex.Message}");
            }
        }



    }
}
