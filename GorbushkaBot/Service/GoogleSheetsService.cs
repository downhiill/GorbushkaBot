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
                    userData.GetValueOrDefault("nomer_soiza", "Не указано"),
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
                    userData.GetValueOrDefault("nomer_soiza", "Не указано"),
                    telegramUsername ?? "Не указано",
                    chatId
                }}
            };

            var request = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, "Пользователи!A2:P");
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();

            
        }

    }
}
