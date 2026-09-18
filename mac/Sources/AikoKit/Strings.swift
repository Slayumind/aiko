// Made by tools/strings.py from tools/strings.json. Do not edit by hand: run the script.

import Foundation

/// The two languages Aiko has words for. The setting also has "follow the system", which is
/// turned into one of these before the app asks for a word.
public enum StringLanguage: Sendable, Equatable {
    case english
    case russian
}

/// Every word Aiko says, in the language the user chose. One property per string, so a key that
/// does not exist is a build error and not a blank label somebody finds months later.
///
/// The table is the twin of Strings.resx and Strings.ru.resx on Windows.
public enum Strings {
    /// Set at startup and again when the language changes. The lock is there because the bridge
    /// and the app may ask from another thread, not because the value changes often.
    nonisolated(unsafe) private static var chosen = StringLanguage.english
    private static let lock = NSLock()

    public static var language: StringLanguage {
        get { lock.lock(); defer { lock.unlock() }; return chosen }
        set { lock.lock(); defer { lock.unlock() }; chosen = newValue }
    }

    public static func get(_ key: String) -> String {
        guard let pair = table[key] else { return key }
        return language == .russian ? pair.russian : pair.english
    }

    /// Fills {0}, {1} and so on, the way string.Format does on Windows. The texts are shared, so
    /// the placeholders have to be read the same way on both systems.
    public static func format(_ text: String, _ arguments: CustomStringConvertible...) -> String {
        var filled = text
        for (index, argument) in arguments.enumerated() {
            filled = filled.replacingOccurrences(of: "{\(index)}", with: argument.description)
        }
        return filled
    }

