# SmartCampusRoleManager (.NET 8)

Консольная утилита: загрузка списка пользователей из API, назначение локальных ролей, фильтрация и экспорт таблицы в CSV.

## Требования

- Windows 10/11 x64;
- .NET 8 SDK для сборки и запуска через `dotnet run`;
- действующий ключ API в `REQRES_API_KEY`;
- исходящий HTTPS-доступ к `reqres.in` через TCP 443.

## Настройка ключа (PowerShell)

```powershell
$env:REQRES_API_KEY = "КЛЮЧ"
```

Не помещайте ключ в `appsettings.json`, `Program.cs` или систему контроля версий.

## Запуск в Visual Studio

1. Открыть `SmartCampusRoleManager.csproj`.
2. Убедиться, что выбран .NET 8.
3. Задать переменную окружения `REQRES_API_KEY` в окружении запуска Visual Studio или запустить программу из PowerShell, где она уже задана.
4. Нажать **Start** или выполнить `dotnet run`.

## Работа

Главное меню позволяет загрузить пользователей, просмотреть список, назначить роль, отфильтровать записи и сохранить CSV. Отчеты сохраняются в `C:\ProgramData\SmartCampus\Reports`.