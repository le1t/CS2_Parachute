using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json.Serialization;

namespace CS2Parachute;

public class CS2ParachuteConfig : BasePluginConfig
{
    [JsonPropertyName("css_parachute_enabled")]
    public int Enabled { get; set; } = 1; // 1 - включен, 0 - выключен

    [JsonPropertyName("css_parachute_decrease_vector")]
    public float DecreaseVec { get; set; } = 50.0f; // Величина уменьшения скорости при активном парашюте (рекомендуемый диапазон 30-100)

    [JsonPropertyName("css_parachute_linear_decrease")]
    public int Linear { get; set; } = 1; // 1 - линейное уменьшение (постоянная скорость падения), 0 - аддитивное (постепенное замедление)

    [JsonPropertyName("css_parachute_fall_speed")]
    public float FallSpeed { get; set; } = 100.0f; // Максимальная скорость падения при активном парашюте (рекомендуемый диапазон 50-200)

    [JsonPropertyName("css_parachute_teleport_ticks")]
    public int TeleportTicks { get; set; } = 300; // Количество тиков между обновлениями позиции игрока (предотвращает ошибки предсказания, 100-500)

    [JsonPropertyName("css_parachute_log_level")]
    public int LogLevel { get; set; } = 4; // Уровень логирования: 0-Trace, 1-Debug, 2-Information, 3-Warning, 4-Error, 5-Critical
}

[MinimumApiVersion(362)]
public class CS2Parachute : BasePlugin, IPluginConfig<CS2ParachuteConfig>
{
    public override string ModuleName => "CS2 Parachute";
    public override string ModuleVersion => "1.7";
    public override string ModuleAuthor => "Fixed by le1t1337 + AI DeepSeek. Code logic by Franc1sco Franug";

    public required CS2ParachuteConfig Config { get; set; }

    private readonly Dictionary<int, bool> _activeParachutes = new();
    private readonly Dictionary<int, int> _parachuteTicks = new();

    public void OnConfigParsed(CS2ParachuteConfig config)
    {
        // Валидация параметров
        config.Enabled = Math.Clamp(config.Enabled, 0, 1);
        config.DecreaseVec = Math.Clamp(config.DecreaseVec, 0.0f, 500.0f);
        config.Linear = Math.Clamp(config.Linear, 0, 1);
        config.FallSpeed = Math.Clamp(config.FallSpeed, 10.0f, 500.0f);
        config.TeleportTicks = Math.Clamp(config.TeleportTicks, 10, 1000);
        config.LogLevel = Math.Clamp(config.LogLevel, 0, 5);

        Config = config;
    }

    public override void Load(bool hotReload)
    {
        // Регистрация команд
        AddCommand("css_parachute_help", "Показать справку по плагину", OnHelpCommand);
        AddCommand("css_parachute_settings", "Показать текущие настройки", OnSettingsCommand);
        AddCommand("css_parachute_test", "Тестовая команда", OnTestCommand);
        AddCommand("css_parachute_reload", "Перезагрузить конфигурацию", OnReloadCommand);

        AddCommand("css_parachute_setenabled", "Включить/выключить плагин (0/1)", OnSetEnabledCommand);
        AddCommand("css_parachute_setdecreasevector", "Установить decrease_vector (0-500)", OnSetDecreaseVecCommand);
        AddCommand("css_parachute_setlinear", "Установить linear (0/1)", OnSetLinearCommand);
        AddCommand("css_parachute_setfallspeed", "Установить fall_speed (10-500)", OnSetFallSpeedCommand);
        AddCommand("css_parachute_setteleportticks", "Установить teleport_ticks (10-1000)", OnSetTeleportTicksCommand);
        AddCommand("css_parachute_setloglevel", "Установить уровень логов (0-5)", OnSetLogLevelCommand);

        // Регистрация событий
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);

        // Подписка на тик
        RegisterListener<Listeners.OnTick>(OnGameTick);

        PrintInfo();