    public static let table: [String: (english: String, russian: String)] = [
        "CardSession": ("Session · 5 hours", "Сессия · 5 часов"),
        "CardWeek": ("Week", "Неделя"),
        "CardModelWeek": ("{0} · week", "{0} · неделя"),
        "CardPercentUsed": ("{0}% used", "потрачено {0}%"),
        "CardResets": ("resets in {0}", "сброс через {0}"),
        "CardJustReset": ("just reset", "только что сброшен"),
        "CardLastsUntilReset": ("lasts until reset", "хватит до сброса"),
        "CardAtThisPace": ("~{0} at this pace", "хватит на ~{0}"),
        "CardToneCaution": ("running low", "на исходе"),
        "CardToneCritical": ("almost gone", "почти не осталось"),
        "CardNoDataYet": ("no data yet", "данных пока нет"),
        "CardLastSeen": ("as of {0}", "данные на {0}"),
        "CardUpdated": ("updated {0}", "обновлено в {0}"),
        "CardNoDataNote": ("Open Claude Code. Limits show up after its first answer.", "Открой Claude Code. Лимиты появятся после первого ответа."),
        "CardNoAccessNote": ("Aiko can't see the limits of this environment yet. You can turn on access in settings.", "Aiko пока не видит лимиты этой среды. Доступ включается в настройках."),
        "SpanDaysHours": ("{0}d {1}h", "{0} д {1} ч"),
        "SpanHoursMinutes": ("{0}h {1}m", "{0} ч {1} м"),
        "SpanMinutes": ("{0}m", "{0} м"),
        "SpanUnderMinute": ("<1m", "<1 м"),
        "TrayOpenClaudeCode": ("open Claude Code", "открой Claude Code"),
        "TrayLastSeen": ("as of {0}", "данные на {0}"),
        "TrayUpdateOut": ("Aiko {0} is out", "вышла Aiko {0}"),
        "MenuShowInRing": ("Show {0} in the ring", "Показать в кольце: {0}"),
        "MenuRefresh": ("Refresh limits", "Обновить лимиты"),
        "Settings": ("Settings", "Настройки"),
        "Close": ("Close", "Закрыть"),
        "QuitAiko": ("Quit Aiko", "Выйти из Aiko"),
        "CheckForUpdates": ("Check for updates", "Проверить обновления"),
        "CheckUpdatesToggle": ("Check for updates", "Проверять обновления"),
        "SectionEnvironments": ("ENVIRONMENTS", "СРЕДЫ"),
        "SectionWhereToShow": ("WHERE TO SHOW", "ГДЕ ПОКАЗЫВАТЬ"),
        "SectionStartupAndUpdates": ("STARTUP AND UPDATES", "ЗАПУСК И ОБНОВЛЕНИЯ"),
        "SectionLanguage": ("LANGUAGE", "ЯЗЫК"),
        "DirectMode": ("Direct mode", "Прямой режим"),
        "DirectModeWhat": ("In direct mode, Aiko asks Anthropic for your limits itself. It uses the token Claude Code already keeps on this computer: Aiko reads it before each request and never saves it. Turn it on if you work in the IDE panel or in Claude Desktop, where there's no status line.", "В прямом режиме Aiko сама спрашивает лимиты у Anthropic. Для этого она берёт токен, который Claude Code уже хранит на этом компьютере: читает его перед каждым запросом и нигде не сохраняет. Включай, если работаешь в панели IDE или в Claude Desktop — там строки состояния нет."),
        "DirectModeAsk": ("If it's a work account, ask whoever manages it first.", "Если аккаунт рабочий, сначала спроси того, кто им управляет."),
        "NoEnvironments": ("No environments yet. Aiko looks for them on the first run.", "Сред пока нет. Aiko ищет их при первом запуске."),
        "AccessOk": ("Claude Code sends its limits to Aiko.", "Claude Code передаёт лимиты в Aiko."),
        "AccessMissingOne": ("One environment doesn't send its limits yet.", "Одна среда пока не передаёт лимиты."),
        "AccessMissingMany": ("{0} environments don't send their limits yet.", "Сред, которые пока не передают лимиты: {0}."),
        "AccessCheck": ("Check access", "Проверить доступ"),
        "AccessSetUp": ("Set it up", "Настроить"),
        "AccessNoBridge": ("Aiko can't find its bridge program, so nothing changed.", "Aiko не нашла свою программу-посредник, поэтому ничего не изменилось."),
        "PlaceTray": ("Tray", "Трей"),
        "PlaceIsland": ("Island", "Остров"),
        "PlaceMenuBar": ("Menu bar", "Строка меню"),
        "HideIslandInFullScreen": ("Hide the island in full screen", "Прятать остров в полноэкранных окнах"),
        "StartWithWindows": ("Start with Windows", "Запускать вместе с Windows"),
        "StartAtLogin": ("Start at login", "Запускать при входе"),
        "CheckUpdatesWhat": ("Once a day Aiko asks slayumind.org for the latest version. Downloads come from GitHub.", "Раз в сутки Aiko спрашивает у slayumind.org свежую версию. Сами файлы скачиваются с GitHub."),
        "CheckNow": ("Check now", "Проверить сейчас"),
        "OpenDownloadPage": ("Open download page", "Открыть страницу загрузки"),
        "UpdateAsking": ("Aiko is asking slayumind.org…", "Aiko спрашивает slayumind.org…"),
        "UpdateAvailable": ("Version {0} is available.", "Вышла версия {0}."),
        "UpdateLatest": ("Aiko {0} is the latest version.", "Aiko {0} — самая свежая версия."),
        "UpdateFailed": ("Couldn't check. Try again in a few minutes.", "Проверить не получилось. Попробуй через несколько минут."),
        "LanguageSystem": ("System", "Системный"),
        "LanguageEnglish": ("English", "English"),
        "LanguageRussian": ("Русский", "Русский"),
        "CopyDiagnostics": ("Copy diagnostics", "Скопировать диагностику"),
        "SettingsWindowTitle": ("Aiko settings", "Настройки Aiko"),
        "WizardStepEnvironments": ("Environments", "Среды"),
        "EnvironmentPlainName": ("Main", "Основная"),
        "WizardStepAccess": ("Access to the limits", "Доступ к лимитам"),
        "WizardStepWhere": ("Where to show Aiko", "Где показывать Aiko"),
        "WizardStepCount": ("step {0} of 3", "шаг {0} из 3"),
        "Back": ("Back", "Назад"),
        "NotNow": ("Not now", "Не сейчас"),
        "Next": ("Next", "Дальше"),
        "Allow": ("Allow", "Разрешить"),
        "Finish": ("Finish", "Готово"),
        "WizardFoundOne": ("Aiko found one Claude Code folder. That's enough to start, and you can add a second one later in settings.", "Aiko нашла одну папку Claude Code. Для начала этого хватит, вторую можно добавить позже в настройках."),
        "WizardFoundMany": ("Aiko found these Claude Code folders. Give them names you'll recognise and turn off the ones you don't need.", "Aiko нашла эти папки Claude Code. Дай им понятные тебе имена и выключи лишние."),
        "WizardNothingFound": ("Aiko didn't find Claude Code on this computer. Install it and check again, or choose the folder yourself.", "Aiko не нашла Claude Code на этом компьютере. Установи его и проверь снова или выбери папку сам."),
        "WizardChooseFolder": ("Choose a folder", "Выбрать папку"),
        "WizardCheckAgain": ("Check again", "Проверить снова"),
        "WizardFolderDialogTitle": ("Choose a Claude Code folder", "Выбери папку Claude Code"),
        "WizardAccessExplain": ("Claude Code sends its limits to a status line. Aiko adds one line to the settings file of each environment. If you already have a status line, it keeps working: Aiko runs it and shows what it prints.", "Claude Code отдаёт лимиты строке состояния. Aiko добавит одну строку в файл настроек каждой среды. Если у тебя уже есть своя строка состояния, она продолжит работать: Aiko запустит её и покажет её вывод."),
        "WizardTheLine": ("THE LINE AIKO ADDS", "СТРОКА, КОТОРУЮ ДОБАВИТ AIKO"),
        "WizardTheFiles": ("FILES AIKO CHANGES", "ФАЙЛЫ, КОТОРЫЕ ИЗМЕНЯТСЯ"),
        "WizardBackupNote": ("Aiko keeps a copy of each file next to it. Removing Aiko puts everything back.", "Aiko сохранит копию каждого файла рядом с ним. Удалишь Aiko — всё вернётся как было."),
        "WizardBridgeNotFound": ("Aiko can't find its bridge program.", "Aiko не нашла свою программу-посредник."),
        "WizardUpdatesWhat": ("Once a day, Aiko asks slayumind.org for the latest version and says that one copy of Aiko ran today. The ID it sends changes every day.", "Раз в сутки Aiko спрашивает у slayumind.org свежую версию и сообщает, что одна копия сегодня работала. Идентификатор, который уходит, меняется каждый день."),
        "WizardDoneTray": ("Aiko is down by the clock. Rest the mouse on it to see the card, or click it to keep the card open.", "Aiko внизу, рядом с часами. Наведи мышь, чтобы увидеть карточку, или кликни, чтобы карточка осталась открытой."),
        "WizardDoneMenuBar": ("Aiko is up by the clock. Rest the mouse on it to see the card, or click it to keep the card open.", "Aiko наверху, рядом с часами. Наведи мышь, чтобы увидеть карточку, или щёлкни, чтобы она осталась открытой."),
        "WizardDoneIsland": ("Aiko is at the top of your screen. Drag it anywhere and it sticks to the nearest edge.", "Aiko вверху экрана. Перетащи куда угодно — она прилипнет к ближайшему краю."),
        "WizardDoneOverflow": ("Windows 11 hides new app icons under the arrow next to the clock. Open the arrow and drag Aiko onto the taskbar: under the arrow, the card can't open.", "Windows 11 прячет значки новых приложений под стрелку рядом с часами. Открой её и перетащи Aiko на панель задач: под стрелкой карточка не открывается."),
        "WizardDoneMenuBarRoom": ("A full menu bar hides the icons furthest from the clock. Close a menu bar app if Aiko doesn't fit.", "Если в строке меню нет места, macOS прячет значки, что дальше от часов. Закрой лишнее приложение, если Aiko не помещается."),
        "WizardDoneFirstNumbers": ("The first numbers arrive after your next answer from Claude Code. Until then, the ring is dashed.", "Первые цифры придут после следующего ответа Claude Code. До этого кольцо пунктирное."),
        "WizardDoneNoAccess": ("Aiko can't see your limits yet. You can turn on access in settings at any time.", "Aiko пока не видит лимиты. Доступ можно включить в настройках в любой момент."),
        "SettingsWriteFailed": ("Aiko couldn't write the Claude Code settings file. Try again.", "Aiko не смогла записать файл настроек Claude Code. Попробуй ещё раз."),
        "SettingsBridgeUnknown": ("Aiko can't find its bridge program.", "Aiko не нашла свою программу-посредник."),
        "ChecklistTitle": ("Set up environments", "Настройка сред"),
        "ChecklistCount": ("{0} of {1} done", "готово {0} из {1}"),
        "GroupFirst": ("ENVIRONMENT 1", "СРЕДА 1"),
        "GroupSecond": ("ENVIRONMENT 2", "СРЕДА 2"),
        "GroupShared": ("SHARED", "ОБЩЕЕ"),
        "ItemInstall": ("Install Claude Code", "Установить Claude Code"),
        "ItemAccount": ("Account", "Аккаунт"),
        "ItemSecond": ("Pick or create", "Выбрать или создать"),
        "ItemCommands": ("Launch commands", "Команды запуска"),
        "ItemFolders": ("Project folders", "Папки проектов"),
        "StateInstalled": ("installed", "установлен"),
        "StateWaiting": ("waiting", "ждём"),
        "StateAfterInstall": ("after install", "после установки"),
        "StateAfterSignIn": ("after sign-in", "после входа"),
        "StateAfterSecond": ("after environment 2", "после второй среды"),
        "StateOptional": ("optional", "по желанию"),
        "StateLater": ("later", "позже"),
        "StateConnected": ("connected", "подключён"),
        "StateSignInNeeded": ("sign in needed", "нужно войти"),
        "StateAdded": ("added", "добавлена"),
        "StateNone": ("none", "нет"),
        "StateBound": ("{0} bound", "привязано: {0}"),
        "StateTray": ("tray", "трей"),
        "StateIsland": ("island", "остров"),
        "StateMenuBar": ("menu bar", "строка меню"),
        "Copy": ("Copy", "Скопировать"),
        "Copied": ("Copied", "Скопировано"),
        "Later": ("Later", "Позже"),
        "InstallFound": ("Claude Code is already on this computer. Nothing to do here.", "Claude Code уже есть на этом компьютере. Здесь ничего делать не нужно."),
        "InstallLead": ("Claude Code isn't on this computer yet. Aiko works alongside it, so install it first.", "Claude Code на этом компьютере пока нет. Aiko работает вместе с ним, так что сначала установи его."),
        "InstallHow": ("Open PowerShell, paste the command and press Enter. Aiko notices when it's done.", "Открой PowerShell, вставь команду и нажми Enter. Aiko заметит, когда установка закончится."),
        "InstallHowMac": ("Open Terminal, paste the command and press Enter. Aiko notices when it's done.", "Открой Терминал, вставь команду и нажми Enter. Aiko заметит, когда установка закончится."),
        "WaitInstall": ("Waiting for Claude Code…", "Ждём Claude Code…"),
        "Env1Lead": ("The first environment lives in the .claude folder. The Claude Code panel in VS Code and Claude Desktop use it too.", "Первая среда живёт в папке .claude. С ней же работают панель Claude Code в VS Code и Claude Desktop."),
        "Env1Empty": ("This folder has no account yet.", "В этой папке ещё нет аккаунта."),
        "Env1Found": ("This folder already has an account.", "В этой папке уже есть аккаунт."),
        "SignIn": ("Sign in", "Войти"),
        "LoginHow": ("Aiko opened Claude Code in a new window. Sign in there through your browser. Aiko waits and moves on by itself.", "Aiko открыла Claude Code в новом окне. Войди там через браузер — Aiko дождётся и пойдёт дальше сама."),
        "LoginNever": ("Aiko never sees a password or a token, only that the account is connected.", "Aiko не видит ни пароля, ни токена. Только то, что аккаунт подключён."),
        "WaitLogin": ("Waiting for you to sign in…", "Ждём, пока войдёшь в аккаунт…"),
        "NameInAiko": ("NAME IN AIKO", "ИМЯ В AIKO"),
        "AccountLabel": ("Account", "Аккаунт"),
        "PlanLabel": ("Plan", "План"),
        "Env2LeadFound": ("Aiko found more Claude Code folders. Pick the one for your second environment, or create a new one.", "Aiko нашла ещё папки Claude Code. Выбери ту, что станет второй средой, или создай новую."),
        "Env2LeadNew": ("The second environment is a separate folder with its own account. Name it and Aiko creates the folder.", "Вторая среда — отдельная папка со своим аккаунтом. Назови её, и Aiko создаст папку."),
        "Env2Later": ("One environment is enough to see limits. You can add the second one later in settings.", "Чтобы видеть лимиты, хватит и одной среды. Вторую можно добавить позже в настройках."),
        "CreateNew": ("Create a new one", "Создать новую"),
        "CreateSignIn": ("Create and sign in", "Создать и войти"),
        "NameExample": ("For example, Personal", "Например, Personal"),
        "LastSession": ("last session {0}", "последняя сессия {0}"),
        "FolderIs": ("Folder: {0}", "Папка: {0}"),
        "AccessAdd": ("Add the line", "Добавить строку"),
        "CmdLead": ("A command starts Claude Code in the right environment. It works in PowerShell, cmd and Git Bash.", "Команда запускает Claude Code сразу под нужной средой. Работает в PowerShell, cmd и Git Bash."),
        "CmdLeadMac": ("A command starts Claude Code in the right environment. It works in Terminal and in any zsh shell.", "Команда запускает Claude Code сразу в нужной среде. Работает в Терминале и в любой оболочке zsh."),
        "CmdFor": ("COMMAND · {0}", "КОМАНДА · {0}"),
        "CmdFollows": ("Follows the environment name.", "Меняется вместе с именем среды."),
        "CmdOwn": ("Your own command: renaming doesn't change it.", "Своя команда: переименование её не трогает."),
        "CmdByName": ("Use the name", "По имени среды"),
        "CmdEmpty": ("Enter a command.", "Впиши команду."),
        "CmdTooLong": ("Up to 40 characters.", "Не длиннее 40 знаков."),
        "CmdBadCharacters": ("Only a–z, 0–9, - and _.", "Только a–z, 0–9, - и _."),
        "CmdReserved": ("claude is the name of Claude Code itself.", "claude — имя самого Claude Code."),
        "CmdTaken": ("The other environment has this command.", "Эта команда уже у другой среды."),
        "FnsLead": ("Your PowerShell profile has functions that switch accounts. They run before Aiko's commands and hide them, so they're ticked for removal.", "В профиле PowerShell нашлись функции, которые переключают аккаунт. Они срабатывают раньше команд Aiko и перекрывают их, поэтому отмечены к удалению."),
        "FnRemove": ("Remove {0}", "Убрать {0}"),
        "FnBackup": ("A copy of the profile stays next to it. Removing Aiko brings the functions back.", "Копия профиля останется рядом, а удаление Aiko вернёт функции."),
        "PathNote": ("Aiko puts its folder at the start of PATH so every terminal finds the commands. Restart open terminals and your IDE.", "Aiko поставит свою папку в начало PATH, чтобы команды нашлись в любом терминале. Открытые терминалы и IDE нужно перезапустить."),
        "PathLineMac": ("THE LINE AIKO ADDS TO ~/.ZSHRC", "СТРОКА, КОТОРУЮ AIKO ДОБАВИТ В ~/.ZSHRC"),
        "PathNoteMac": ("macOS puts a folder of your own on PATH only when your shell profile says so, so Aiko adds this line to ~/.zshrc. A copy of the file stays next to it, and removing Aiko takes the line out. Terminals that are already open keep the PATH they started with.", "macOS добавляет твою папку в PATH, только если об этом сказано в профиле оболочки, поэтому Aiko допишет эту строку в ~/.zshrc. Рядом останется копия файла, а при удалении Aiko строка уйдёт. Уже открытые терминалы сохранят прежний PATH."),
        "PathAdd": ("Add to PATH", "Добавить в PATH"),
        "NoCommands": ("No commands", "Без команд"),
        "FoldersLead": ("Bind project folders to {0}. In them and in every folder inside, claude starts {0}.", "Привяжи папки проектов к {0}. В них и во всех вложенных папках claude запустит {0}."),
        "AddProject": ("Add a project folder", "Добавить папку проекта"),
        "PickProject": ("Choose a project folder", "Выбери папку проекта"),
        "NoBinds": ("Nothing is bound yet. You can do it later in settings.", "Пока ничего не привязано. Можно и потом, в настройках."),
        "ExplicitWins": ("A command beats a binding: {0} in a bound folder starts {1}, and Claude Code reminds you of the binding.", "Команда сильнее привязки: {0} в привязанной папке запустит {1}, а Claude Code напомнит о привязке."),
        "Remove": ("Remove", "Убрать"),
        "DoneCommands": ("Commands: {0}. Open a new terminal to use them.", "Команды: {0}. Они заработают в новом окне терминала."),
        "SetUpEnvironments": ("Set up environments", "Настроить среды"),
        "NavGeneral": ("General", "Общее"),
        "NavBound": ("{0} bound", "привязано: {0}"),
        "AddSecondEnvironment": ("Add a second environment", "Добавить вторую среду"),
        "Saved": ("Saved", "Сохранено"),
        "ByDefault": ("default", "по умолчанию"),
        "SectionAccount": ("ACCOUNT", "АККАУНТ"),
        "EmailLabel": ("Email", "Почта"),
        "SignInAgain": ("Sign in again", "Войти заново"),
        "ClaudeOpened": ("Claude Code opened in a new window.", "Claude Code открылся в новом окне."),
        "SectionCommand": ("COMMAND", "КОМАНДА"),
        "CmdShells": ("PowerShell · cmd · Git Bash", "PowerShell · cmd · Git Bash"),
        "CmdShellsMac": ("zsh · bash", "zsh · bash"),
        "CmdKept": ("The command stays the same, since your terminals and scripts know it.", "Команда осталась прежней: терминалы и скрипты её знают."),
        "RenameCommandTo": ("Rename to {0}", "Переименовать в {0}"),
        "NameEmpty": ("Enter a name.", "Впиши имя."),
        "NameTooLong": ("Up to 40 characters.", "Не длиннее 40 знаков."),
        "NameTaken": ("The other environment has this name.", "Это имя уже у другой среды."),
        "SectionBoundFolders": ("BOUND FOLDERS", "ПРИВЯЗАННЫЕ ПАПКИ"),
        "NoneBound": ("No folders.", "Ни одной папки."),
        "RestToo": ("And every other folder: this is the default environment.", "И все остальные папки: это среда по умолчанию."),
        "EditInFolders": ("Change in project folders", "Изменить в папках проектов"),
        "RemoveEnvironment": ("Remove environment", "Удалить среду"),
        "RemoveEnvironmentLine": ("Aiko's line and plugins leave this folder's settings.json, and so do the command {0} and the bindings to {1}.", "Строка и плагины Aiko уйдут из settings.json этой папки. Вместе с ними уйдут команда {0} и привязки к {1}."),
        "MoveToRecycleBin": ("And move the folder to the Recycle Bin", "И переместить папку в корзину"),
        "MoveToTrash": ("And move the folder to the Trash", "И переместить папку в Корзину"),
        "RecycleWhy": ("It holds the Claude Code account, history and memory. You can restore it from the Recycle Bin.", "В ней аккаунт, история и память Claude Code. Вернуть можно из корзины Windows."),
        "TrashWhy": ("It holds the Claude Code account, history and memory. You can put it back from the Trash.", "В ней аккаунт, история и память Claude Code. Вернуть её можно из Корзины."),
        "KeepClaude": ("VS Code and Claude Desktop use the .claude folder, so Aiko never removes it.", "Папку .claude используют VS Code и Claude Desktop, поэтому Aiko её не удаляет."),
        "Cancel": ("Cancel", "Отмена"),
        "RemoveConfirm": ("Remove", "Удалить"),
        "EnvironmentRemoved": ("{0} removed.", "{0} удалена."),
        "EnvironmentRemovedToBin": ("{0} removed. The folder is in the Recycle Bin.", "{0} удалена, папка в корзине."),
        "EnvironmentRemovedToTrash": ("{0} removed. The folder is in the Trash.", "{0} удалена, папка в Корзине."),
        "FoldersTableLead": ("In a folder and in every folder inside, plain claude starts its environment. A command like {0} beats a binding, and Claude Code reminds you of it.", "В папке и во всех вложенных обычный claude запускает её среду. Команда вроде {0} сильнее привязки, но Claude Code напомнит о ней."),
        "FolderColumn": ("FOLDER", "ПАПКА"),
        "EnvironmentColumn": ("ENVIRONMENT", "СРЕДА"),
        "AllOtherFolders": ("All other folders", "Все остальные папки"),
        "AddFolder": ("Add a folder", "Добавить папку"),
        "RemoveBinding": ("Remove binding", "Убрать привязку"),
        "FoldersOneEnvironment": ("Bindings matter once you have two environments. Right now every folder runs in {0}.", "Привязка нужна, когда сред две. Сейчас все папки работают в {0}."),
        "FoldersNeedCommands": ("Bindings work through the launch commands, and those aren't set up yet.", "Привязки работают через команды запуска, а они пока не включены."),
        "SectionAccess": ("ACCESS TO THE LIMITS", "ДОСТУП К ЛИМИТАМ"),
        "SectionDiagnostics": ("DIAGNOSTICS", "ДИАГНОСТИКА"),
        "DiagnosticsWhat": ("Version and settings for a bug report. No tokens and no email addresses.", "Версия и настройки для сообщения об ошибке. Токенов и почты там нет."),
        "DiagnosticsCopied": ("Diagnostics copied.", "Диагностика скопирована."),
        "SetupDone": ("Environments are set up.", "Среды настроены."),
        "Restart": ("Start over", "Начать заново"),
        "RestartWhat": ("Removes the environments from Aiko and opens the checklist right here.", "Убирает среды из Aiko и открывает чек-лист здесь же."),
        "RestartLine": ("Aiko removes the environments, its lines and plugins in settings.json, the commands {0}, the folder bindings and its folder in PATH, and turns the PowerShell profile functions back on. Accounts and history stay in the folders.", "Из Aiko уйдут среды, строки и плагины Aiko в settings.json, команды {0}, привязки папок и папка Aiko в PATH, а функции в профиле PowerShell вернутся. Аккаунты и история в папках останутся."),
        "RestartLineMac": ("Aiko removes the environments, its lines and plugins in settings.json, the commands {0}, the folder bindings and its line in ~/.zshrc. Accounts and history stay in the folders.", "Из Aiko уйдут среды, строки и плагины Aiko в settings.json, команды {0}, привязки папок и строка Aiko в ~/.zshrc. Аккаунты и история в папках останутся."),
        "RestartBin": ("And move {0} to the Recycle Bin", "И переместить {0} в корзину"),
        "RestartTrash": ("And move {0} to the Trash", "И переместить {0} в Корзину"),
        "RestartKeep": (".claude stays either way.", ".claude остаётся в любом случае."),
        "OpenClaudeCode": ("Open Claude Code", "Открыть Claude Code"),
        "CardSignInNote": ("Sign in so Aiko can see the limits of this environment.", "Войди, чтобы Aiko видела лимиты этой среды."),
        "StateWorkingNow": ("working now", "сейчас работает"),
        "NavPersonality": ("Personality", "Личность"),
        "NavPersonaOn": ("on: {0}", "включена: {0}"),
        "NavPersonaOff": ("off", "выключена"),
        "NavPrivacy": ("Privacy", "Приватность"),
        "NavPrivacyStatsOn": ("statistics: on", "статистика: включена"),
        "NavPrivacyStatsOff": ("statistics: off", "статистика: выключена"),
        "SectionConnection": ("CONNECTION TO SLAYUMIND.ORG", "СВЯЗЬ С SLAYUMIND.ORG"),
        "SendStatsToggle": ("Send anonymous statistics", "Отправлять анонимную статистику"),
        "SendStatsWhat": ("The same request says that one copy ran today. The author of Aiko sees numbers and nothing else.", "Тем же запросом Aiko сообщает, что одна копия сегодня работала. Автор Aiko видит только числа."),
        "SectionWhatIsSent": ("WHAT IS SENT", "ЧТО ОТПРАВЛЯЕТСЯ"),
        "SentVersion": ("the version of Aiko: whether updates arrive", "версия Aiko: доходят ли обновления"),
        "SentWindows": ("the version of Windows: what to test first", "версия Windows: что проверять первым"),
        "SentMacOS": ("the version of macOS: what to test first", "версия macOS: что проверять первым"),
        "SentDayId": ("an ID that changes every day", "ID, который меняется каждый день"),
        "SentWeekFlag": ("the first run this week", "первый запуск на этой неделе"),
        "SentMonthFlag": ("the first run this month", "первый запуск в этом месяце"),
        "SentPersona": ("the personality is on in at least one environment", "личность включена хотя бы в одной среде"),
        "NothingSentYet": ("None of this is sent right now.", "Сейчас не отправляется ничего из этого."),
        "SectionNeverSent": ("WHAT NEVER LEAVES", "ЧТО НЕ ОТПРАВЛЯЕТСЯ НИКОГДА"),
        "NeverSentWhat": ("Your name, email, project folders, limits, token and session texts. The site doesn't read or store the IP address.", "Имя, почта, папки проектов, лимиты, токен и тексты сессий. IP-адрес сайт не читает и не хранит."),
        "StatsKeptFor": ("The rows are deleted after 90 days.", "Строки удаляются через 90 дней."),
        "ResetInstallId": ("Reset ID", "Сбросить ID"),
        "ResetInstallIdWhat": ("Aiko makes a new value, and the days before it can't be linked to it.", "Aiko придумает новое значение, и прежние сутки с ним не свяжутся."),
        "ResetInstallIdDone": ("The ID is new.", "ID теперь новый."),
        "OpenPrivacyDoc": ("Open PRIVACY.md", "Открыть PRIVACY.md"),
        "PrivacyLinkFromGeneral": ("What Aiko sends and to whom — Privacy.", "Что Aiko отправляет и кому — раздел «Приватность»."),
        "WizardStats": ("Anonymous statistics", "Анонимная статистика"),
        "WizardStatsWhat": ("The author of Aiko doesn't know how many people use it. Once a day Aiko can say that one copy ran and send six values. Here they are, all of them.", "Автор Aiko не знает, сколько людей ею пользуется. Раз в сутки Aiko может сообщить, что одна копия работала, и отправить шесть значений — вот они целиком."),
        "WizardStatsLater": ("You can change this later in settings, under Privacy.", "Решение меняется в настройках, в разделе «Приватность»."),
        "StatsDecline": ("Don't send", "Не отправлять"),
        "StatsAccept": ("Send", "Отправлять"),
        "StatsStateOn": ("on", "включена"),
        "StatsStateOff": ("off", "выключена"),
        "StatsStateUnset": ("not chosen", "не выбрано"),
        "SectionWhereAikoTalks": ("WHERE AIKO TALKS", "ГДЕ AIKO ГОВОРИТ"),
        "PersonaNewSessions": ("Works in new sessions. Open sessions finish the way they started.", "Действует в новых сессиях. Открытые сессии доработают как были."),
        "PersonaOwnStyle": ("While the personality is on, your style {0} doesn't work. Turn it off and it comes back.", "Пока личность включена, твой стиль {0} не работает. Выключишь — вернётся."),
        "SectionFace": ("FACE", "ЛИЦО"),
        "FaceChibi": ("Chibi", "Чиби"),
        "FaceEmoji": ("Emoji", "Смайлик"),
        "FaceWhere": ("This is how Aiko looks in the tray, on the island and in windows.", "Так Aiko выглядит в трее, на острове и в окнах."),
        "FaceWhereMac": ("That's how Aiko looks in the menu bar, on the island and in the windows.", "Так Aiko выглядит в строке меню, на острове и в окнах."),
        "SectionTemperament": ("TEMPERAMENT", "ТЕМПЕРАМЕНТ"),
        "TemperamentQuiet": ("Quiet", "Тихий"),
        "TemperamentNormal": ("Normal", "Обычный"),
        "TemperamentBright": ("Bright", "Яркий"),
        "TemperamentQuietAbout": ("Almost no character: one warm line at the end of an answer.", "Почти без характера: одна тёплая фраза в конце ответа."),
        "TemperamentNormalAbout": ("A short interjection at the start, a quick verdict at the end. Japanese words now and then.", "Короткое междометие в начале, оценка в конце. Японские слова — изредка."),
        "TemperamentBrightAbout": ("An interjection in every answer, game metaphors, Japanese words more often.", "Междометие в каждом ответе, игровые метафоры, японские слова чаще."),
        "TemperamentMusouAbout": ("Everything at full. In code, commits, files and errors she still stays quiet.", "Всё на максимум. В коде, коммитах, файлах и ошибках она всё равно молчит."),
        "SectionSampleReply": ("HOW SHE'LL ANSWER", "ТАК ОНА ОТВЕТИТ"),
        "SampleQuestion": ("Why does the reset countdown show 0 minutes?", "Почему отсчёт до сброса показывает 0 минут?"),
        "SampleBody": ("The limits test passed by luck: rounding went down, so a reset at 14:59:30 showed as “in 0 min”. I fixed ResetCountdown and added a test for the edge.", "Тест на разбор лимитов проходил случайно: округление шло вниз, и сброс в 14:59:30 показывался как «через 0 мин». Поправила ResetCountdown и добавила тест на край."),
        "SampleBodyBright": ("The limits test passed by luck: rounding went down, so a reset at 14:59:30 hid behind “in 0 min”, like a mimic chest in Dark Souls. I fixed ResetCountdown and added a test for the edge.", "Тест на разбор лимитов проходил случайно: округление шло вниз, и сброс в 14:59:30 прятался за «через 0 мин», как мимик-сундук в Dark Souls. Поправила ResetCountdown и добавила тест на край."),
        "SampleBodyMusouTail": (" Parried it, like in Elden Ring.", " Парировала его, как в Elden Ring."),
        "SampleOpenNormal": ("えへへ, got it.", "えへへ, поймала."),
        "SampleOpenBright": ("やった, got it!", "やった, поймала!"),
        "SampleOpenMusou": ("やった〜! すごい, what a sneaky one!", "やった〜! すごい, какой хитрый!"),
        "SampleCloseQuiet": ("A rare bug. It was nice to find.", "Редкий баг, приятно было найти."),
        "SampleCloseNormal": ("In my own game I'd have hunted a bug like this for half a day.", "Такой баг я бы и в своей игре полдня искала."),
        "SampleCloseBright": ("お疲れ様!", "お疲れ様!"),
        "SampleCloseMusou": ("よし、行くぞ! On to the next one (^_^)", "よし、行くぞ! Дальше (^_^)"),
        "SampleCommit": ("The commit for this fix has no character:", "Коммит к этой правке — без характера:"),
        "SectionSkills": ("SKILLS · {0}", "НАВЫКИ · {0}"),
        "SkillsWork": ("Skills work in environments where the personality is on.", "Навыки работают в средах, где включена личность."),
        "SkillsSwitch": ("Aiko's skills", "Навыки Aiko"),
        "SkillsNeedPersona": ("Turn the personality on in at least one environment, and the skills start working.", "Включи личность хотя бы в одной среде — тогда навыки заработают."),
        "SkillDomainProjects": ("PROJECT MANAGEMENT", "УПРАВЛЕНИЕ ПРОЕКТАМИ"),
        "SkillDomainGames": ("GAME DEVELOPMENT", "РАЗРАБОТКА ИГР"),
        "SkillAikoCopy": ("UI text, READMEs and release notes without stiff wording or AI tells. RU and EN.", "Интерфейс, README и релизы без канцелярита и штампов ИИ. RU и EN."),
        "SkillAikoReleaseGate": ("Before a release: what can't be undone, how to roll back, whether the docs match the code.", "Перед релизом: что необратимо, как откатить, совпадают ли документы с кодом."),
        "SkillAikoDocsHygiene": ("One fact in one place, a decision log, contradictions found.", "Факт живёт в одном месте, журнал решений, поиск противоречий."),
        "SkillAikoPlaytest": ("Checks a game with numbers: the same test scene before and after a change.", "Проверяет игру цифрами: одна и та же тестовая сцена до и после правки."),
        "SkillAikoBlenderToUnity": ("A model from Blender to Unity: axes, normals, export and import.", "Модель из Blender в Unity: оси, нормали, экспорт и импорт."),
        "SkillAikoTexturing": ("Ready files for textures: a size that fits the camera, and sheets to paint over.", "Готовые файлы для текстур: размер под камеру и шаблоны листов для отрисовки."),
        "SkillAikoGlbForWeb": ("A .glb for the web: size, axes, compression, a blank view where the model should be.", "Файл .glb для веба: размер, оси, сжатие, пустое окно вместо модели."),
        "SkillAikoPalette": ("Fit a color to the palette in OKLCH with the smallest change.", "Подогнать цвет под палитру в OKLCH с самой маленькой правкой."),
        "SkillAikoGamedesignResearch": ("30–40 games with the mechanic you need: an overview with screenshots, and interactive demos on request.", "30–40 игр с нужной механикой: обзор со скриншотами и интерактивные стенды по запросу."),
        "SkillAikoCalendar": ("Plan the day in Google Calendar, find free time, add events after your yes.", "План дня по Google Calendar, свободное время, события — после твоего «да»."),
        "SkillAikoDrive": ("Documents from Google Drive into the work, and files from the project onto Drive.", "Документы с Google Drive в работу и файлы проекта — на Drive."),
        "ItemMeetAiko": ("Meet Aiko", "Познакомься с Aiko"),
        "MeetAikoLead": ("Aiko can talk in your Claude Code sessions in her own voice, and she brings eleven skills. In code and commits she stays quiet.", "Aiko может говорить в твоих сессиях Claude Code своим голосом и приносит одиннадцать навыков. В коде и коммитах она молчит."),
        "MeetAikoSettings": ("The temperament, the face and the skills are in settings, under Personality. It works in new sessions.", "Темперамент, лицо и навыки — в настройках, в разделе «Личность». Действует в новых сессиях."),
        "MeetAikoTurnOn": ("Turn on in {0}", "Включить в среде {0}"),
        "StateOn": ("on", "включена"),
    ]

