using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json.Serialization;

namespace CS2Parachute;

public class ParachuteConfig : BasePluginConfig
{
    [JsonPropertyName("css_parachute_enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("css_parachute_decrease_vector")]
    public float DecreaseVec { get; set; } = 50.0f;

    [JsonPropertyName("css_parachute_linear_decrease")]
    public bool Linear { get; set; } = true;

    [JsonPropertyName("css_parachute_fall_speed")]
    public float FallSpeed { get; set; } = 100.0f;

    [JsonPropertyName("css_parachute_teleport_ticks")]
    public int TeleportTicks { get; set; } = 300;
}

[MinimumApiVersion(241)]
public class Parachute : BasePlugin, IPluginConfig<ParachuteConfig>
{
    public override string ModuleName => "CS2 Parachute";
    public override string ModuleAuthor => "Fixed by le1t1337 + AI DeepSeek. Code logic by Franc1sco Franug";
    public override string ModuleVersion => "1.6";

    public required ParachuteConfig Config { get; set; }
    public void OnConfigParsed(ParachuteConfig config) => Config = config;

    private readonly Dictionary<int, bool> _activeParachutes = new();
    private readonly Dictionary<int, int> _parachuteTicks = new();

    public override void Load(bool hotReload)
    {
        // Регистрируем ConVar команды для удобного управления
        AddCommand("css_parachute_help", "Show Parachute help", OnHelpCommand);
        AddCommand("css_parachute_settings", "Show current Parachute settings", OnSettingsCommand);
        AddCommand("css_parachute_test", "Test parachute functionality", OnTestCommand);
        
        // Выводим информацию о ConVar переменных
        PrintConVarInfo();
        
        if (!Config.Enabled)
        {
            Console.WriteLine("[Parachute] Plugin disabled in configuration");
            return;
        }

        RegisterEventHandlers();
        
        // Register tick handler for parachute physics
        RegisterListener<Listeners.OnTick>(OnGameTick);
    }

    private void PrintConVarInfo()
    {
        Server.PrintToConsole("===============================================");
        Server.PrintToConsole("[Parachute] Plugin successfully loaded!");
        Server.PrintToConsole("[Parachute] Configuration file: configs/plugins/CS2Parachute/Parachute.json");
        Server.PrintToConsole("[Parachute] Current settings:");
        Server.PrintToConsole($"[Parachute]   css_parachute_enabled = {Config.Enabled}");
        Server.PrintToConsole($"[Parachute]   css_parachute_decrease_vector = {Config.DecreaseVec}");
        Server.PrintToConsole($"[Parachute]   css_parachute_linear_decrease = {Config.Linear}");
        Server.PrintToConsole($"[Parachute]   css_parachute_fall_speed = {Config.FallSpeed}");
        Server.PrintToConsole($"[Parachute]   css_parachute_teleport_ticks = {Config.TeleportTicks}");
        Server.PrintToConsole("[Parachute] Commands:");
        Server.PrintToConsole("[Parachute]   css_parachute_help - Show plugin help");
        Server.PrintToConsole("[Parachute]   css_parachute_settings - Show current settings");
        Server.PrintToConsole("[Parachute]   css_parachute_test - Test parachute functionality");
        Server.PrintToConsole("[Parachute]   css_plugins reload CS2Parachute - Reload config");
        Server.PrintToConsole("===============================================");
    }

    private void OnHelpCommand(CCSPlayerController? player, CommandInfo command)
    {
        var helpMessage = "\n===============================================\n" +
                         "PARACHUTE PLUGIN HELP\n" +
                         "===============================================\n" +
                         "DESCRIPTION:\n" +
                         "  Allows players to use parachute by holding USE button (E) while falling.\n" +
                         "  When parachute is active, falling speed is significantly reduced.\n\n" +
                         "USAGE:\n" +
                         "  - Hold the USE key (default: E) while falling to activate parachute\n" +
                         "  - Release USE key to deactivate parachute\n" +
                         "  - Parachute automatically deactivates when landing or dying\n\n" +
                         "CONFIGURATION VARIABLES (edit in configs/plugins/CS2Parachute/Parachute.json):\n" +
                         "  css_parachute_enabled (boolean)\n" +
                         "      Enable/disable the parachute plugin\n" +
                         "      Default: true, Options: true/false\n\n" +
                         "  css_parachute_decrease_vector (float)\n" +
                         "      Velocity decrease amount when parachute is active\n" +
                         "      Higher values = slower fall, faster deceleration\n" +
                         "      Default: 50.0, Recommended range: 30-100\n\n" +
                         "  css_parachute_linear_decrease (boolean)\n" +
                         "      Use linear velocity decrease (true) or additive decrease (false)\n" +
                         "      Linear: Constant fall speed, Additive: Gradual slowing\n" +
                         "      Default: true, Options: true/false\n\n" +
                         "  css_parachute_fall_speed (float)\n" +
                         "      Maximum fall speed when parachute is active\n" +
                         "      Lower values = slower maximum fall speed\n" +
                         "      Default: 100.0, Recommended range: 50-200\n\n" +
                         "  css_parachute_teleport_ticks (integer)\n" +
                         "      Number of ticks between player position updates\n" +
                         "      Prevents prediction errors and client-side glitches\n" +
                         "      Default: 300, Recommended range: 100-500\n\n" +
                         "CONSOLE COMMANDS:\n" +
                         "  css_parachute_help - Show this help message\n" +
                         "  css_parachute_settings - Show current plugin settings\n" +
                         "  css_parachute_test - Test parachute functionality\n" +
                         "  css_plugins reload CS2Parachute - Reload configuration file\n" +
                         "===============================================\n";
        
        if (player != null)
        {
            player.PrintToConsole(helpMessage);
            player.PrintToChat("Parachute: Check console for plugin help");
        }
        else
        {
            Server.PrintToConsole(helpMessage);
        }
    }

    private void OnSettingsCommand(CCSPlayerController? player, CommandInfo command)
    {
        var settingsMessage = $"\n===============================================\n" +
                             "PARACHUTE CURRENT SETTINGS\n" +
                             "===============================================\n" +
                             $"Plugin Enabled: {Config.Enabled}\n" +
                             $"Decrease Vector: {Config.DecreaseVec}\n" +
                             $"Linear Decrease: {Config.Linear}\n" +
                             $"Max Fall Speed: {Config.FallSpeed}\n" +
                             $"Teleport Ticks: {Config.TeleportTicks}\n" +
                             "===============================================\n";
        
        if (player != null)
        {
            player.PrintToConsole(settingsMessage);
            player.PrintToChat($"Parachute: Enabled={Config.Enabled}, FallSpeed={Config.FallSpeed}");
        }
        else
        {
            Server.PrintToConsole(settingsMessage);
        }
    }

    private void OnTestCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null)
        {
            player.PrintToChat("=== PARACHUTE TEST ===");
            player.PrintToChat($"To test parachute:");
            player.PrintToChat($"1. Jump from a high place (like a building)");
            player.PrintToChat($"2. While falling, hold USE key (default: E)");
            player.PrintToChat($"3. You should slow down significantly");
            player.PrintToChat($"4. Release USE key to deactivate parachute");
            player.PrintToChat($"Current settings: Decrease={Config.DecreaseVec}, Linear={Config.Linear}");
        }
        else
        {
            Server.PrintToConsole("Parachute test command can only be used by players");
        }
    }

