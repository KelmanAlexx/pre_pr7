using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartCampusRoleManager;

internal class Program
{
    // Допустимые роли
    private static readonly string[] Roles =
    {
        "Администратор",
        "Оператор проходной",
        "Сотрудник столовой",
        "Преподаватель",
        "Не назначена"
    };

    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "ИС «Умный кампус» — менеджер ролей";

        AppSettings settings = LoadSettings();
        List<OperatorUser> users = new List<OperatorUser>();

        using (HttpClient client = new HttpClient())
        {
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);

            bool exit = false;
            while (!exit)
            {
                ShowMenu();
                Console.Write("Выберите действие: ");
                string choice = Console.ReadLine();
                Console.WriteLine();

                switch (choice)
                {
                    case "1":
                        await LoadUsersAsync(users, settings, client);
                        break;
                    case "2":
                        ShowUsers(users);
                        break;
                    case "3":
                        AssignRole(users);
                        break;
                    case "4":
                        FilterUsers(users);
                        break;
                    case "5":
                        ExportAccessTable(users, settings);
                        break;
                    case "0":
                        exit = true;
                        Console.WriteLine("Работа программы завершена.");
                        break;
                    default:
                        Console.WriteLine("Неизвестная команда. Выберите пункт от 0 до 5.");
                        break;
                }

                if (!exit)
                {
                    Console.WriteLine();
                    Console.WriteLine("Нажмите Enter, чтобы продолжить...");
                    Console.ReadLine();
                    Console.Clear();
                }
            }
        }
    }

    // Читаем настройки из appsettings.json
    private static AppSettings LoadSettings()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(configPath))
        {
            Console.WriteLine("Предупреждение: appsettings.json не найден. Используются настройки по умолчанию.");
            return new AppSettings();
        }

        try
        {
            string json = File.ReadAllText(configPath, Encoding.UTF8);
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (settings == null)
            {
                Console.WriteLine("Предупреждение: настройки пусты. Используются значения по умолчанию.");
                return new AppSettings();
            }

            if (settings.TimeoutSeconds < 1 || settings.TimeoutSeconds > 120)
            {
                settings.TimeoutSeconds = 15;
            }

            if (string.IsNullOrWhiteSpace(settings.ApiUrl))
            {
                settings.ApiUrl = "https://reqres.in/api/users?page=1";
            }

            if (string.IsNullOrWhiteSpace(settings.OutputDirectory))
            {
                settings.OutputDirectory = "Reports";
            }

            if (string.IsNullOrWhiteSpace(settings.ReqresEnvironment))
            {
                settings.ReqresEnvironment = "prod";
            }

            return settings;
        }
        catch (JsonException)
        {
            Console.WriteLine("Ошибка формата appsettings.json. Используются настройки по умолчанию.");
            return new AppSettings();
        }
        catch (IOException exception)
        {
            Console.WriteLine("Не удалось прочитать файл настроек: " + exception.Message);
            return new AppSettings();
        }
    }

    private static void ShowMenu()
    {
        Console.WriteLine("===============================================");
        Console.WriteLine("       ИС «УМНЫЙ КАМПУС» — МЕНЕДЖЕР РОЛЕЙ");
        Console.WriteLine("===============================================");
        Console.WriteLine("1. Загрузить список пользователей из API");
        Console.WriteLine("2. Показать весь список пользователей");
        Console.WriteLine("3. Назначить или изменить роль пользователя");
        Console.WriteLine("4. Отфильтровать пользователей по роли");
        Console.WriteLine("5. Сохранить таблицу доступов в CSV");
        Console.WriteLine("0. Выход");
        Console.WriteLine("-----------------------------------------------");
    }

    // Получаем страницу пользователей через HTTPS и преобразуем ответ API в локальные записи.
    private static async Task LoadUsersAsync(
        List<OperatorUser> users,
        AppSettings settings,
        HttpClient client)
    {
        string apiKey = Environment.GetEnvironmentVariable("REQRES_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = " ";
            Console.WriteLine("Предупреждение: не задан ключ API.");
            Console.WriteLine("Создайте ключ и сохраните его в переменную REQRES_API_KEY.");
            Console.WriteLine("Производится попытка доступа без API ключа.");
        }

        try
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, settings.ApiUrl))
            {
                request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
                request.Headers.TryAddWithoutValidation("X-Reqres-Env", settings.ReqresEnvironment);

                Console.WriteLine("Подключение к API...");

                using (HttpResponseMessage response = await client.SendAsync(request))
                {
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode == HttpStatusCode.Unauthorized ||
                        response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        Console.WriteLine("Ошибка доступа к API (401/403). Проверьте ключ REQRES_API_KEY.");
                        return;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Сервер вернул ошибку: " + (int)response.StatusCode + " " + response.ReasonPhrase);
                        Console.WriteLine("Повторите попытку позже или проверьте настройки сети.");
                        return;
                    }

                    ApiResponse apiResponse = JsonSerializer.Deserialize<ApiResponse>(
                        responseBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (apiResponse == null || apiResponse.Data == null || apiResponse.Data.Count == 0)
                    {
                        Console.WriteLine("Ответ получен, но список пользователей пуст или имеет неверный формат.");
                        return;
                    }

                    users.Clear();
                    foreach (ApiUser apiUser in apiResponse.Data)
                    {
                        OperatorUser user = new OperatorUser();
                        user.Id = apiUser.Id;
                        user.FirstName = SafeText(apiUser.FirstName);
                        user.LastName = SafeText(apiUser.LastName);
                        user.Email = SafeText(apiUser.Email);
                        user.Role = "Не назначена";
                        users.Add(user);
                    }

                    Console.WriteLine("[OK] Получено пользователей: " + users.Count);
                    Console.WriteLine("Источник: " + settings.ApiUrl);
                    Console.WriteLine("Роли не загружаются из API и назначаются оператором отдельно.");
                }
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Ошибка: время ожидания ответа истекло. Проверьте интернет-соединение.");
        }
        catch (HttpRequestException exception)
        {
            Console.WriteLine("Ошибка сетевого соединения: " + exception.Message);
        }
        catch (JsonException)
        {
            Console.WriteLine("Ошибка: сервер вернул данные в неизвестном формате JSON.");
        }
    }

    private static string SafeText(string value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        return value.Trim();
    }

    private static void ShowUsers(List<OperatorUser> users)
    {
        if (users.Count == 0)
        {
            Console.WriteLine("Список пуст. Сначала выполните загрузку пользователей.");
            return;
        }

        PrintUsers(users);
    }

    private static void PrintUsers(List<OperatorUser> list)
    {
        Console.WriteLine("{0,-5} {1,-18} {2,-29} {3}", "ID", "ФИО", "Электронная почта", "Роль");
        Console.WriteLine(new string('-', 90));

        foreach (OperatorUser user in list)
        {
            string fullName = (user.FirstName + " " + user.LastName).Trim();
            Console.WriteLine("{0,-5} {1,-18} {2,-29} {3}",
                user.Id,
                LimitText(fullName, 17),
                LimitText(user.Email, 28),
                user.Role);
        }

        Console.WriteLine();
        Console.WriteLine("Всего записей: " + list.Count);
    }

    private static string LimitText(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength - 3) + "...";
    }

    // Меняем роль
    private static void AssignRole(List<OperatorUser> users)
    {
        if (users.Count == 0)
        {
            Console.WriteLine("Сначала загрузите пользователей.");
            return;
        }

        Console.Write("Введите ID пользователя: ");
        int id;
        if (!int.TryParse(Console.ReadLine(), out id))
        {
            Console.WriteLine("ID должен быть целым числом.");
            return;
        }

        OperatorUser selectedUser = users.FirstOrDefault(user => user.Id == id);
        if (selectedUser == null)
        {
            Console.WriteLine("Пользователь с таким ID не найден.");
            return;
        }

        Console.WriteLine("Пользователь: " + selectedUser.FirstName + " " + selectedUser.LastName);
        Console.WriteLine("Выберите роль:");

        for (int index = 0; index < Roles.Length; index++)
        {
            Console.WriteLine((index + 1) + ". " + Roles[index]);
        }

        Console.Write("Номер роли: ");
        int roleNumber;
        if (!int.TryParse(Console.ReadLine(), out roleNumber) || roleNumber < 1 || roleNumber > Roles.Length)
        {
            Console.WriteLine("Неверный номер роли. Изменения не сохранены.");
            return;
        }

        selectedUser.Role = Roles[roleNumber - 1];
        Console.WriteLine("Роль изменена: " + selectedUser.Role);
    }

    private static void FilterUsers(List<OperatorUser> users)
    {
        if (users.Count == 0)
        {
            Console.WriteLine("Сначала загрузите пользователей.");
            return;
        }

        Console.WriteLine("Выберите роль для фильтрации:");
        for (int index = 0; index < Roles.Length; index++)
        {
            Console.WriteLine((index + 1) + ". " + Roles[index]);
        }

        Console.Write("Номер роли: ");
        int roleNumber;
        if (!int.TryParse(Console.ReadLine(), out roleNumber) || roleNumber < 1 || roleNumber > Roles.Length)
        {
            Console.WriteLine("Неверный номер роли.");
            return;
        }

        string selectedRole = Roles[roleNumber - 1];
        List<OperatorUser> filteredUsers = users
            .Where(user => user.Role == selectedRole)
            .ToList();

        Console.WriteLine("Результат фильтрации: " + selectedRole);
        if (filteredUsers.Count == 0)
        {
            Console.WriteLine("Пользователи с выбранной ролью не найдены.");
            return;
        }

        PrintUsers(filteredUsers);
    }

    // Формируем CSV с разделителем ';' для удобного открытия в Excel.
    private static void ExportAccessTable(List<OperatorUser> users, AppSettings settings)
    {
        if (users.Count == 0)
        {
            Console.WriteLine("Нет данных для экспорта. Сначала загрузите пользователей.");
            return;
        }

        try
        {
            // Отчеты хранятся отдельно от Program Files, чтобы обычный оператор мог записывать CSV без права изменения файлов приложения.
            string commonDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string outputRoot = Path.Combine(commonDataFolder, "SmartCampus");
            string outputFolder = Path.Combine(outputRoot, settings.OutputDirectory);
            Directory.CreateDirectory(outputFolder);

            string fileName = "Таблица_доступов_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
            string filePath = Path.Combine(outputFolder, fileName);
            StringBuilder csv = new StringBuilder();

            csv.AppendLine("ID;Имя;Фамилия;Электронная почта;Роль;Статус записи;Дата формирования");

            string createdAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            foreach (OperatorUser user in users)
            {
                string status = user.Role == "Не назначена" ? "Не назначена" : "Роль указана";
                csv.Append(EscapeCsv(user.Id.ToString())).Append(';');
                csv.Append(EscapeCsv(user.FirstName)).Append(';');
                csv.Append(EscapeCsv(user.LastName)).Append(';');
                csv.Append(EscapeCsv(user.Email)).Append(';');
                csv.Append(EscapeCsv(user.Role)).Append(';');
                csv.Append(EscapeCsv(status)).Append(';');
                csv.AppendLine(EscapeCsv(createdAt));
            }

            // Помогаем Excel правильно распознать русские символы в UTF-8.
            File.WriteAllText(filePath, csv.ToString(), new UTF8Encoding(true));
            Console.WriteLine("[OK] Таблица сохранена:");
            Console.WriteLine(filePath);
            Console.WriteLine("Количество строк: " + users.Count);
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("Нет прав на запись в папку отчетов. Обратитесь к администратору.");
        }
        catch (IOException exception)
        {
            Console.WriteLine("Не удалось сохранить файл: " + exception.Message);
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}

internal class AppSettings
{
    public string ApiUrl { get; set; } = "https://reqres.in/api/users?page=1";
    public string ReqresEnvironment { get; set; } = "prod";
    public int TimeoutSeconds { get; set; } = 15;
    public string OutputDirectory { get; set; } = "Reports";
}

internal class ApiResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("data")]
    public List<ApiUser> Data { get; set; } = new List<ApiUser>();
}

internal class ApiUser
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

internal class OperatorUser
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Не назначена";
}