    /// Session · 5 hours
    public static var cardSession: String { Strings.get("CardSession") }

    /// Week
    public static var cardWeek: String { Strings.get("CardWeek") }

    /// {0} · week
    public static var cardModelWeek: String { Strings.get("CardModelWeek") }

    /// {0}% used
    public static var cardPercentUsed: String { Strings.get("CardPercentUsed") }

    /// resets in {0}
    public static var cardResets: String { Strings.get("CardResets") }

    /// just reset
    public static var cardJustReset: String { Strings.get("CardJustReset") }

    /// lasts until reset
    public static var cardLastsUntilReset: String { Strings.get("CardLastsUntilReset") }

    /// ~{0} at this pace
    public static var cardAtThisPace: String { Strings.get("CardAtThisPace") }

    /// running low
    public static var cardToneCaution: String { Strings.get("CardToneCaution") }

    /// almost gone
    public static var cardToneCritical: String { Strings.get("CardToneCritical") }

    /// no data yet
    public static var cardNoDataYet: String { Strings.get("CardNoDataYet") }

    /// as of {0}
    public static var cardLastSeen: String { Strings.get("CardLastSeen") }

    /// updated {0}
    public static var cardUpdated: String { Strings.get("CardUpdated") }

    /// Open Claude Code. Limits show up after its first answer.
    public static var cardNoDataNote: String { Strings.get("CardNoDataNote") }

