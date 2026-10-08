using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace LocalChatRange
{
    public enum VisibilityMode
    {
        Always,
        LocalTabOnly,
        WhileTypingLocal
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("OnTogether.exe")]
    [BepInDependency(ChatCommands.CommandApiGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ontogether.localchatrange";
        public const string PluginName = "LocalChatRange";
        public const string PluginVersion = "1.1.1";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        internal ConfigEntry<bool> Enabled;
        internal ConfigEntry<KeyboardShortcut> ToggleKey;
        internal ConfigEntry<VisibilityMode> Mode;
        internal ConfigEntry<bool> HighlightPlayers;

        ConfigEntry<bool> _autoDetectRadius;
        ConfigEntry<float> _radius;

        ConfigEntry<Color> _fillColor;
        ConfigEntry<Color> _outlineColor;
        ConfigEntry<float> _outlineWidth;
        ConfigEntry<bool> _outlineOnTop;
        ConfigEntry<Color> _markerColor;

        ConfigEntry<int> _segments;
        ConfigEntry<int> _rings;
        ConfigEntry<int> _refineSteps;
        ConfigEntry<float> _updateInterval;
        ConfigEntry<float> _maxStepUp;
        ConfigEntry<float> _surfaceOffset;

        float? _detectedRadius;
        Harmony _harmony;
        RangeArea _area;
        PlayerMarkers _markers;
        int _layerMask;
        readonly List<Vector3> _playersInRange = new List<Vector3>();
        bool _visualsFailed;
        string _lastError;

        Transform _pivotOwner;
        CharacterController _pivotController;
        float _pivotHeight;

        internal float Radius =>
            _autoDetectRadius.Value && _detectedRadius.HasValue ? _detectedRadius.Value : _radius.Value;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            BindConfig();
            ConfigMenu.Create(gameObject, PluginName, "range", Config, () => GameAccess.Chat != null);

            try
            {
                _detectedRadius = GameAccess.DetectRadiusFromGameCode();
            }
            catch (Exception e)
            {
                Log.LogWarning("Radius auto-detection failed: " + e.Message);
            }
            if (_detectedRadius.HasValue)
                Log.LogInfo($"Local chat radius read from the game code: {_detectedRadius.Value:0.##} m");
            else
                Log.LogWarning($"Could not read the local chat radius from the game code; using Range.Radius = {_radius.Value} m");

            try
            {
                _harmony = Harmony.CreateAndPatchAll(typeof(ChatCommandPatch), PluginGuid);
            }
            catch (Exception e)
            {
                Log.LogError("Could not patch the chat; /chatrange will not work: " + e);
            }
            ChatCommands.RegisterWithCommandApi();

            Config.SettingChanged += OnSettingChanged;
            if (_harmony != null) UiEnvironment.InstallInputGuard(_harmony);
            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Toggle with {ToggleKey.Value} or /chatrange");
        }

        void BindConfig()
        {
            Enabled = Config.Bind("General", "Enabled", true,
                "Show the local text chat range around your character.");
            ToggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F8),
                "Hotkey that shows/hides the area (ignored while typing in a text field).");
            Mode = Config.Bind("General", "VisibilityMode", VisibilityMode.Always,
                "When the area is drawn:\n" +
                "Always - whenever it is enabled;\n" +
                "LocalTabOnly - only while the chat is switched to the Local tab;\n" +
                "WhileTypingLocal - only while you are typing a message in the Local tab.");
            HighlightPlayers = Config.Bind("General", "HighlightPlayersInRange", true,
                "Draw a ring under every player who would receive your local messages.");

            _autoDetectRadius = Config.Bind("Range", "AutoDetectRadius", true,
                "Read the local chat distance from the game code at startup (recommended). If it cannot be read, Radius is used.");
            _radius = Config.Bind("Range", "Radius", 5f,
                new ConfigDescription("Local chat distance in metres, used when auto-detection is off or fails.",
                    new AcceptableValueRange<float>(0.5f, 100f)));

            _fillColor = Config.Bind("Appearance", "FillColor", new Color(0.55f, 0.85f, 1f, 0.12f),
                "Colour of the area (RRGGBBAA). The fill fades towards the centre.");
            _outlineColor = Config.Bind("Appearance", "OutlineColor", new Color(0.55f, 0.85f, 1f, 0.9f),
                "Colour of the area's edge (RRGGBBAA).");
            _outlineWidth = Config.Bind("Appearance", "OutlineWidth", 0.08f,
                new ConfigDescription("Width of the edge line in metres.", new AcceptableValueRange<float>(0.01f, 1f)));
            _outlineOnTop = Config.Bind("Appearance", "OutlineOnTop", false,
                "Draw the edge line on top of walls and furniture.");
            _markerColor = Config.Bind("Appearance", "PlayerMarkerColor", new Color(0.55f, 1f, 0.55f, 0.9f),
                "Colour of the rings under players in range (RRGGBBAA).");

            _segments = Config.Bind("Quality", "Segments", 64,
                new ConfigDescription("Points around the circle.", new AcceptableValueRange<int>(16, 256)));
            _rings = Config.Bind("Quality", "Rings", 8,
                new ConfigDescription("Surface samples from the centre to the edge. More rings follow stairs and bumps more closely.",
                    new AcceptableValueRange<int>(2, 32)));
            _refineSteps = Config.Bind("Quality", "EdgeRefineSteps", 4,
                new ConfigDescription("Extra samples used to place the edge precisely where the ground leaves the range.",
                    new AcceptableValueRange<int>(0, 8)));
            _updateInterval = Config.Bind("Quality", "UpdateInterval", 0.05f,
                new ConfigDescription("Seconds between surface re-samples while moving. The area follows you every frame regardless.",
                    new AcceptableValueRange<float>(0f, 1f)));
            _maxStepUp = Config.Bind("Quality", "MaxStepUp", 2f,
                new ConfigDescription("Ignore surfaces higher than this above your feet (ceilings, upper floors), in metres.",
                    new AcceptableValueRange<float>(0.2f, 10f)));
            _surfaceOffset = Config.Bind("Quality", "SurfaceOffset", 0.03f,
                new ConfigDescription("Lift above the surface to avoid flickering, in metres.",
                    new AcceptableValueRange<float>(0f, 0.5f)));
        }

        void Update()
        {
            if (Pressed(ToggleKey.Value) && !UiEnvironment.AnyWindowOpen && !GameAccess.IsAnyTextFieldFocused())
                SetEnabled(!Enabled.Value, false);
        }

        // KeyboardShortcut.IsDown() does not fire while any other key is held (e.g. W while walking), so only the
        // shortcut's own keys are checked.
        static bool Pressed(KeyboardShortcut shortcut)
        {
            KeyCode mainKey = shortcut.MainKey;
            if (mainKey == KeyCode.None || !Input.GetKeyDown(mainKey))
                return false;
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!Input.GetKey(modifier))
                    return false;
            }
            return true;
        }

        void LateUpdate()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                // Log each distinct error once instead of every frame.
                string message = e.GetType().Name + ": " + e.Message;
                if (message != _lastError)
                {
                    _lastError = message;
                    Log.LogError("Update failed: " + e);
                }
                HideVisuals();
            }
        }

        void Tick()
        {
            TextChannelManager chat = GameAccess.Chat;
            Transform player = chat != null ? chat.MainPlayer : null;
            if (!ShouldShow(chat, player) || !EnsureVisuals())
            {
                HideVisuals();
                return;
            }

            AreaSettings settings = CurrentSettings();
            Vector3 feet = player.position + Vector3.down * PivotHeight(player);
            _area.SetVisible(true);
            _area.Tick(feet, settings);

            if (HighlightPlayers.Value)
            {
                PlayersInRange.Find(player, settings.Radius, _playersInRange);
                _markers.Show(_playersInRange, settings);
            }
            else
            {
                _markers.HideAll();
            }
        }

        bool ShouldShow(TextChannelManager chat, Transform player)
        {
            if (!Enabled.Value || chat == null || player == null)
                return false;
            switch (Mode.Value)
            {
                case VisibilityMode.LocalTabOnly:
                    return chat.Islocal;
                case VisibilityMode.WhileTypingLocal:
                    return chat.Islocal && GameAccess.IsChatInputFocused();
                default:
                    return true;
            }
        }

        /// <summary>
        /// Height of the player's root above the ground under a standing character. The game compares root
        /// positions, so the sphere around your root reaches standing players whose feet are within the
        /// radius of your feet.
        /// </summary>
        float PivotHeight(Transform player)
        {
            if (_pivotOwner != player)
            {
                _pivotOwner = player;
                _pivotController = player.GetComponentInChildren<CharacterController>(true);
                _pivotHeight = 0f;
            }
            if (_pivotController != null && _pivotController.enabled && _pivotController.gameObject.activeInHierarchy)
            {
                float height = player.position.y - _pivotController.bounds.min.y + _pivotController.skinWidth;
                _pivotHeight = Mathf.Clamp(height, 0f, 3f);
            }
            return _pivotHeight;
        }

        AreaSettings CurrentSettings()
        {
            return new AreaSettings
            {
                Radius = Radius,
                Segments = _segments.Value,
                Rings = _rings.Value,
                RefineSteps = _refineSteps.Value,
                UpdateInterval = _updateInterval.Value,
                MaxStepUp = _maxStepUp.Value,
                SurfaceOffset = _surfaceOffset.Value,
                OutlineWidth = _outlineWidth.Value,
                LayerMask = _layerMask
            };
        }

        bool EnsureVisuals()
        {
            if (_visualsFailed)
                return false;
            if (_area != null && _area.IsAlive && _markers != null && _markers.IsAlive)
                return true;

            DestroyVisuals();
            // Surfaces a player can stand on: everything except characters, cosmetics, balls, invisible
            // barriers and helper volumes (layer names from the game's tag manager).
            _layerMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask(
                "Player", "PlayerRender", "NPC", "Customization", "IsGroundedChecker",
                "Area", "AreaChecker", "AreaTrigger", "AntiPlayer", "IgnorePlayer",
                "BasketBall", "BasketBallPlayer", "BasketBoundaryLayer", "Umbrella", "Drawing",
                "UI", "Indicator", "Overlay", "PostProcess1", "PostProcess2");
            _area = new RangeArea();
            _markers = new PlayerMarkers();
            if (!_area.HasMaterials)
            {
                _visualsFailed = true;
                Log.LogError("No usable shader found; the area cannot be drawn.");
                DestroyVisuals();
                return false;
            }
            ApplyAppearance();
            Log.LogInfo("Area visuals created (shader: " + Materials.ShaderInUse + ")");
            return true;
        }

        void ApplyAppearance()
        {
            if (_area != null)
            {
                _area.ApplyAppearance(_fillColor.Value, _outlineColor.Value, _outlineOnTop.Value);
                _area.MarkDirty();
            }
            if (_markers != null)
                _markers.SetColor(_markerColor.Value);
        }

        void HideVisuals()
        {
            if (_area != null && _area.IsAlive)
                _area.SetVisible(false);
            if (_markers != null && _markers.IsAlive)
                _markers.HideAll();
        }

        void DestroyVisuals()
        {
            if (_area != null)
                _area.Destroy();
            if (_markers != null)
                _markers.Destroy();
            _area = null;
            _markers = null;
        }

        void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            ApplyAppearance();
        }

        internal void SetEnabled(bool enabled, bool notify)
        {
            Enabled.Value = enabled;
            if (!notify)
                return;
            string message = enabled ? "Local chat area: shown" : "Local chat area: hidden";
            if (enabled && Mode.Value != VisibilityMode.Always)
                message += " (" + Describe(Mode.Value) + ")";
            GameAccess.Notify(message);
        }

        internal string DescribeStatus()
        {
            string source = _autoDetectRadius.Value && _detectedRadius.HasValue ? "from game code" : "from config";
            string status = $"{(Enabled.Value ? "shown" : "hidden")}, mode: {Describe(Mode.Value)}, " +
                            $"radius: {Radius:0.##} m ({source}), hotkey: {ToggleKey.Value}";

            TextChannelManager chat = GameAccess.Chat;
            Transform player = chat != null ? chat.MainPlayer : null;
            if (player == null)
                return status;
            var inRange = new List<Vector3>();
            PlayersInRange.Find(player, Radius, inRange);
            return status + $", players in range: {inRange.Count}";
        }

        internal static string Describe(VisibilityMode mode)
        {
            switch (mode)
            {
                case VisibilityMode.LocalTabOnly:
                    return "only on the Local chat tab";
                case VisibilityMode.WhileTypingLocal:
                    return "only while typing in the Local tab";
                default:
                    return "always";
            }
        }

        void OnDestroy()
        {
            Config.SettingChanged -= OnSettingChanged;
            if (_harmony != null)
                _harmony.UnpatchSelf();
            DestroyVisuals();
        }
    }
}