    private void RegisterEventHandlers()
    {
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Pre);
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        
        if (player == null || player.IsBot || !player.IsValid)
            return HookResult.Continue;

        int playerIndex = (int)player.Index;
        _activeParachutes[playerIndex] = false;
        _parachuteTicks[playerIndex] = 0;
        
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
        if (!Config.Enabled) return;

        foreach (var player in Utilities.GetPlayers())
        {
            if (!IsValidPlayer(player)) continue;

            int playerIndex = (int)player.Index;
            
            // Check for USE button press while in air
            if ((player.Buttons & PlayerButtons.Use) != 0 && !player.PlayerPawn.Value!.OnGroundLastTick)
            {
                StartParachute(player);
            }
            else if (_activeParachutes.TryGetValue(playerIndex, out bool isActive) && isActive)
            {
                StopParachute(player);
            }
        }
    }

    private bool IsValidPlayer(CCSPlayerController player)
    {
        return player != null && 
               player.IsValid && 
               !player.IsBot && 
               player.PlayerPawn != null && 
               player.PlayerPawn.IsValid && 
               player.PlayerPawn.Value != null && 
               player.PawnIsAlive;
    }

    private void StartParachute(CCSPlayerController player)
    {
        int playerIndex = (int)player.Index;
        
        if (!_activeParachutes.TryGetValue(playerIndex, out bool isActive) || !isActive)
        {
            _activeParachutes[playerIndex] = true;
            player.PlayerPawn.Value!.GravityScale = 0.1f;
        }

        ApplyParachutePhysics(player);
    }

    private void ApplyParachutePhysics(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null) return;

        int playerIndex = (int)player.Index;
        var velocity = pawn.AbsVelocity;
        
        if (velocity.Z < 0.0f)
        {
            float fallspeed = Config.FallSpeed * -1.0f;
            bool isFallSpeed = velocity.Z >= fallspeed;

            if (isFallSpeed && Config.Linear || Config.DecreaseVec == 0.0f)
            {
                velocity.Z = fallspeed;
            }
            else
            {
                velocity.Z += Config.DecreaseVec;
            }

            // Update player position periodically to prevent prediction errors
            _parachuteTicks[playerIndex]++;
            if (_parachuteTicks[playerIndex] > Config.TeleportTicks)
            {
                player.Teleport(pawn.AbsOrigin, pawn.AbsRotation, velocity);
                _parachuteTicks[playerIndex] = 0;
            }
            else
            {
                // Use Teleport with current position and rotation to update velocity
                player.Teleport(pawn.AbsOrigin, pawn.AbsRotation, velocity);
            }
        }
    }

    private void StopParachute(CCSPlayerController player)
    {
        int playerIndex = (int)player.Index;
        _activeParachutes[playerIndex] = false;
        _parachuteTicks[playerIndex] = 0;
        
        var pawn = player.PlayerPawn.Value;
        if (pawn != null)
        {
            pawn.GravityScale = 1.0f;
        }
    }
}