        if (hotReload)
        {
            Server.NextFrame(() =>
            {
                // Ничего особенного, тик сам подхватит игроков
            });
        }
    }

    private void PrintInfo()
    {
        Log(LogLevel.Information, "===============================================");
        Log(LogLevel.Information, $"Плагин {ModuleName} версии {ModuleVersion} успешно загружен!");
        Log(LogLevel.Information, $"Автор: {ModuleAuthor}");
        Log(LogLevel.Information, "Текущие настройки:");
        Log(LogLevel.Information, $"  css_parachute_enabled = {Config.Enabled} (0/1)");
        Log(LogLevel.Information, $"  css_parachute_decrease_vector = {Config.DecreaseVec:F2}");
        Log(LogLevel.Information, $"  css_parachute_linear_decrease = {Config.Linear} (0/1)");
        Log(LogLevel.Information, $"  css_parachute_fall_speed = {Config.FallSpeed:F2}");
        Log(LogLevel.Information, $"  css_parachute_teleport_ticks = {Config.TeleportTicks}");
        Log(LogLevel.Information, $"  css_parachute_log_level = {Config.LogLevel} (0-Trace, 1-Debug, 2-Information, 3-Warning, 4-Error, 5-Critical)");
        Log(LogLevel.Information, "===============================================");
    }

    private void Log(LogLevel level, string message)
    {
        if ((int)level >= Config.LogLevel)
        {
            Logger.Log(level, "[Parachute] {Message}", message);
        }
    }

    private bool IsValidPlayer(CCSPlayerController player)
    {
        // Проверка только на жизнь и валидность – боты и зрители не отсеиваются по типу
        return player != null && 
               player.IsValid && 
               player.PlayerPawn != null && 
               player.PlayerPawn.IsValid && 
               player.PlayerPawn.Value != null && 
               player.PawnIsAlive;
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        int playerIndex = (int)player.Index;
        _activeParachutes[playerIndex] = false;
        _parachuteTicks[playerIndex] = 0;

        Log(LogLevel.Debug, $"Игрок {player.PlayerName} подключился, индекс {playerIndex}");
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        int playerIndex = (int)player.Index;
        _activeParachutes.Remove(playerIndex);
        _parachuteTicks.Remove(playerIndex);

        Log(LogLevel.Debug, $"Игрок {player.PlayerName} отключился, данные очищены");
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid)
        {
            int playerIndex = (int)player.Index;
            if (_activeParachutes.TryGetValue(playerIndex, out bool isActive) && isActive)
            {
                StopParachute(player);
            }
        }
        return HookResult.Continue;
    }

    private void OnGameTick()
    {
        if (Config.Enabled == 0) return;

        foreach (var player in Utilities.GetPlayers())
        {
            if (!IsValidPlayer(player)) continue;

            int playerIndex = (int)player.Index;
            var pawn = player.PlayerPawn.Value!; // точно не null после IsValidPlayer

            // Проверка нажатия кнопки USE (E) и нахождение в воздухе
            if ((player.Buttons & PlayerButtons.Use) != 0 && !pawn.OnGroundLastTick)
            {
                StartParachute(player);
            }
            else if (_activeParachutes.TryGetValue(playerIndex, out bool isActive) && isActive)
            {
                StopParachute(player);
            }
        }
    }

    private void StartParachute(CCSPlayerController player)
    {
        int playerIndex = (int)player.Index;

        if (!_activeParachutes.TryGetValue(playerIndex, out bool isActive) || !isActive)
        {
            _activeParachutes[playerIndex] = true;
            player.PlayerPawn.Value!.GravityScale = 0.1f;
            Log(LogLevel.Trace, $"Игрок {player.PlayerName} активировал парашют");
        }

        ApplyParachutePhysics(player);
    }

    private void ApplyParachutePhysics(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn.Value!; // вызывается только когда игрок валиден
        int playerIndex = (int)player.Index;
        var velocity = pawn.AbsVelocity;

        if (velocity.Z < 0.0f)
        {
            float targetFallSpeed = Config.FallSpeed * -1.0f; // отрицательная скорость вниз
            bool isFasterThanTarget = velocity.Z >= targetFallSpeed; // velocity.Z отрицательное, поэтому "быстрее" = ближе к нулю

            if (isFasterThanTarget && Config.Linear == 1 || Config.DecreaseVec == 0.0f)
            {
                velocity.Z = targetFallSpeed;
            }
            else
            {
                velocity.Z += Config.DecreaseVec; // увеличиваем (делаем менее отрицательной)
            }

            // Периодическое обновление позиции для предотвращения ошибок предсказания
            _parachuteTicks[playerIndex]++;
            if (_parachuteTicks[playerIndex] > Config.TeleportTicks)
            {
                player.Teleport(pawn.AbsOrigin, pawn.AbsRotation, velocity);
                _parachuteTicks[playerIndex] = 0;
            }
            else
            {
                player.Teleport(pawn.AbsOrigin, pawn.AbsRotation, velocity);
            }
        }
    }

    private void StopParachute(CCSPlayerController player)
    {
        int playerIndex = (int)player.Index;
        _activeParachutes[playerIndex] = false;
        _parachuteTicks[playerIndex] = 0;

        var pawn = player.PlayerPawn?.Value;
        if (pawn != null)
        {
            pawn.GravityScale = 1.0f;
            Log(LogLevel.Trace, $"Игрок {player.PlayerName} деактивировал парашют");
        }
    }

    // ----- Команды -----

    private void OnHelpCommand(CCSPlayerController? player, CommandInfo command)
    {
        string help = $"""
            ================================================
            СПРАВКА ПО ПЛАГИНУ {ModuleName} v{ModuleVersion}
            ================================================
            ОПИСАНИЕ:
              Позволяет игрокам использовать парашют, удерживая кнопку USE (E) во время падения.
              При активации скорость падения значительно снижается.

            ИСПОЛЬЗОВАНИЕ:
              - Удерживайте USE (по умолчанию E) во время падения для активации парашюта
              - Отпустите USE для деактивации
              - Парашют автоматически деактивируется при приземлении или смерти

            КОМАНДЫ:
              css_parachute_help                - показать эту справку
              css_parachute_settings             - показать текущие настройки
              css_parachute_test                  - проверить работу плагина
              css_parachute_reload                - перезагрузить конфигурацию
              css_parachute_setenabled <0/1>      - вкл/выкл плагин
              css_parachute_setdecreasevector <0-500> - установить decrease_vector
              css_parachute_setlinear <0/1>        - установить linear (0/1)
              css_parachute_setfallspeed <10-500>  - установить fall_speed
              css_parachute_setteleportticks <10-1000> - установить teleport_ticks
              css_parachute_setloglevel <0-5>     - установить уровень логов (0-Trace, 1-Debug, 2-Information, 3-Warning, 4-Error, 5-Critical)

            ПРИМЕРЫ:
              css_parachute_setdecreasevector 70
              css_parachute_setlinear 1
            ================================================
            """;
        command.ReplyToCommand(help);
        if (player != null)
        {
            player.PrintToChat($"[Parachute] Справка отправлена в консоль.");
        }
    }

    private void OnSettingsCommand(CCSPlayerController? player, CommandInfo command)
    {
        int activeCount = _activeParachutes.Count(kvp => kvp.Value);
        string status = Config.Enabled == 1 ? "Включён" : "Отключён";

        string settings = $"""
            ================================================
            ТЕКУЩИЕ НАСТРОЙКИ {ModuleName} v{ModuleVersion}
            ================================================
            Статус плагина: {status}
            decrease_vector: {Config.DecreaseVec:F2}
            linear: {Config.Linear} (0/1)
            fall_speed: {Config.FallSpeed:F2}
            teleport_ticks: {Config.TeleportTicks}
            Уровень логов: {Config.LogLevel} (0-Trace, 1-Debug, 2-Information, 3-Warning, 4-Error, 5-Critical)
            Активных парашютов: {activeCount}
            Всего игроков в словаре: {_activeParachutes.Count}
            ================================================
            """;
        command.ReplyToCommand(settings);
        if (player != null)
        {
            player.PrintToChat($"[Parachute] Настройки отправлены в консоль.");
        }
    }

    private void OnTestCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null)
        {
            command.ReplyToCommand("[Parachute] Эта команда доступна только игрокам.");
            return;
        }

        if (Config.Enabled == 0)
        {
            command.ReplyToCommand("[Parachute] Плагин выключен. Включите командой css_parachute_setenabled 1.");
            return;
        }

        player.PrintToChat("=== ТЕСТ ПАРАШЮТА ===");
        player.PrintToChat("Чтобы протестировать парашют:");
        player.PrintToChat("1. Заберитесь на высокое место (например, здание)");
        player.PrintToChat("2. Прыгните и во время падения удерживайте USE (E)");
        player.PrintToChat("3. Скорость падения должна значительно снизиться");
        player.PrintToChat("4. Отпустите USE для деактивации");
        player.PrintToChat($"Текущие параметры: decrease={Config.DecreaseVec:F2}, linear={Config.Linear}, fall_speed={Config.FallSpeed:F2}");
        command.ReplyToCommand("[Parachute] Тестовая информация выведена в чат.");
    }

    private void OnReloadCommand(CCSPlayerController? player, CommandInfo command)
    {
        // Останавливаем все активные парашюты
        foreach (var kv in _activeParachutes.Where(kv => kv.Value).ToList())
        {
            var p = Utilities.GetPlayerFromIndex(kv.Key);
            if (p != null && p.IsValid)
                StopParachute(p);
        }
        _activeParachutes.Clear();
        _parachuteTicks.Clear();

        // Перезагружаем конфиг из файла
        try
        {
            string configPath = Path.Combine(Server.GameDirectory, "counterstrikesharp", "configs", "plugins", "CS2Parachute", "CS2Parachute.json");
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                var newConfig = System.Text.Json.JsonSerializer.Deserialize<CS2ParachuteConfig>(json);
                if (newConfig != null)
                {
                    OnConfigParsed(newConfig);
                }
            }
            else
            {
                SaveConfig();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка при перезагрузке конфига");
        }

        command.ReplyToCommand("[Parachute] Конфигурация перезагружена.");
        Log(LogLevel.Information, "Конфигурация перезагружена по команде.");
    }

    private void OnSetEnabledCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[Parachute] Текущее значение Enabled: {Config.Enabled} (по умолч. 1). Использование: css_parachute_setenabled <0/1>");
            return;
        }

        string arg = command.GetArg(1);
        if (int.TryParse(arg, out int value) && (value == 0 || value == 1))
        {
            int old = Config.Enabled;
            Config.Enabled = value;
            SaveConfig();

            if (value == 0)
            {
                // При выключении деактивируем все парашюты
                foreach (var kv in _activeParachutes.Where(kv => kv.Value).ToList())
                {
                    var p = Utilities.GetPlayerFromIndex(kv.Key);
                    if (p != null && p.IsValid)
                        StopParachute(p);
                }
            }

            command.ReplyToCommand($"[Parachute] Enabled изменён с {old} на {value}.");
        }
        else
        {
            command.ReplyToCommand("[Parachute] Неверное значение. Используйте 0 или 1.");
        }
    }

    private void OnSetDecreaseVecCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[Parachute] Текущее значение decrease_vector: {Config.DecreaseVec:F2} (по умолч. 50). Использование: css_parachute_setdecreasevector <0-500>");
            return;
        }

        string arg = command.GetArg(1).Replace(',', '.');
        if (float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            float old = Config.DecreaseVec;
            Config.DecreaseVec = Math.Clamp(value, 0.0f, 500.0f);
            SaveConfig();
            command.ReplyToCommand($"[Parachute] decrease_vector изменён с {old:F2} на {Config.DecreaseVec:F2}.");
        }
        else
        {
            command.ReplyToCommand("[Parachute] Неверное значение. Используйте число с точкой, например 50.5.");
        }
    }

    private void OnSetLinearCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[Parachute] Текущее значение linear: {Config.Linear} (по умолч. 1). Использование: css_parachute_setlinear <0/1>");
            return;
        }

        string arg = command.GetArg(1);
        if (int.TryParse(arg, out int value) && (value == 0 || value == 1))
        {
            int old = Config.Linear;
            Config.Linear = value;
            SaveConfig();
            command.ReplyToCommand($"[Parachute] linear изменён с {old} на {value}.");
        }
        else
        {
            command.ReplyToCommand("[Parachute] Неверное значение. Используйте 0 или 1.");
        }
    }

    private void OnSetFallSpeedCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[Parachute] Текущее значение fall_speed: {Config.FallSpeed:F2} (по умолч. 100). Использование: css_parachute_setfallspeed <10-500>");
            return;
        }

        string arg = command.GetArg(1).Replace(',', '.');
        if (float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            float old = Config.FallSpeed;
            Config.FallSpeed = Math.Clamp(value, 10.0f, 500.0f);
            SaveConfig();
            command.ReplyToCommand($"[Parachute] fall_speed изменён с {old:F2} на {Config.FallSpeed:F2}.");
        }
        else
        {
            command.ReplyToCommand("[Parachute] Неверное значение. Используйте число с точкой, например 120.5.");
        }
    }

    private void OnSetTeleportTicksCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[Parachute] Текущее значение teleport_ticks: {Config.TeleportTicks} (по умолч. 300). Использование: css_parachute_setteleportticks <10-1000>");
            return;
        }

        string arg = command.GetArg(1);
        if (int.TryParse(arg, out int value))
        {
            int old = Config.TeleportTicks;
            Config.TeleportTicks = Math.Clamp(value, 10, 1000);
            SaveConfig();
            command.ReplyToCommand($"[Parachute] teleport_ticks изменён с {old} на {Config.TeleportTicks}.");
        }
        else
        {
            command.ReplyToCommand("[Parachute] Неверное значение. Используйте целое число от 10 до 1000.");
        }
    }

    private void OnSetLogLevelCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[Parachute] Текущий уровень логов: {Config.LogLevel} (0-Trace, 1-Debug, 2-Information, 3-Warning, 4-Error, 5-Critical, по умолч. 4). Использование: css_parachute_setloglevel <0-5>");
            return;
        }

        string arg = command.GetArg(1);
        if (int.TryParse(arg, out int value) && value >= 0 && value <= 5)
        {
            int old = Config.LogLevel;
            Config.LogLevel = value;
            SaveConfig();
            command.ReplyToCommand($"[Parachute] Уровень логов изменён с {old} на {Config.LogLevel}.");
        }
        else
        {
            command.ReplyToCommand("[Parachute] Неверное значение. Используйте число от 0 до 5.");
        }
    }

    private void SaveConfig()
    {
        try
        {
            string configPath = Path.Combine(Server.GameDirectory, "counterstrikesharp", "configs", "plugins", "CS2Parachute", "CS2Parachute.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string json = System.Text.Json.JsonSerializer.Serialize(Config, options);
            File.WriteAllText(configPath, json);
            Log(LogLevel.Debug, $"Конфигурация сохранена в {configPath}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка сохранения конфигурации");
        }
    }

    public override void Unload(bool hotReload)
    {
        // Восстанавливаем гравитацию всем игрокам
        foreach (var player in Utilities.GetPlayers())
        {
            if (player?.IsValid != true) continue;
            var pawn = player.PlayerPawn?.Value;
            if (pawn != null)
            {
                pawn.GravityScale = 1.0f;
            }
        }
        _activeParachutes.Clear();
        _parachuteTicks.Clear();
        Log(LogLevel.Information, "Плагин выгружен.");
    }
}