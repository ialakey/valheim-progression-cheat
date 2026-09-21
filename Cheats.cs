using System;
using HarmonyLib;

namespace ProgressionCheat
{
    /// <summary>
    /// The always-on cheats: immortality, endless stamina and eitr, and flight.
    ///
    /// Each one is two halves. A Harmony prefix stops the game from ever spending the
    /// resource, and <see cref="Enforce"/> tops the bar back up once a frame so anything
    /// that writes the value behind the patch's back (Character.UseHealth, drowning) is
    /// covered too. Flight is the exception: the game already has a working flight mode,
    /// so the toggle only has to keep Player.m_debugFly at the state the menu asks for.
    /// </summary>
    internal static class Cheats
    {
        private static ProgressionCheatPlugin _plugin;

        private static bool _immortal;
        private static bool _infiniteStamina;
        private static bool _infiniteEitr;
        private static bool _fly;

        public static void Init(ProgressionCheatPlugin plugin)
        {
            _plugin = plugin;
            _immortal = plugin.CfgImmortal.Value;
            _infiniteStamina = plugin.CfgInfiniteStamina.Value;
            _infiniteEitr = plugin.CfgInfiniteEitr.Value;
            _fly = false;
        }

        // The three sustained cheats are written back to the config so they survive a
        // restart. Flight is deliberately not: starting a session already airborne is a
        // surprise, not a convenience.

        public static bool Immortal
        {
            get { return _immortal; }
            set
            {
                _immortal = value;
                if (_plugin != null)
                    _plugin.CfgImmortal.Value = value;
            }
        }

        public static bool InfiniteStamina
        {
            get { return _infiniteStamina; }
            set
            {
                _infiniteStamina = value;
                if (_plugin != null)
                    _plugin.CfgInfiniteStamina.Value = value;
            }
        }

        public static bool InfiniteEitr
        {
            get { return _infiniteEitr; }
            set
            {
                _infiniteEitr = value;
                if (_plugin != null)
                    _plugin.CfgInfiniteEitr.Value = value;
            }
        }

        public static bool Fly
        {
            get { return _fly; }
            set { _fly = value; }
        }

        /// <summary>
        /// True when the cheat applies to this character - which is only ever the one
        /// sitting at this keyboard. Other players and every creature are left alone.
        /// </summary>
        private static bool IsLocal(Character character)
        {
            return character != null && (object)character == (object)Player.m_localPlayer;
        }

        /// <summary>
        /// Called once a frame while a character is in the world.
        /// </summary>
        public static void Enforce(Player player)
        {
            // A corpse is not something to top up: the ragdoll and the respawn screen are
            // the game's business until the character is back on its feet.
            if (player == null || player.IsDead())
                return;

            if (_immortal)
            {
                float max = player.GetMaxHealth();
                if (player.GetHealth() < max)
                    player.SetHealth(max);
            }

            if (_infiniteStamina)
            {
                float max = player.GetMaxStamina();
                if (player.GetStamina() < max)
                    player.AddStamina(max);
            }

            if (_infiniteEitr)
            {
                float max = player.GetMaxEitr();
                if (max > 0f && player.GetEitr() < max)
                    player.AddEitr(max);
            }

            // ToggleDebugFly() is the only public way in, and it does more than flip the
            // field - it also writes straight into the ZDO, which is not there yet while
            // the character is still spawning. So flip it only when the state disagrees
            // with the toggle and the ZDO is actually live.
            if (_fly != player.InDebugFlyMode())
            {
                ZNetView nview = player.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid())
                    player.ToggleDebugFly();
            }
        }

        // ------------------------------------------------------------------
        // patches
        // ------------------------------------------------------------------

        public static void ApplyPatches(Harmony harmony)
        {
            // Every source of damage funnels through Character.ApplyDamage - weapons,
            // falls, burning, poison, smoke. Character.Damage/RPC_Damage sits in front of
            // it but the status effects skip straight to this one, so this is the single
            // point that catches all of them.
            Patches.Patch(harmony,
                AccessTools.Method(typeof(Character), "ApplyDamage",
                    new Type[] { typeof(HitData), typeof(bool), typeof(bool), typeof(HitData.DamageModifier) }),
                typeof(Cheats), "ApplyDamagePrefix", null);

            Patches.Patch(harmony,
                AccessTools.Method(typeof(Player), "UseStamina", new Type[] { typeof(float) }),
                typeof(Cheats), "UseStaminaPrefix", null);

            Patches.Patch(harmony,
                AccessTools.Method(typeof(Player), "UseEitr", new Type[] { typeof(float) }),
                typeof(Cheats), "UseEitrPrefix", null);
        }

        public static bool ApplyDamagePrefix(Character __instance)
        {
            return !(_immortal && IsLocal(__instance));
        }

        public static bool UseStaminaPrefix(Player __instance)
        {
            return !(_infiniteStamina && IsLocal(__instance));
        }

        public static bool UseEitrPrefix(Player __instance)
        {
            return !(_infiniteEitr && IsLocal(__instance));
        }
    }
}
