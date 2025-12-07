# JarvisCore Unit Tests

Unit-тесты для проекта JARVIS Assistant C# Core.

## Технологии

- **xUnit** - фреймворк для тестирования
- **Moq** - библиотека для создания мок-объектов
- **FluentAssertions** - библиотека для более читаемых assertions

## Структура тестов

```
JarvisCore.Tests/
├── ValidationTests/
│   ├── CommandValidatorTests.cs    - Тесты валидации команд
│   └── PathValidatorTests.cs       - Тесты валидации путей
└── ServicesTests/
    └── ApplicationRegistryTests.cs - Тесты реестра приложений
```

## Запуск тестов

### Все тесты

```bash
cd core
dotnet test JarvisCore.Tests/JarvisCore.Tests.csproj
```

### С подробным выводом

```bash
dotnet test JarvisCore.Tests/JarvisCore.Tests.csproj --verbosity detailed
```

### С покрытием кода

```bash
dotnet test JarvisCore.Tests/JarvisCore.Tests.csproj /p:CollectCoverage=true
```

### Конкретный тестовый класс

```bash
dotnet test --filter "FullyQualifiedName~CommandValidatorTests"
```

### Конкретный тест

```bash
dotnet test --filter "FullyQualifiedName~CommandValidatorTests.Validate_ValidOpenAppCommand_ShouldReturnValid"
```

## Что тестируется

### CommandValidatorTests (15 тестов)

- ✅ Валидация корректных команд (open_app, create_folder, move_file, copy_file, scan_applications)
- ✅ Проверка пустых и неизвестных действий
- ✅ Проверка отсутствующих и пустых параметров
- ✅ Валидация UUID
- ✅ Валидация timestamp (ISO 8601 формат)

### PathValidatorTests (10 тестов)

- ✅ Проверка допустимых пользовательских путей
- ✅ Блокировка системных путей (C:\Windows, Program Files)
- ✅ Блокировка корневых дисков (C:\, D:\)
- ✅ Раскрытие environment variables (%USERPROFILE%, %TEMP%, %APPDATA%)
- ✅ Проверка null/empty/whitespace путей

### ApplicationRegistryTests (10 тестов)

- ✅ Поиск приложений по точному имени
- ✅ Поиск по алиасам
- ✅ Case-insensitive поиск
- ✅ Получение всех приложений
- ✅ Фильтрация по категории
- ✅ Статистика приложений
- ✅ Слияние приложений при сканировании

## Покрытие

**Всего: 35 тестов**

- ✅ CommandValidatorTests: 15 тестов
- ✅ PathValidatorTests: 10 тестов
- ✅ ApplicationRegistryTests: 10 тестов

## Примеры использования

### Добавление нового теста

```csharp
[Fact]
public void YourTestName_TestScenario_ExpectedResult()
{
    // Arrange - подготовка данных
    var request = new CommandRequest { ... };

    // Act - выполнение действия
    var result = _validator.Validate(request);

    // Assert - проверка результата
    result.IsValid.Should().BeTrue();
}
```

### Использование Theory для параметризованных тестов

```csharp
[Theory]
[InlineData("path1")]
[InlineData("path2")]
[InlineData("path3")]
public void TestName_MultiplePaths_ShouldWork(string path)
{
    // Тест будет выполнен 3 раза с разными параметрами
    var result = PathValidator.IsValidPath(path);
    result.Should().BeTrue();
}
```

## Continuous Integration

Тесты автоматически запускаются при:
- Push в ветку main
- Создании Pull Request
- Перед релизом

## Полезные команды

```bash
# Запуск тестов с фильтром по категории
dotnet test --filter "Category=Validation"

# Запуск с параллельным выполнением
dotnet test --parallel

# Генерация отчета о покрытии
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Просмотр списка всех тестов
dotnet test --list-tests
```

## Добавление новых тестов

1. Создайте новый файл в соответствующей папке (ValidationTests/ или ServicesTests/)
2. Наследуйтесь от базового класса (если нужно)
3. Используйте атрибуты [Fact] для простых тестов или [Theory] для параметризованных
4. Следуйте паттерну Arrange-Act-Assert
5. Используйте FluentAssertions для более читаемых проверок

## Troubleshooting

### Тесты не запускаются

```bash
# Восстановите пакеты
dotnet restore JarvisCore.Tests/

# Пересоберите проект
dotnet build JarvisCore.Tests/
```

### Тесты падают с ошибкой зависимостей

Убедитесь, что основной проект JarvisCore собирается без ошибок:

```bash
cd core
dotnet build JarvisCore.csproj
```
