using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.ModOptions;
using BTD_Mod_Helper.Extensions;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Towers;
using Il2CppAssets.Scripts.Models.Towers.Behaviors.Abilities;
using Il2CppAssets.Scripts.Simulation.Towers;
using Il2CppAssets.Scripts.Simulation.Towers.Behaviors.Abilities;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(UnlimitedAbilities_NoPatch_v2.Main), "Unlimited Abilities (No Patch NoCD) v2", "2.0.0", "Myself")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace UnlimitedAbilities_NoPatch_v2
{
    public class Main : BloonsTD6Mod
    {
        public static readonly ModSettingBool StartEnabled = new(true)
        {
            displayName = "Enable No-Cooldown (default)",
            description = "Initial default; press hotkey to toggle during play.",
            button = true,
            enabledText = "ON",
            disabledText = "OFF"
        };

        public static readonly ModSettingHotkey ToggleKey = new(UnityEngine.KeyCode.F8)
        {
            displayName = "Toggle Hotkey",
            description = "Press to toggle No-Cooldown ON/OFF."
        };

        private static bool _enabled;

        public override void OnApplicationStart()
        {
            _enabled = StartEnabled;
            MelonLogger.Msg($"[NoPatch NoCD v2] Startup: enabled={_enabled}");
        }

        public override void OnUpdate()
        {
            if (ToggleKey.JustPressed())
            {
                _enabled = !_enabled;
                MelonLogger.Msg($"[NoPatch NoCD v2] {(_enabled ? "ENABLED" : "DISABLED")}");
            }
        }

        public override void OnNewGameModel(GameModel model)
        {
            if (model == null) return;
            var towers = model.towers;
            if (towers == null) return;

            for (int t = 0; t < towers.Count; t++)
            {
                var tm = towers[t];
                if (tm == null) continue;

                var abilities = tm.GetBehaviors<AbilityModel>();
                if (abilities == null || abilities.Count == 0) continue;

                for (int i = 0; i < abilities.Count; i++)
                {
                    var a = abilities[i];
                    a.cooldown = 0f;
                    a.maxActivationsPerRound = -1;
                }
            }

            MelonLogger.Msg("[NoPatch NoCD v2] Model pass: cooldown=0, per-round=unlimited.");
        }

        public override void OnAbilityCast(Ability ability)
        {
            if (!_enabled || ability == null) return;
            try { ability.cooldownTimeRemaining = 0f; }
            catch (global::System.Exception ex) { MelonLogger.Warning($"[NoPatch NoCD v2] OnAbilityCast zero failed: {ex.Message}"); }
        }

        public override void OnTowerModelChanged(Tower tower, Model newModel)
        {
            ZeroNowMaybeNextFrame(tower);
        }

        public override void OnTowerUpgraded(Tower tower, string upgradeName, TowerModel newModel)
        {
            if (newModel != null)
            {
                var abilities = newModel.GetBehaviors<AbilityModel>();
                if (abilities != null && abilities.Count > 0)
                {
                    for (int i = 0; i < abilities.Count; i++)
                    {
                        var a = abilities[i];
                        a.cooldown = 0f;
                        a.maxActivationsPerRound = -1;
                    }
                }
            }
            ZeroNowMaybeNextFrame(tower);
        }

        private static void ZeroNowMaybeNextFrame(Tower tower)
        {
            if (!_enabled || tower == null) return;

            bool changed = TryZeroCooldowns(tower);
            if (changed)
            {
                MelonCoroutines.Start(ZeroNextFrameOnce(tower));
            }
        }

        private static System.Collections.IEnumerator ZeroNextFrameOnce(Tower tower)
        {
            yield return null; // next frame
            TryZeroCooldowns(tower);
        }

        private static bool TryZeroCooldowns(Tower tower)
        {
            try
            {
                var entity = (tower != null) ? tower.entity : null;
                if (entity == null) return false;

                var enumerable = entity.GetBehaviors<Il2CppAssets.Scripts.Simulation.Towers.Behaviors.Abilities.Ability>();
                if (enumerable == null) return false;

                bool changed = false;

                // Generic enumerator + non-generic cast for MoveNext()
                global::Il2CppSystem.Collections.Generic.IEnumerator<Il2CppAssets.Scripts.Simulation.Towers.Behaviors.Abilities.Ability> gen = enumerable.GetEnumerator();
                var it = (global::Il2CppSystem.Collections.IEnumerator)(object)gen;

                while (it != null && it.MoveNext())
                {
                    var ab = gen.Current;
                    if (ab == null) continue;

                    if (ab.cooldownTimeRemaining > 0f)
                    {
                        ab.cooldownTimeRemaining = 0f;
                        changed = true;
                    }
                }

                return changed;
            }
            catch (global::System.Exception ex)
            {
                MelonLogger.Warning($"[NoPatch NoCD v2] TryZeroCooldowns failed: {ex.Message}");
                return false;
            }
        }
    }
}