    /// Aiko can't see the limits of this environment yet. You can turn on access in settings.
    public static var cardNoAccessNote: String { Strings.get("CardNoAccessNote") }

    /// {0}d {1}h
    public static var spanDaysHours: String { Strings.get("SpanDaysHours") }

    /// {0}h {1}m
    public static var spanHoursMinutes: String { Strings.get("SpanHoursMinutes") }

    /// {0}m
    public static var spanMinutes: String { Strings.get("SpanMinutes") }

    /// <1m
    public static var spanUnderMinute: String { Strings.get("SpanUnderMinute") }

    /// open Claude Code
    public static var trayOpenClaudeCode: String { Strings.get("TrayOpenClaudeCode") }

    /// as of {0}
    public static var trayLastSeen: String { Strings.get("TrayLastSeen") }

    /// Aiko {0} is out
    public static var trayUpdateOut: String { Strings.get("TrayUpdateOut") }

    /// Show {0} in the ring
    public static var menuShowInRing: String { Strings.get("MenuShowInRing") }

    /// Refresh limits
    public static var menuRefresh: String { Strings.get("MenuRefresh") }

    /// Settings
    public static var settings: String { Strings.get("Settings") }

    /// Close
    public static var close: String { Strings.get("Close") }

    /// Quit Aiko
    public static var quitAiko: String { Strings.get("QuitAiko") }

