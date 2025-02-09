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
        private readonly UserAcceptService _userAcceptService;

        public GoogleSheetsService(string credentialPath, string spreadsheetId, UserApplicationService userApplicationService, UserAcceptService userAcceptService)
        {
            var credential = GoogleCredential.FromFile(credentialPath)
                .CreateScoped(SheetsService.Scope.Spreadsheets);

            _service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "GorbushkaBot"
            });

            _spreadsheetId = spreadsheetId;
            _userApplicationService = userApplicationService; // Инициализация
            _userAcceptService = userAcceptService;
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
                    userData["role"],
                    userData["passport_issue_date"],
                    userData["registration_address"],
                    userData["passport_photo"],
                    userData.GetValueOrDefault("pavilion_number", ""),
                    userData.GetValueOrDefault("rental_contract", ""),
                    userData["pavilion_photo"]
                }}
            };

            var request = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, Range);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();

            
        }

        public async Task AppendUserDataAsync(Dictionary<string, string> userData, string folderUrl)
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
                    userData["registration_address"],
                    userData["passport_photo"],
                    userData.GetValueOrDefault("pavilion_number", ""),
                    userData.GetValueOrDefault("rental_contract", ""),
                    userData["pavilion_photo"]
                }}
            };

            var request = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, "Пользователь!A:K");
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();

            
        }

    }
}
