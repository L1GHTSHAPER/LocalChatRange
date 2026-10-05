using System;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using TMPro;

namespace LocalChatRange
{
    /// <summary>
    /// Client-side chat command: /chatrange (alias /lcr). Handled commands are never sent to other players.
    /// </summary>
    internal static class ChatCommands
    {
        public const string CommandApiGuid = "com.on-together-mods.commandapi";

        static readonly string[] Names = { "chatrange", "lcr" };

        const string Usage =
            "/chatrange - show/hide the local chat area (hotkey: {0})\n" +
            "/chatrange on | off\n" +
            "/chatrange mode always | local | typing\n" +
            "/chatrange players on | off - highlight players in range\n" +
            "/chatrange status";

        /// <summary>Returns true when the text was one of our commands (and has been executed).</summary>
        public static bool TryExecute(string text)
        {
            if (string.IsNullOrEmpty(text) || text[0] != '/')
                return false;
            string[] parts = text.Substring(1).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || Array.IndexOf(Names, parts[0].ToLowerInvariant()) < 0)
                return false;

            Execute(parts.Skip(1).ToArray());
            return true;
        }

        public static void Execute(string[] args)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin == null)
                return;

            string action = args.Length > 0 ? args[0].ToLowerInvariant() : "toggle";
            string value = args.Length > 1 ? args[1].ToLowerInvariant() : null;
            switch (action)
            {
                case "toggle":
                    plugin.SetEnabled(!plugin.Enabled.Value, true);
                    break;
                case "on":
                case "show":
                    plugin.SetEnabled(true, true);
                    break;
                case "off":
                case "hide":
                    plugin.SetEnabled(false, true);
                    break;
                case "mode":
                    if (TryParseMode(value, out VisibilityMode mode))
                    {
                        plugin.Mode.Value = mode;
                        GameAccess.Notify("Mode: " + Plugin.Describe(mode));
                    }
                    else
                    {
                        GameAccess.Notify("Usage: /chatrange mode always | local | typing");
                    }
                    break;
                case "players":
                    if (value == "on" || value == "off")
                    {
                        plugin.HighlightPlayers.Value = value == "on";
                        GameAccess.Notify("Highlight players in range: " + value);
                    }
                    else
                    {
                        GameAccess.Notify("Usage: /chatrange players on | off");
                    }
                    break;
                case "status":
                    GameAccess.Notify(plugin.DescribeStatus());
                    break;
                default:
                    GameAccess.Notify(string.Format(Usage, plugin.ToggleKey.Value));
                    break;
            }
        }

        static bool TryParseMode(string value, out VisibilityMode mode)
        {
            switch (value)
            {
                case "always":
                    mode = VisibilityMode.Always;
                    return true;
                case "local":
                    mode = VisibilityMode.LocalTabOnly;
                    return true;
                case "typing":
                    mode = VisibilityMode.WhileTypingLocal;
                    return true;
                default:
                    mode = VisibilityMode.Always;
                    return false;
            }
        }

        /// <summary>
        /// When CommandAPI is installed, register the command there too so it is listed by its /help and
        /// suggested by CommandTypeahead. Execution still goes through our own prefix, which runs first.
        /// </summary>
        public static void RegisterWithCommandApi()
        {
            if (!Chainloader.PluginInfos.TryGetValue(CommandApiGuid, out BepInEx.PluginInfo info) || info.Instance == null)
                return;
            try
            {
                Assembly assembly = info.Instance.GetType().Assembly;
                Type registry = assembly.GetType("CommandAPI.CommandRegistry");
                Type parameterType = assembly.GetType("CommandAPI.Parameter");
                Type parameterKind = assembly.GetType("CommandAPI.ParameterType");
                MethodInfo register = registry?.GetMethod("Register", new[]
                {
                    typeof(string), typeof(string), typeof(Action<string[]>), typeof(string), parameterType.MakeArrayType()
                });
                if (register == null)
                    return;

                object stringKind = Enum.Parse(parameterKind, "String");
                Array parameters = Array.CreateInstance(parameterType, 2);
                parameters.SetValue(Activator.CreateInstance(parameterType, "action", stringKind, true, null, null), 0);
                parameters.SetValue(Activator.CreateInstance(parameterType, "value", stringKind, true, null, null), 1);

                var handler = new Action<string[]>(Execute);
                foreach (string name in Names)
                {
                    register.Invoke(null, new object[]
                    {
                        name, "LocalChatRange", handler,
                        "Show/hide the local chat range area: on, off, mode, players, status", parameters
                    });
                }
                Plugin.Log.LogInfo("Registered /chatrange with CommandAPI.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not register with CommandAPI (the command still works): " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(TextChannelManager), nameof(TextChannelManager.OnEnterPressed))]
    internal static class ChatCommandPatch
    {
        // Runs before other mods' command handlers. A handled command clears the input field, so the game
        // and the other prefixes see an empty message: nothing is sent, and the game still releases the
        // input lock and deselects the field as it does after every Enter.
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        static void Prefix()
        {
            try
            {
                UIManager ui = MonoSingleton<UIManager>.I;
                TMP_InputField input = ui != null ? ui.MessageInput : null;
                if (input != null && ChatCommands.TryExecute(input.text))
                    input.text = string.Empty;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Chat command failed: " + e);
            }
        }
    }
}
