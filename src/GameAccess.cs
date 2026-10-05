using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LocalChatRange
{
    /// <summary>
    /// Cached, exception-safe access to the game objects the mod reads.
    /// The game's singleton getters fall back to FindAnyObjectByType when the instance is missing
    /// (e.g. in the main menu), so lookups of missing instances are throttled.
    /// </summary>
    internal static class GameAccess
    {
        const float LookupInterval = 1f;

        static TextChannelManager _chat;
        static UIManager _ui;
        static PlayerPanelController _playerPanel;
        static float _nextChatLookup;
        static float _nextUiLookup;
        static float _nextPlayerPanelLookup;

        static readonly AccessTools.FieldRef<TextChannelManager, TMP_Text> TextPrefabRef =
            TryFieldRef<TMP_Text>("_textPrefab");
        static readonly AccessTools.FieldRef<TextChannelManager, List<GameObject>> LocalMessagesRef =
            TryFieldRef<List<GameObject>>("_messageObjectsLocal");

        public static TextChannelManager Chat
        {
            get
            {
                if (_chat == null && Time.unscaledTime >= _nextChatLookup)
                {
                    _nextChatLookup = Time.unscaledTime + LookupInterval;
                    _chat = NetworkSingleton<TextChannelManager>.I;
                }
                return _chat;
            }
        }

        public static UIManager UI
        {
            get
            {
                if (_ui == null && Time.unscaledTime >= _nextUiLookup)
                {
                    _nextUiLookup = Time.unscaledTime + LookupInterval;
                    _ui = MonoSingleton<UIManager>.I;
                }
                return _ui;
            }
        }

        public static PlayerPanelController PlayerPanel
        {
            get
            {
                if (_playerPanel == null && Time.unscaledTime >= _nextPlayerPanelLookup)
                {
                    _nextPlayerPanelLookup = Time.unscaledTime + LookupInterval;
                    _playerPanel = NetworkSingleton<PlayerPanelController>.I;
                }
                return _playerPanel;
            }
        }

        /// <summary>True while the chat message field has keyboard focus.</summary>
        public static bool IsChatInputFocused()
        {
            UIManager ui = UI;
            if (ui == null)
                return false;
            TMP_InputField input = ui.MessageInput;
            if (input != null && input.isFocused)
                return true;
            TMP_InputField field = ui.MessageInputField;
            return field != null && field.isFocused;
        }

        /// <summary>True while any text field has keyboard focus (chat, journal, to-do list...).</summary>
        public static bool IsAnyTextFieldFocused()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null)
                return false;
            TMP_InputField tmpInput = selected.GetComponent<TMP_InputField>();
            if (tmpInput != null && tmpInput.isFocused)
                return true;
            InputField legacyInput = selected.GetComponent<InputField>();
            return legacyInput != null && legacyInput.isFocused;
        }

        /// <summary>
        /// Reads the local chat distance from the game's own code instead of trusting a hard-coded copy.
        /// TextChannelManager.OnChannelMessageReceived drops local messages when
        /// <c>Vector3.SqrMagnitude(senderPosition - MainPlayer.position) &gt; 25f</c>,
        /// so the constant loaded right after the SqrMagnitude call is the squared radius.
        /// </summary>
        public static float? DetectRadiusFromGameCode()
        {
            MethodInfo method = AccessTools.Method(typeof(TextChannelManager), "OnChannelMessageReceived");
            if (method == null)
                return null;
            MethodBody body = method.GetMethodBody();
            byte[] il = body != null ? body.GetILAsByteArray() : null;
            if (il == null)
                return null;

            const byte OpCall = 0x28;
            const byte OpLdcR4 = 0x22;
            for (int i = 0; i + 10 <= il.Length; i++)
            {
                if (il[i] != OpCall || il[i + 5] != OpLdcR4)
                    continue;

                MethodBase callee;
                try
                {
                    callee = method.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1));
                }
                catch (Exception)
                {
                    continue;
                }
                if (callee == null || callee.Name != "SqrMagnitude" || callee.DeclaringType != typeof(Vector3))
                    continue;

                float squaredRadius = BitConverter.ToSingle(il, i + 6);
                if (squaredRadius > 0f && squaredRadius < 1e6f)
                    return Mathf.Sqrt(squaredRadius);
            }
            return null;
        }

        /// <summary>
        /// Shows a client-side line in the chat tab the player is currently looking at.
        /// Nothing is sent to other players.
        /// </summary>
        public static void Notify(string message)
        {
            TextChannelManager chat = Chat;
            if (chat == null)
                return;

            string text = "<color=#7FD8FF>[LocalChatRange]</color> " + message;
            try
            {
                if (chat.Islocal && TryAddLocalLine(chat, text))
                    return;
                chat.AddNotification(text);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not show chat notification: " + e.Message);
            }
        }

        /// <summary>Mirrors TextChannelManager.AddNotification, but for the Local tab.</summary>
        static bool TryAddLocalLine(TextChannelManager chat, string text)
        {
            if (TextPrefabRef == null)
                return false;
            UIManager ui = UI;
            Transform parent = ui != null ? ui.TextContentLocalTransform : null;
            TMP_Text prefab = TextPrefabRef(chat);
            if (parent == null || prefab == null)
                return false;

            TMP_Text line = UnityEngine.Object.Instantiate(prefab, parent);
            Button button = line.GetComponent<Button>();
            if (button != null)
                button.interactable = false;
            line.text = text;

            // Register the line with the game so its normal history limit trims it later.
            List<GameObject> messages = LocalMessagesRef != null ? LocalMessagesRef(chat) : null;
            if (messages != null)
            {
                messages.Add(line.gameObject);
                int limit = ScriptableSingleton<GameSettings>.I.LocalMessageLimitCount;
                while (messages.Count > limit && messages.Count > 0)
                {
                    GameObject oldest = messages[0];
                    messages.RemoveAt(0);
                    if (oldest != null)
                        UnityEngine.Object.Destroy(oldest);
                }
            }
            return true;
        }

        static AccessTools.FieldRef<TextChannelManager, T> TryFieldRef<T>(string fieldName)
        {
            try
            {
                return AccessTools.FieldRefAccess<TextChannelManager, T>(fieldName);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