    /// Check for updates
    public static var checkForUpdates: String { Strings.get("CheckForUpdates") }

    /// Check for updates
    public static var checkUpdatesToggle: String { Strings.get("CheckUpdatesToggle") }

    /// ENVIRONMENTS
    public static var sectionEnvironments: String { Strings.get("SectionEnvironments") }

    /// WHERE TO SHOW
    public static var sectionWhereToShow: String { Strings.get("SectionWhereToShow") }

    /// STARTUP AND UPDATES
    public static var sectionStartupAndUpdates: String { Strings.get("SectionStartupAndUpdates") }

    /// LANGUAGE
    public static var sectionLanguage: String { Strings.get("SectionLanguage") }

    /// Direct mode
    public static var directMode: String { Strings.get("DirectMode") }

    /// In direct mode, Aiko asks Anthropic for your limits itself. It uses the token Claude Code...
    public static var directModeWhat: String { Strings.get("DirectModeWhat") }

    /// If it's a work account, ask whoever manages it first.
    public static var directModeAsk: String { Strings.get("DirectModeAsk") }

    /// No environments yet. Aiko looks for them on the first run.
    public static var noEnvironments: String { Strings.get("NoEnvironments") }

    /// Claude Code sends its limits to Aiko.
    public static var accessOk: String { Strings.get("AccessOk") }

    /// One environment doesn't send its limits yet.
    public static var accessMissingOne: String { Strings.get("AccessMissingOne") }

    /// {0} environments don't send their limits yet.
    public static var accessMissingMany: String { Strings.get("AccessMissingMany") }

    /// Check access
    public static var accessCheck: String { Strings.get("AccessCheck") }

    /// Set it up
    public static var accessSetUp: String { Strings.get("AccessSetUp") }

    /// Aiko can't find its bridge program, so nothing changed.
    public static var accessNoBridge: String { Strings.get("AccessNoBridge") }

    /// Tray
    public static var placeTray: String { Strings.get("PlaceTray") }

    /// Island
    public static var placeIsland: String { Strings.get("PlaceIsland") }

    /// Menu bar
    public static var placeMenuBar: String { Strings.get("PlaceMenuBar") }

    /// Hide the island in full screen
    public static var hideIslandInFullScreen: String { Strings.get("HideIslandInFullScreen") }

    /// Start with Windows
    public static var startWithWindows: String { Strings.get("StartWithWindows") }

    /// Start at login
    public static var startAtLogin: String { Strings.get("StartAtLogin") }

    /// Once a day Aiko asks slayumind.org for the latest version. Downloads come from GitHub.
    public static var checkUpdatesWhat: String { Strings.get("CheckUpdatesWhat") }

    /// Check now
    public static var checkNow: String { Strings.get("CheckNow") }

    /// Open download page
    public static var openDownloadPage: String { Strings.get("OpenDownloadPage") }

    /// Aiko is asking slayumind.org…
    public static var updateAsking: String { Strings.get("UpdateAsking") }

    /// Version {0} is available.
    public static var updateAvailable: String { Strings.get("UpdateAvailable") }

    /// Aiko {0} is the latest version.
    public static var updateLatest: String { Strings.get("UpdateLatest") }

    /// Couldn't check. Try again in a few minutes.
    public static var updateFailed: String { Strings.get("UpdateFailed") }

    /// System
    public static var languageSystem: String { Strings.get("LanguageSystem") }

    /// English
    public static var languageEnglish: String { Strings.get("LanguageEnglish") }

    /// Русский
    public static var languageRussian: String { Strings.get("LanguageRussian") }

    /// Copy diagnostics
    public static var copyDiagnostics: String { Strings.get("CopyDiagnostics") }

    /// Aiko settings
    public static var settingsWindowTitle: String { Strings.get("SettingsWindowTitle") }

    /// Environments
    public static var wizardStepEnvironments: String { Strings.get("WizardStepEnvironments") }

    /// Main
    public static var environmentPlainName: String { Strings.get("EnvironmentPlainName") }

    /// Access to the limits
    public static var wizardStepAccess: String { Strings.get("WizardStepAccess") }

    /// Where to show Aiko
    public static var wizardStepWhere: String { Strings.get("WizardStepWhere") }

    /// step {0} of 3
    public static var wizardStepCount: String { Strings.get("WizardStepCount") }

    /// Back
    public static var back: String { Strings.get("Back") }

    /// Not now
    public static var notNow: String { Strings.get("NotNow") }

    /// Next
    public static var next: String { Strings.get("Next") }

    /// Allow
    public static var allow: String { Strings.get("Allow") }

    /// Finish
    public static var finish: String { Strings.get("Finish") }

    /// Aiko found one Claude Code folder. That's enough to start, and you can add a second one l...
    public static var wizardFoundOne: String { Strings.get("WizardFoundOne") }

    /// Aiko found these Claude Code folders. Give them names you'll recognise and turn off the o...
    public static var wizardFoundMany: String { Strings.get("WizardFoundMany") }

    /// Aiko didn't find Claude Code on this computer. Install it and check again, or choose the ...
    public static var wizardNothingFound: String { Strings.get("WizardNothingFound") }

