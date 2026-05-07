# AGENTS.md

## Cursor Cloud specific instructions

This is a **single-project WPF .NET 8 Windows desktop application** with no backend services, no database, no containers, and no test projects.

### Building on Linux

The project targets `net8.0-windows` with WPF enabled. To build on this Linux VM, you **must** set the environment variable `EnableWindowsTargeting=true` (already configured in `~/.bashrc`). With this variable set, all standard `dotnet` commands work:

```bash
dotnet restore VpnSpeedAnalyzer.csproj
dotnet build VpnSpeedAnalyzer.csproj -c Debug
dotnet build VpnSpeedAnalyzer.csproj -c Release
dotnet publish VpnSpeedAnalyzer.csproj -c Release -r win-x64 --self-contained true -o out
```

### Linting

```bash
dotnet format --verify-no-changes   # whitespace/style check
dotnet build -warnaserror           # treat warnings as errors
```

Note: The repo has some pre-existing whitespace formatting issues reported by `dotnet format`.

### Tests

There are **no automated test projects** in this repository. Validation is done via successful build + publish.

### Runtime limitations

- The application **cannot run** on Linux (WPF requires Windows). The "hello world" task for this codebase is a successful Debug + Release build and a standalone publish.
- At runtime on Windows, the app requires `speedtest.exe` (Ookla CLI) in PATH for speed measurements.

### Key paths

| Item | Path |
|------|------|
| Project file | `VpnSpeedAnalyzer.csproj` |
| CI workflow | `.github/workflows/build.yml` |
| .NET SDK | `/usr/share/dotnet-latest` (v8.0.420) |

### Правила командной работы

- При добавлении или изменении комментариев в коде писать их на русском.
- Сообщения коммитов по умолчанию писать на русском.
- Перед переходом в новый чат всегда готовить короткий хендовер:
  - что уже сделано,
  - что осталось открытым,
  - текущее состояние git,
  - следующие рекомендуемые шаги.
- Так как это WPF desktop-приложение, финальную визуальную проверку выполнять на Windows, а проверки сборки/publish оставлять в GitHub Actions.
- Если изменяется **только документация** (`README`, заметки, комментарии в docs-файлах), пушить коммит с маркером `"[skip ci]"` в сообщении коммита, чтобы GitHub Actions не запускал build без необходимости.
- Если меняется **логика приложения** (код, XAML, сервисы, view-model, детекторы, алгоритмы), маркер `"[skip ci]"` не использовать — автосборка в GitHub Actions должна запускаться обязательно.

### UI Style Contract (обязательно для новых/изменённых окон)

- Для новых окон/экранов использовать общую тему из `Styles/Theme.xaml` и не дублировать локально базовые кисти/стили.
- Базовые отступы и сетка: шаг `8/12/16`; основной внутренний отступ окна/карточки — `12`.
- Использовать только существующие акцентные цвета и кисти (`BgBrush`, `PanelBrush`, `PanelSecondaryBrush`, `AccentBrush`, `TextBrush`, `MutedTextBrush`, `OkBrush`).
- Кнопки, скроллбары и базовые контролы должны быть в едином стиле с основным приложением (шаблоны из общей темы).
- Для новых модальных окон: контент в верхней панели/карточке, нижние действия — отдельной нижней полосой, как в `MainWindow`.
- Перед завершением задачи по UI обязательно самопроверка:
  - совпадают отступы по сетке,
  - нет локальных «разовых» цветов/стилей без необходимости,
  - hover/pressed/focus ведут себя одинаково,
  - скроллбар визуально совпадает с основным окном.
