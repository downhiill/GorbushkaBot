using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Sheets.v4;

namespace GorbushkaBot
{
    // Сервис для работы с Google Sheets
    public class GoogleSheetsService
    {
        private readonly SheetsService _service;
        private readonly string _spreadsheetId;
        private const string Range = "Лист1!A:M";

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
                    userData.GetValueOrDefault("company_name", ""),
                    userData.GetValueOrDefault("company_activity", ""),
                    userData.GetValueOrDefault("pavilion_number", ""),
                    userData.GetValueOrDefault("rental_contract", ""),
                    userData["pavilion_photos"]
                }}
            };

            var request = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, Range);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();
        }
    }
}