    /// Choose a folder
    public static var wizardChooseFolder: String { Strings.get("WizardChooseFolder") }

    /// Check again
    public static var wizardCheckAgain: String { Strings.get("WizardCheckAgain") }

    /// Choose a Claude Code folder
    public static var wizardFolderDialogTitle: String { Strings.get("WizardFolderDialogTitle") }

    /// Claude Code sends its limits to a status line. Aiko adds one line to the settings file of...
    public static var wizardAccessExplain: String { Strings.get("WizardAccessExplain") }

    /// THE LINE AIKO ADDS
    public static var wizardTheLine: String { Strings.get("WizardTheLine") }

    /// FILES AIKO CHANGES
    public static var wizardTheFiles: String { Strings.get("WizardTheFiles") }

    /// Aiko keeps a copy of each file next to it. Removing Aiko puts everything back.
    public static var wizardBackupNote: String { Strings.get("WizardBackupNote") }

    /// Aiko can't find its bridge program.
    public static var wizardBridgeNotFound: String { Strings.get("WizardBridgeNotFound") }

    /// Once a day, Aiko asks slayumind.org for the latest version and says that one copy of Aiko...
    public static var wizardUpdatesWhat: String { Strings.get("WizardUpdatesWhat") }

    /// Aiko is down by the clock. Rest the mouse on it to see the card, or click it to keep the ...
    public static var wizardDoneTray: String { Strings.get("WizardDoneTray") }

    /// Aiko is up by the clock. Rest the mouse on it to see the card, or click it to keep the ca...
    public static var wizardDoneMenuBar: String { Strings.get("WizardDoneMenuBar") }

    /// Aiko is at the top of your screen. Drag it anywhere and it sticks to the nearest edge.
    public static var wizardDoneIsland: String { Strings.get("WizardDoneIsland") }

    /// Windows 11 hides new app icons under the arrow next to the clock. Open the arrow and drag...
    public static var wizardDoneOverflow: String { Strings.get("WizardDoneOverflow") }

    /// A full menu bar hides the icons furthest from the clock. Close a menu bar app if Aiko doe...
    public static var wizardDoneMenuBarRoom: String { Strings.get("WizardDoneMenuBarRoom") }

    /// The first numbers arrive after your next answer from Claude Code. Until then, the ring is...
    public static var wizardDoneFirstNumbers: String { Strings.get("WizardDoneFirstNumbers") }

    /// Aiko can't see your limits yet. You can turn on access in settings at any time.
    public static var wizardDoneNoAccess: String { Strings.get("WizardDoneNoAccess") }

    /// Aiko couldn't write the Claude Code settings file. Try again.
    public static var settingsWriteFailed: String { Strings.get("SettingsWriteFailed") }

    /// Aiko can't find its bridge program.
    public static var settingsBridgeUnknown: String { Strings.get("SettingsBridgeUnknown") }

    /// Set up environments
    public static var checklistTitle: String { Strings.get("ChecklistTitle") }

    /// {0} of {1} done
    public static var checklistCount: String { Strings.get("ChecklistCount") }

    /// ENVIRONMENT 1
    public static var groupFirst: String { Strings.get("GroupFirst") }

    /// ENVIRONMENT 2
    public static var groupSecond: String { Strings.get("GroupSecond") }

    /// SHARED
    public static var groupShared: String { Strings.get("GroupShared") }

    /// Install Claude Code
    public static var itemInstall: String { Strings.get("ItemInstall") }

    /// Account
    public static var itemAccount: String { Strings.get("ItemAccount") }

    /// Pick or create
    public static var itemSecond: String { Strings.get("ItemSecond") }

    /// Launch commands
    public static var itemCommands: String { Strings.get("ItemCommands") }

    /// Project folders
    public static var itemFolders: String { Strings.get("ItemFolders") }

    /// installed
    public static var stateInstalled: String { Strings.get("StateInstalled") }

    /// waiting
    public static var stateWaiting: String { Strings.get("StateWaiting") }

    /// after install
    public static var stateAfterInstall: String { Strings.get("StateAfterInstall") }

    /// after sign-in
    public static var stateAfterSignIn: String { Strings.get("StateAfterSignIn") }

    /// after environment 2
    public static var stateAfterSecond: String { Strings.get("StateAfterSecond") }

    /// optional
    public static var stateOptional: String { Strings.get("StateOptional") }

    /// later
    public static var stateLater: String { Strings.get("StateLater") }

    /// connected
    public static var stateConnected: String { Strings.get("StateConnected") }

    /// sign in needed
    public static var stateSignInNeeded: String { Strings.get("StateSignInNeeded") }

    /// added
    public static var stateAdded: String { Strings.get("StateAdded") }

    /// none
    public static var stateNone: String { Strings.get("StateNone") }

    /// {0} bound
    public static var stateBound: String { Strings.get("StateBound") }

    /// tray
    public static var stateTray: String { Strings.get("StateTray") }

    /// island
    public static var stateIsland: String { Strings.get("StateIsland") }

    /// menu bar
    public static var stateMenuBar: String { Strings.get("StateMenuBar") }

    /// Copy
    public static var copy: String { Strings.get("Copy") }

    /// Copied
    public static var copied: String { Strings.get("Copied") }

    /// Later
    public static var later: String { Strings.get("Later") }

    /// Claude Code is already on this computer. Nothing to do here.
    public static var installFound: String { Strings.get("InstallFound") }

    /// Claude Code isn't on this computer yet. Aiko works alongside it, so install it first.
    public static var installLead: String { Strings.get("InstallLead") }

    /// Open PowerShell, paste the command and press Enter. Aiko notices when it's done.
    public static var installHow: String { Strings.get("InstallHow") }

    /// Open Terminal, paste the command and press Enter. Aiko notices when it's done.
    public static var installHowMac: String { Strings.get("InstallHowMac") }

    /// Waiting for Claude Code…
    public static var waitInstall: String { Strings.get("WaitInstall") }

    /// The first environment lives in the .claude folder. The Claude Code panel in VS Code and C...
    public static var env1Lead: String { Strings.get("Env1Lead") }

    /// This folder has no account yet.
    public static var env1Empty: String { Strings.get("Env1Empty") }

    /// This folder already has an account.
    public static var env1Found: String { Strings.get("Env1Found") }

    /// Sign in
    public static var signIn: String { Strings.get("SignIn") }

    /// Aiko opened Claude Code in a new window. Sign in there through your browser. Aiko waits a...
    public static var loginHow: String { Strings.get("LoginHow") }

    /// Aiko never sees a password or a token, only that the account is connected.
    public static var loginNever: String { Strings.get("LoginNever") }

    /// Waiting for you to sign in…
    public static var waitLogin: String { Strings.get("WaitLogin") }

    /// NAME IN AIKO
    public static var nameInAiko: String { Strings.get("NameInAiko") }

    /// Account
    public static var accountLabel: String { Strings.get("AccountLabel") }

    /// Plan
    public static var planLabel: String { Strings.get("PlanLabel") }

    /// Aiko found more Claude Code folders. Pick the one for your second environment, or create ...
    public static var env2LeadFound: String { Strings.get("Env2LeadFound") }

    /// The second environment is a separate folder with its own account. Name it and Aiko create...
    public static var env2LeadNew: String { Strings.get("Env2LeadNew") }

    /// One environment is enough to see limits. You can add the second one later in settings.
    public static var env2Later: String { Strings.get("Env2Later") }

    /// Create a new one
    public static var createNew: String { Strings.get("CreateNew") }

    /// Create and sign in
    public static var createSignIn: String { Strings.get("CreateSignIn") }

    /// For example, Personal
    public static var nameExample: String { Strings.get("NameExample") }

    /// last session {0}
    public static var lastSession: String { Strings.get("LastSession") }

    /// Folder: {0}
    public static var folderIs: String { Strings.get("FolderIs") }

    /// Add the line
    public static var accessAdd: String { Strings.get("AccessAdd") }

    /// A command starts Claude Code in the right environment. It works in PowerShell, cmd and Gi...
    public static var cmdLead: String { Strings.get("CmdLead") }

    /// A command starts Claude Code in the right environment. It works in Terminal and in any zs...
    public static var cmdLeadMac: String { Strings.get("CmdLeadMac") }

    /// COMMAND · {0}
    public static var cmdFor: String { Strings.get("CmdFor") }

    /// Follows the environment name.
    public static var cmdFollows: String { Strings.get("CmdFollows") }

    /// Your own command: renaming doesn't change it.
    public static var cmdOwn: String { Strings.get("CmdOwn") }

    /// Use the name
    public static var cmdByName: String { Strings.get("CmdByName") }

    /// Enter a command.
    public static var cmdEmpty: String { Strings.get("CmdEmpty") }

    /// Up to 40 characters.
    public static var cmdTooLong: String { Strings.get("CmdTooLong") }

    /// Only a–z, 0–9, - and _.
    public static var cmdBadCharacters: String { Strings.get("CmdBadCharacters") }

    /// claude is the name of Claude Code itself.
    public static var cmdReserved: String { Strings.get("CmdReserved") }

    /// The other environment has this command.
    public static var cmdTaken: String { Strings.get("CmdTaken") }

    /// Your PowerShell profile has functions that switch accounts. They run before Aiko's comman...
    public static var fnsLead: String { Strings.get("FnsLead") }

