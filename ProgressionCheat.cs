using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ProgressionCheat
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class ProgressionCheatPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "alakey.valheim.progressioncheat";
        public const string PluginName = "Progression Cheat";
        public const string PluginVersion = "2.1.0";

        public static ProgressionCheatPlugin Instance;

        // --- config ---
        public ConfigEntry<string> CfgMenuKey;
        public ConfigEntry<float> CfgUiScale;
        public ConfigEntry<int> CfgDefaultAmount;
        public ConfigEntry<bool> CfgKnownOnly;
        public ConfigEntry<bool> CfgDropIfFull;
        public ConfigEntry<bool> CfgMarkCheated;
        public ConfigEntry<bool> CfgShowUnavailable;
        public ConfigEntry<bool> CfgDisableSkillCap;
        public ConfigEntry<bool> CfgImmortal;
        public ConfigEntry<bool> CfgInfiniteStamina;
        public ConfigEntry<bool> CfgInfiniteEitr;

        private Key[] _menuCombo;
        private CheatMenu _menu;

        public bool MenuOpen { get; private set; }

        private void Awake()
        {
            Instance = this;

            CfgMenuKey = Config.Bind("Hotkeys", "ToggleMenu", "F6",
                "Key (or combo like LeftShift+F6) that opens and closes the cheat menu. Names come from UnityEngine.InputSystem.Key.");

            CfgUiScale = Config.Bind("UI", "Scale", 0f,
                "Menu scale. 0 = pick automatically from the screen height. Raise it if the text is too small.");

            CfgDefaultAmount = Config.Bind("Items", "DefaultAmount", 50,
                "Amount pre-filled in the menu when the game starts.");
            CfgKnownOnly = Config.Bind("Items", "KnownOnly", true,
                "Start with the menu filtered to items this character has already picked up at least once.");
            CfgDropIfFull = Config.Bind("Items", "DropIfInventoryFull", false,
                "false = refuse to grant an item when the inventory is full. true = drop the overflow on the ground.");
            CfgMarkCheated = Config.Bind("Items", "MarkAsCheated", false,
                "Tag granted items with the cheated flag the game uses for console spawns. Off by default so they behave like normal loot.");

            CfgShowUnavailable = Config.Bind("Items", "ShowUnavailable", false,
                "Start with the menu showing items the game normally keeps out of reach - quest items and items from content this install does not have.");

            CfgImmortal = Config.Bind("Cheats", "Immortal", false,
                "Block every source of damage and keep health full. The menu writes this back when you toggle it.");
            CfgInfiniteStamina = Config.Bind("Cheats", "InfiniteStamina", false,
                "Never spend stamina. The menu writes this back when you toggle it.");
            CfgInfiniteEitr = Config.Bind("Cheats", "InfiniteEitr", false,
                "Never spend eitr. The menu writes this back when you toggle it.");

            CfgDisableSkillCap = Config.Bind("Skills", "DisableSkillCap", true,
                "Valheim can enforce a total-skill cap that rebalances (lowers) other skills. Keep this on, or the levels you pick will not stick.");

            _menuCombo = ParseCombo(CfgMenuKey.Value, Key.F6);
            Cheats.Init(this);
            _menu = new CheatMenu(this);

            Patches.Apply();

            Logger.LogInfo(string.Format("{0} {1} loaded. Menu key: {2}",
                PluginName, PluginVersion, CfgMenuKey.Value));
        }

        private void Update()
        {
            if (ComboPressed(_menuCombo))
                SetMenuOpen(!MenuOpen);

            // Nothing below matters until a character is actually in the world.
            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            Cheats.Enforce(player);

            if (CfgDisableSkillCap.Value)
            {
                Skills skills = player.GetSkills();
                if (skills != null && skills.m_useSkillCap)
                    skills.m_useSkillCap = false;
            }
        }

        public void SetMenuOpen(bool open)
        {
            if (MenuOpen == open)
                return;

            MenuOpen = open;
            if (open)
                _menu.OnOpen();
        }

        private void OnGUI()
        {
            if (!MenuOpen)
                return;

            float scale = CfgUiScale.Value;
            if (scale <= 0f)
                scale = Mathf.Clamp(Screen.height / 1080f, 1f, 2.5f);

            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            _menu.Draw();
            GUI.matrix = previous;
        }

        public new BepInEx.Logging.ManualLogSource Logger
        {
            get { return base.Logger; }
        }

        // ------------------------------------------------------------------
        // hotkey parsing
        // ------------------------------------------------------------------

        private Key[] ParseCombo(string raw, Key fallback)
        {
            List<Key> keys = new List<Key>();
            if (!string.IsNullOrEmpty(raw))
            {
                string[] parts = raw.Split('+');
                for (int i = 0; i < parts.Length; i++)
                {
                    string name = parts[i].Trim();
                    if (name.Length == 0)
                        continue;
                    try
                    {
                        keys.Add((Key)Enum.Parse(typeof(Key), name, true));
                    }
                    catch (Exception)
                    {
                        base.Logger.LogWarning("Unknown key in config: " + name);
                    }
                }
            }
            if (keys.Count == 0)
            {
                base.Logger.LogWarning(string.Format("Could not parse hotkey [{0}], falling back to {1}", raw, fallback));
                keys.Add(fallback);
            }
            return keys.ToArray();
        }

        // Every key but the last must be held down; the last one triggers on the frame it goes down.
        private static bool ComboPressed(Key[] combo)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || combo == null || combo.Length == 0)
                return false;

            for (int i = 0; i < combo.Length - 1; i++)
            {
                KeyControl modifier = keyboard[combo[i]];
                if (modifier == null || !modifier.isPressed)
                    return false;
            }

            KeyControl trigger = keyboard[combo[combo.Length - 1]];
            return trigger != null && trigger.wasPressedThisFrame;
        }
    }

    /// <summary>
    /// While the menu is open the player must not walk, swing or look around, and the
    /// cursor has to stay free. Both are decided by the game every frame, so they are
    /// patched rather than merely overwritten.
    /// </summary>
    internal static class Patches
    {
        private static bool MenuOpen
        {
            get
            {
                return ProgressionCheatPlugin.Instance != null && ProgressionCheatPlugin.Instance.MenuOpen;
            }
        }

        /// <summary>
        /// True only when the menu is up and a character is actually in the world, so
        /// nothing is faked on the main menu.
        /// </summary>
        private static bool Blocking
        {
            get { return MenuOpen && Player.m_localPlayer != null; }
        }

        public static void Apply()
        {
            Harmony harmony = new Harmony(ProgressionCheatPlugin.PluginGuid);

            // The one lever that matters. Player.TakeInput() looks like the obvious hook
            // but nothing in the game calls it - the live gate is PlayerController, and
            // nearly everything that must go quiet while a window is up (player input,
            // camera rotation, cursor lock, hotbar, chat, minimap) keys off this one
            // method. Reporting the inventory as open makes the whole game behave as if
            // a normal GUI were in front of the player.
            Patch(harmony, AccessTools.Method(typeof(InventoryGui), "IsVisible", new Type[0]),
                "InventoryVisiblePrefix", null);

            // PlayerController.TakeInput skips the InventoryGui check while a gamepad is
            // active, so close that path too.
            Patch(harmony, AccessTools.Method(typeof(PlayerController), "TakeInput", new Type[] { typeof(bool) }),
                null, "TakeInputPostfix");

            // Belt and braces for the cursor: it must stay free even if something else
            // decides to re-lock it.
            Patch(harmony, AccessTools.Method(typeof(GameCamera), "UpdateMouseCapture", new Type[0]),
                "MouseCapturePrefix", null);

            // Immortality, endless stamina and eitr live in their own class but share
            // the same Harmony instance and the same missing-target handling.
            Cheats.ApplyPatches(harmony);
        }

        private static void Patch(Harmony harmony, MethodInfo target, string prefix, string postfix)
        {
            Patch(harmony, target, typeof(Patches), prefix, postfix);
        }

        /// <summary>
        /// Shared by this class and <see cref="Cheats"/>. A missing target is logged and
        /// skipped rather than thrown: one signature drifting in a game update should cost
        /// that one feature, not the whole plugin.
        /// </summary>
        internal static void Patch(Harmony harmony, MethodInfo target, Type owner, string prefix, string postfix)
        {
            if (target == null)
            {
                ProgressionCheatPlugin.Instance.Logger.LogWarning(
                    string.Format("Patch target not found ({0}.{1}/{2}) - that feature will not work",
                        owner.Name, prefix, postfix));
                return;
            }

            harmony.Patch(target,
                prefix == null ? null : new HarmonyMethod(owner.GetMethod(prefix, BindingFlags.Static | BindingFlags.Public)),
                postfix == null ? null : new HarmonyMethod(owner.GetMethod(postfix, BindingFlags.Static | BindingFlags.Public)));
        }

        public static bool InventoryVisiblePrefix(ref bool __result)
        {
            if (!Blocking)
                return true;

            __result = true;
            return false;
        }

        public static void TakeInputPostfix(ref bool __result)
        {
            if (Blocking)
                __result = false;
        }

        public static bool MouseCapturePrefix()
        {
            if (!Blocking)
                return true;

            ZCursor.LockState = CursorLockMode.None;
            ZCursor.Show();
            return false;
        }
    }
}
