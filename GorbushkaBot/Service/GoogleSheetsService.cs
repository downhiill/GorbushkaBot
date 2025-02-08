using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Sheets.v4;
using GorbushkaBot.Model;
using GorbushkaBot.AppDbContext;

namespace GorbushkaBot.Service
{
    // Сервис для работы с Google Sheets
    public class GoogleSheetsService
    {
        private readonly SheetsService _service;
        private readonly string _spreadsheetId;
        private const string Range = "Лист1!A:K";
        private readonly UserApplicationService _userApplicationService;

        public GoogleSheetsService(string credentialPath, string spreadsheetId)
        {
            var credential = GoogleCredential.FromFile(credentialPath)
                .CreateScoped(SheetsService.Scope.Spreadsheets);

            _service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "GorbushkaBot"
            });

            _spreadsheetId = spreadsheetId;
        }

        public async Task AppendDataAsync(Dictionary<string, string> userData, string folderUrl)
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
                    userData["passport_issue_date"],
                    userData["registration_address"],
                    userData["passport_photos"],
                    userData.GetValueOrDefault("pavilion_number", ""),
                    userData.GetValueOrDefault("rental_contract", ""),
                    userData["pavilion_photos"]
                }}
            };

            var request = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, Range);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();

            await _userApplicationService.SaveUserApplicationAsync(userData, folderUrl);
        }

        public async Task DeleteDataAsync(int rowIndex)
        {
            try
            {
                // Удаление данных в строке (по индексу)
                var request = _service.Spreadsheets.Values.Clear(new ClearValuesRequest(), _spreadsheetId, $"Лист1!A{rowIndex}:M{rowIndex}");
                await request.ExecuteAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при удалении данных: {ex.Message}");
            }
        }

        public async Task AppendUserDataAsync(Dictionary<string, string> userData)
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
                    userData["passport_issue_date"],
                    userData["registration_address"],
                    userData["passport_photo"],
                    userData.GetValueOrDefault("pavilion_number", ""),
                    userData.GetValueOrDefault("rental_contract", ""),
                    userData["pavilion_photo"]
                }}
            };

            var request = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, "Пользователь!A:M");
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();

            
        }

        public async Task<int> GetRowIndexFromCallbackData(string callbackData)
        {
            string[] parts = callbackData.Split('_');
            if (parts.Length < 2 || !long.TryParse(parts[1], out long targetChatId))
            {
                throw new ArgumentException("Некорректные данные callback");
            }

            // Получаем данные из таблицы "Лист1"
            var request = _service.Spreadsheets.Values.Get(_spreadsheetId, "Лист1!A:M");
            var response = await request.ExecuteAsync();

            // Ищем строку с нужным chatId
            for (int rowIndex = 1; rowIndex < response.Values.Count; rowIndex++) // Начинаем с 1, чтобы пропустить заголовки
            {
                var row = response.Values[rowIndex];
                if (row.Count > 1 && row[1].ToString() == targetChatId.ToString()) // Предполагаем, что ChatId в колонке 2
                {
                    return rowIndex + 1; // Индекс строки с учетом 1
                }
            }

            throw new Exception($"Заявка с chatId {targetChatId} не найдена.");
        }

        public async Task<Dictionary<string, string>> GetDataFromRow(int rowIndex)
        {
            // Получаем данные из строки по индексу
            var request = _service.Spreadsheets.Values.Get(_spreadsheetId, $"Лист1!A{rowIndex}:M{rowIndex}");
            var response = await request.ExecuteAsync();

            var row = response.Values.FirstOrDefault();
            if (row == null)
            {
                throw new Exception($"Данные в строке {rowIndex} не найдены.");
            }

            // Преобразуем строку в словарь для добавления в "Пользователь"
            var userData = new Dictionary<string, string>
            {
                { "date", row[0].ToString() },
                { "face_photo", row[1].ToString() },
                { "fio", row[2].ToString() },
                { "phone_number", row[3].ToString() },
                { "passport_number", row[4].ToString() },
                { "passport_issue_date", row[5].ToString() },
                { "registration_address", row[6].ToString() },
                { "passport_photos", row[7].ToString() },
                { "company_name", row[8].ToString() },
                { "company_activity", row[9].ToString() },
                { "pavilion_number", row[10].ToString() },
                { "rental_contract", row[11].ToString() },
                { "pavilion_photos", row[12].ToString() }
            };

            return userData;
        }

    }
}