    /// Remove {0}
    public static var fnRemove: String { Strings.get("FnRemove") }

    /// A copy of the profile stays next to it. Removing Aiko brings the functions back.
    public static var fnBackup: String { Strings.get("FnBackup") }

    /// Aiko puts its folder at the start of PATH so every terminal finds the commands. Restart o...
    public static var pathNote: String { Strings.get("PathNote") }

    /// THE LINE AIKO ADDS TO ~/.ZSHRC
    public static var pathLineMac: String { Strings.get("PathLineMac") }

    /// macOS puts a folder of your own on PATH only when your shell profile says so, so Aiko add...
    public static var pathNoteMac: String { Strings.get("PathNoteMac") }

    /// Add to PATH
    public static var pathAdd: String { Strings.get("PathAdd") }

    /// No commands
    public static var noCommands: String { Strings.get("NoCommands") }

    /// Bind project folders to {0}. In them and in every folder inside, claude starts {0}.
    public static var foldersLead: String { Strings.get("FoldersLead") }

    /// Add a project folder
    public static var addProject: String { Strings.get("AddProject") }

    /// Choose a project folder
    public static var pickProject: String { Strings.get("PickProject") }

    /// Nothing is bound yet. You can do it later in settings.
    public static var noBinds: String { Strings.get("NoBinds") }

    /// A command beats a binding: {0} in a bound folder starts {1}, and Claude Code reminds you ...
    public static var explicitWins: String { Strings.get("ExplicitWins") }

    /// Remove
    public static var remove: String { Strings.get("Remove") }

    /// Commands: {0}. Open a new terminal to use them.
    public static var doneCommands: String { Strings.get("DoneCommands") }

    /// Set up environments
    public static var setUpEnvironments: String { Strings.get("SetUpEnvironments") }

    /// General
    public static var navGeneral: String { Strings.get("NavGeneral") }

    /// {0} bound
    public static var navBound: String { Strings.get("NavBound") }

    /// Add a second environment
    public static var addSecondEnvironment: String { Strings.get("AddSecondEnvironment") }

    /// Saved
    public static var saved: String { Strings.get("Saved") }

    /// default
    public static var byDefault: String { Strings.get("ByDefault") }

    /// ACCOUNT
    public static var sectionAccount: String { Strings.get("SectionAccount") }

    /// Email
    public static var emailLabel: String { Strings.get("EmailLabel") }

    /// Sign in again
    public static var signInAgain: String { Strings.get("SignInAgain") }

    /// Claude Code opened in a new window.
    public static var claudeOpened: String { Strings.get("ClaudeOpened") }

    /// COMMAND
    public static var sectionCommand: String { Strings.get("SectionCommand") }

    /// PowerShell · cmd · Git Bash
    public static var cmdShells: String { Strings.get("CmdShells") }

    /// zsh · bash
    public static var cmdShellsMac: String { Strings.get("CmdShellsMac") }

    /// The command stays the same, since your terminals and scripts know it.
    public static var cmdKept: String { Strings.get("CmdKept") }

    /// Rename to {0}
    public static var renameCommandTo: String { Strings.get("RenameCommandTo") }

    /// Enter a name.
    public static var nameEmpty: String { Strings.get("NameEmpty") }

    /// Up to 40 characters.
    public static var nameTooLong: String { Strings.get("NameTooLong") }

    /// The other environment has this name.
    public static var nameTaken: String { Strings.get("NameTaken") }

    /// BOUND FOLDERS
    public static var sectionBoundFolders: String { Strings.get("SectionBoundFolders") }

    /// No folders.
    public static var noneBound: String { Strings.get("NoneBound") }

    /// And every other folder: this is the default environment.
    public static var restToo: String { Strings.get("RestToo") }

    /// Change in project folders
    public static var editInFolders: String { Strings.get("EditInFolders") }

    /// Remove environment
    public static var removeEnvironment: String { Strings.get("RemoveEnvironment") }

    /// Aiko's line and plugins leave this folder's settings.json, and so do the command {0} and ...
    public static var removeEnvironmentLine: String { Strings.get("RemoveEnvironmentLine") }

    /// And move the folder to the Recycle Bin
    public static var moveToRecycleBin: String { Strings.get("MoveToRecycleBin") }

    /// And move the folder to the Trash
    public static var moveToTrash: String { Strings.get("MoveToTrash") }

    /// It holds the Claude Code account, history and memory. You can restore it from the Recycle...
    public static var recycleWhy: String { Strings.get("RecycleWhy") }

    /// It holds the Claude Code account, history and memory. You can put it back from the Trash.
    public static var trashWhy: String { Strings.get("TrashWhy") }

    /// VS Code and Claude Desktop use the .claude folder, so Aiko never removes it.
    public static var keepClaude: String { Strings.get("KeepClaude") }

    /// Cancel
    public static var cancel: String { Strings.get("Cancel") }

    /// Remove
    public static var removeConfirm: String { Strings.get("RemoveConfirm") }

    /// {0} removed.
    public static var environmentRemoved: String { Strings.get("EnvironmentRemoved") }

    /// {0} removed. The folder is in the Recycle Bin.
    public static var environmentRemovedToBin: String { Strings.get("EnvironmentRemovedToBin") }

    /// {0} removed. The folder is in the Trash.
    public static var environmentRemovedToTrash: String { Strings.get("EnvironmentRemovedToTrash") }

    /// In a folder and in every folder inside, plain claude starts its environment. A command li...
    public static var foldersTableLead: String { Strings.get("FoldersTableLead") }

    /// FOLDER
    public static var folderColumn: String { Strings.get("FolderColumn") }

    /// ENVIRONMENT
    public static var environmentColumn: String { Strings.get("EnvironmentColumn") }

    /// All other folders
    public static var allOtherFolders: String { Strings.get("AllOtherFolders") }

    /// Add a folder
    public static var addFolder: String { Strings.get("AddFolder") }

    /// Remove binding
    public static var removeBinding: String { Strings.get("RemoveBinding") }

    /// Bindings matter once you have two environments. Right now every folder runs in {0}.
    public static var foldersOneEnvironment: String { Strings.get("FoldersOneEnvironment") }

    /// Bindings work through the launch commands, and those aren't set up yet.
    public static var foldersNeedCommands: String { Strings.get("FoldersNeedCommands") }

    /// ACCESS TO THE LIMITS
    public static var sectionAccess: String { Strings.get("SectionAccess") }

    /// DIAGNOSTICS
    public static var sectionDiagnostics: String { Strings.get("SectionDiagnostics") }

    /// Version and settings for a bug report. No tokens and no email addresses.
    public static var diagnosticsWhat: String { Strings.get("DiagnosticsWhat") }

    /// Diagnostics copied.
    public static var diagnosticsCopied: String { Strings.get("DiagnosticsCopied") }

    /// Environments are set up.
    public static var setupDone: String { Strings.get("SetupDone") }

    /// Start over
    public static var restart: String { Strings.get("Restart") }

    /// Removes the environments from Aiko and opens the checklist right here.
    public static var restartWhat: String { Strings.get("RestartWhat") }

    /// Aiko removes the environments, its lines and plugins in settings.json, the commands {0}, ...
    public static var restartLine: String { Strings.get("RestartLine") }

    /// Aiko removes the environments, its lines and plugins in settings.json, the commands {0}, ...
    public static var restartLineMac: String { Strings.get("RestartLineMac") }

    /// And move {0} to the Recycle Bin
    public static var restartBin: String { Strings.get("RestartBin") }

    /// And move {0} to the Trash
    public static var restartTrash: String { Strings.get("RestartTrash") }

    /// .claude stays either way.
    public static var restartKeep: String { Strings.get("RestartKeep") }

    /// Open Claude Code
    public static var openClaudeCode: String { Strings.get("OpenClaudeCode") }

    /// Sign in so Aiko can see the limits of this environment.
    public static var cardSignInNote: String { Strings.get("CardSignInNote") }

    /// working now
    public static var stateWorkingNow: String { Strings.get("StateWorkingNow") }

    /// Personality
    public static var navPersonality: String { Strings.get("NavPersonality") }

    /// on: {0}
    public static var navPersonaOn: String { Strings.get("NavPersonaOn") }

    /// off
    public static var navPersonaOff: String { Strings.get("NavPersonaOff") }

    /// Privacy
    public static var navPrivacy: String { Strings.get("NavPrivacy") }

    /// statistics: on
    public static var navPrivacyStatsOn: String { Strings.get("NavPrivacyStatsOn") }

    /// statistics: off
    public static var navPrivacyStatsOff: String { Strings.get("NavPrivacyStatsOff") }

    /// CONNECTION TO SLAYUMIND.ORG
    public static var sectionConnection: String { Strings.get("SectionConnection") }

    /// Send anonymous statistics
    public static var sendStatsToggle: String { Strings.get("SendStatsToggle") }

    /// The same request says that one copy ran today. The author of Aiko sees numbers and nothin...
    public static var sendStatsWhat: String { Strings.get("SendStatsWhat") }

    /// WHAT IS SENT
    public static var sectionWhatIsSent: String { Strings.get("SectionWhatIsSent") }

    /// the version of Aiko: whether updates arrive
    public static var sentVersion: String { Strings.get("SentVersion") }

    /// the version of Windows: what to test first
    public static var sentWindows: String { Strings.get("SentWindows") }

    /// the version of macOS: what to test first
    public static var sentMacOS: String { Strings.get("SentMacOS") }

    /// an ID that changes every day
    public static var sentDayId: String { Strings.get("SentDayId") }

    /// the first run this week
    public static var sentWeekFlag: String { Strings.get("SentWeekFlag") }

    /// the first run this month
    public static var sentMonthFlag: String { Strings.get("SentMonthFlag") }

    /// the personality is on in at least one environment
    public static var sentPersona: String { Strings.get("SentPersona") }

    /// None of this is sent right now.
    public static var nothingSentYet: String { Strings.get("NothingSentYet") }

    /// WHAT NEVER LEAVES
    public static var sectionNeverSent: String { Strings.get("SectionNeverSent") }

    /// Your name, email, project folders, limits, token and session texts. The site doesn't read...
    public static var neverSentWhat: String { Strings.get("NeverSentWhat") }

    /// The rows are deleted after 90 days.
    public static var statsKeptFor: String { Strings.get("StatsKeptFor") }

    /// Reset ID
    public static var resetInstallId: String { Strings.get("ResetInstallId") }

    /// Aiko makes a new value, and the days before it can't be linked to it.
    public static var resetInstallIdWhat: String { Strings.get("ResetInstallIdWhat") }

    /// The ID is new.
    public static var resetInstallIdDone: String { Strings.get("ResetInstallIdDone") }

    /// Open PRIVACY.md
    public static var openPrivacyDoc: String { Strings.get("OpenPrivacyDoc") }

    /// What Aiko sends and to whom — Privacy.
    public static var privacyLinkFromGeneral: String { Strings.get("PrivacyLinkFromGeneral") }

    /// Anonymous statistics
    public static var wizardStats: String { Strings.get("WizardStats") }

    /// The author of Aiko doesn't know how many people use it. Once a day Aiko can say that one ...
    public static var wizardStatsWhat: String { Strings.get("WizardStatsWhat") }

    /// You can change this later in settings, under Privacy.
    public static var wizardStatsLater: String { Strings.get("WizardStatsLater") }

    /// Don't send
    public static var statsDecline: String { Strings.get("StatsDecline") }

    /// Send
    public static var statsAccept: String { Strings.get("StatsAccept") }

    /// on
    public static var statsStateOn: String { Strings.get("StatsStateOn") }

    /// off
    public static var statsStateOff: String { Strings.get("StatsStateOff") }

    /// not chosen
    public static var statsStateUnset: String { Strings.get("StatsStateUnset") }

    /// WHERE AIKO TALKS
    public static var sectionWhereAikoTalks: String { Strings.get("SectionWhereAikoTalks") }

    /// Works in new sessions. Open sessions finish the way they started.
    public static var personaNewSessions: String { Strings.get("PersonaNewSessions") }

    /// While the personality is on, your style {0} doesn't work. Turn it off and it comes back.
    public static var personaOwnStyle: String { Strings.get("PersonaOwnStyle") }

    /// FACE
    public static var sectionFace: String { Strings.get("SectionFace") }

    /// Chibi
    public static var faceChibi: String { Strings.get("FaceChibi") }

    /// Emoji
    public static var faceEmoji: String { Strings.get("FaceEmoji") }

    /// This is how Aiko looks in the tray, on the island and in windows.
    public static var faceWhere: String { Strings.get("FaceWhere") }

    /// That's how Aiko looks in the menu bar, on the island and in the windows.
    public static var faceWhereMac: String { Strings.get("FaceWhereMac") }

    /// TEMPERAMENT
    public static var sectionTemperament: String { Strings.get("SectionTemperament") }

    /// Quiet
    public static var temperamentQuiet: String { Strings.get("TemperamentQuiet") }

    /// Normal
    public static var temperamentNormal: String { Strings.get("TemperamentNormal") }

    /// Bright
    public static var temperamentBright: String { Strings.get("TemperamentBright") }

    /// Almost no character: one warm line at the end of an answer.
    public static var temperamentQuietAbout: String { Strings.get("TemperamentQuietAbout") }

    /// A short interjection at the start, a quick verdict at the end. Japanese words now and then.
    public static var temperamentNormalAbout: String { Strings.get("TemperamentNormalAbout") }

    /// An interjection in every answer, game metaphors, Japanese words more often.
    public static var temperamentBrightAbout: String { Strings.get("TemperamentBrightAbout") }

    /// Everything at full. In code, commits, files and errors she still stays quiet.
    public static var temperamentMusouAbout: String { Strings.get("TemperamentMusouAbout") }

    /// HOW SHE'LL ANSWER
    public static var sectionSampleReply: String { Strings.get("SectionSampleReply") }

    /// Why does the reset countdown show 0 minutes?
    public static var sampleQuestion: String { Strings.get("SampleQuestion") }

    /// The limits test passed by luck: rounding went down, so a reset at 14:59:30 showed as “in ...
    public static var sampleBody: String { Strings.get("SampleBody") }

    /// The limits test passed by luck: rounding went down, so a reset at 14:59:30 hid behind “in...
    public static var sampleBodyBright: String { Strings.get("SampleBodyBright") }

    ///  Parried it, like in Elden Ring.
    public static var sampleBodyMusouTail: String { Strings.get("SampleBodyMusouTail") }

    /// えへへ, got it.
    public static var sampleOpenNormal: String { Strings.get("SampleOpenNormal") }

    /// やった, got it!
    public static var sampleOpenBright: String { Strings.get("SampleOpenBright") }

    /// やった〜! すごい, what a sneaky one!
    public static var sampleOpenMusou: String { Strings.get("SampleOpenMusou") }

    /// A rare bug. It was nice to find.
    public static var sampleCloseQuiet: String { Strings.get("SampleCloseQuiet") }

    /// In my own game I'd have hunted a bug like this for half a day.
    public static var sampleCloseNormal: String { Strings.get("SampleCloseNormal") }

    /// お疲れ様!
    public static var sampleCloseBright: String { Strings.get("SampleCloseBright") }

    /// よし、行くぞ! On to the next one (^_^)
    public static var sampleCloseMusou: String { Strings.get("SampleCloseMusou") }

    /// The commit for this fix has no character:
    public static var sampleCommit: String { Strings.get("SampleCommit") }

    /// SKILLS · {0}
    public static var sectionSkills: String { Strings.get("SectionSkills") }

    /// Skills work in environments where the personality is on.
    public static var skillsWork: String { Strings.get("SkillsWork") }

    /// Aiko's skills
    public static var skillsSwitch: String { Strings.get("SkillsSwitch") }

    /// Turn the personality on in at least one environment, and the skills start working.
    public static var skillsNeedPersona: String { Strings.get("SkillsNeedPersona") }

    /// PROJECT MANAGEMENT
    public static var skillDomainProjects: String { Strings.get("SkillDomainProjects") }

    /// GAME DEVELOPMENT
    public static var skillDomainGames: String { Strings.get("SkillDomainGames") }

    /// UI text, READMEs and release notes without stiff wording or AI tells. RU and EN.
    public static var skillAikoCopy: String { Strings.get("SkillAikoCopy") }

    /// Before a release: what can't be undone, how to roll back, whether the docs match the code.
    public static var skillAikoReleaseGate: String { Strings.get("SkillAikoReleaseGate") }

    /// One fact in one place, a decision log, contradictions found.
    public static var skillAikoDocsHygiene: String { Strings.get("SkillAikoDocsHygiene") }

    /// Checks a game with numbers: the same test scene before and after a change.
    public static var skillAikoPlaytest: String { Strings.get("SkillAikoPlaytest") }

    /// A model from Blender to Unity: axes, normals, export and import.
    public static var skillAikoBlenderToUnity: String { Strings.get("SkillAikoBlenderToUnity") }

    /// Ready files for textures: a size that fits the camera, and sheets to paint over.
    public static var skillAikoTexturing: String { Strings.get("SkillAikoTexturing") }

    /// A .glb for the web: size, axes, compression, a blank view where the model should be.
    public static var skillAikoGlbForWeb: String { Strings.get("SkillAikoGlbForWeb") }

    /// Fit a color to the palette in OKLCH with the smallest change.
    public static var skillAikoPalette: String { Strings.get("SkillAikoPalette") }

    /// 30–40 games with the mechanic you need: an overview with screenshots, and interactive dem...
    public static var skillAikoGamedesignResearch: String { Strings.get("SkillAikoGamedesignResearch") }

    /// Plan the day in Google Calendar, find free time, add events after your yes.
    public static var skillAikoCalendar: String { Strings.get("SkillAikoCalendar") }

    /// Documents from Google Drive into the work, and files from the project onto Drive.
    public static var skillAikoDrive: String { Strings.get("SkillAikoDrive") }

    /// Meet Aiko
    public static var itemMeetAiko: String { Strings.get("ItemMeetAiko") }

    /// Aiko can talk in your Claude Code sessions in her own voice, and she brings eleven skills...
    public static var meetAikoLead: String { Strings.get("MeetAikoLead") }

    /// The temperament, the face and the skills are in settings, under Personality. It works in ...
    public static var meetAikoSettings: String { Strings.get("MeetAikoSettings") }

    /// Turn on in {0}
    public static var meetAikoTurnOn: String { Strings.get("MeetAikoTurnOn") }

    /// on
    public static var stateOn: String { Strings.get("StateOn") }
}
