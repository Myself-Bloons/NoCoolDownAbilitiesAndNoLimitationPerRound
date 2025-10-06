using System.Linq;
using System.Reflection;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Extensions; // GetDescendants<T>()
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Towers.Behaviors.Abilities;
using Il2CppAssets.Scripts.Simulation.Towers.Behaviors.Abilities;
using MelonLoader;


[assembly: MelonInfo(typeof(UnlimitedAbilities.Main), "Unlimited Abilities (No Cooldown)", "1.2.1", "PierreMartin")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace UnlimitedAbilities
{
    public class Main : BloonsTD6Mod
    {
        private HarmonyLib.Harmony _harmony;

        public override void OnApplicationStart()
        {
            _harmony = new HarmonyLib.Harmony("UnlimitedAbilities.NoCooldown.Runtime");
            PatchAbilityRuntime(_harmony);
        }

        // Model-side: unlimited uses + 0s in UI
        public override void OnNewGameModel(GameModel model)
        {
            foreach (var ability in model.GetDescendants<AbilityModel>().ToList())
            {
                ability.maxActivationsPerRound = 99999; // unlimited uses per round
                ability.cooldown = 0;               // UI shows 0s
            }
        }

        // Runtime: hook whatever "update-like" methods actually exist on Ability
        private static void PatchAbilityRuntime(HarmonyLib.Harmony h)
        {
            var abilityType = typeof(Ability);
            var post = new HarmonyLib.HarmonyMethod(
                typeof(Main).GetMethod(nameof(AfterAbilityUpdatePostfix), BindingFlags.Static | BindingFlags.NonPublic)
            );

            int count = 0;
            foreach (var m in abilityType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (m.IsSpecialName) continue;              // skip get_/set_
                if (m.ReturnType != typeof(void)) continue; // only void methods

                var n = m.Name;
                if (IsUpdateLike(n))
                {
                    h.Patch(m, postfix: post);
                    count++;
                }
            }

            MelonLogger.Msg($"[UnlimitedAbilities] Patched {count} Ability update-like methods.");
        }

        private static bool IsUpdateLike(string n) =>
            n.Equals("Update", System.StringComparison.OrdinalIgnoreCase) ||
            n.Equals("FixedUpdate", System.StringComparison.OrdinalIgnoreCase) ||
            n.Equals("OnUpdate", System.StringComparison.OrdinalIgnoreCase) ||
            n.Equals("Step", System.StringComparison.OrdinalIgnoreCase) ||
            n.IndexOf("Tick", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("Process", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("Simulate", System.StringComparison.OrdinalIgnoreCase) >= 0;

        // Postfix: zero the actual cooldown timers/markers on the Ability instance
        private static void AfterAbilityUpdatePostfix(Ability __instance)
        {
            TryZero(__instance, "cooldownTimeRemaining");
            TryZero(__instance, "remainingCooldown");
            TryZero(__instance, "cooldownTimer");
            TryZero(__instance, "cooldown");
            TryZero(__instance, "timeUntilReady");

            // “next activation” style fields (varies by build)
            TryZero(__instance, "nextActivateTime");
            TryZero(__instance, "nextActivation");
            TryZero(__instance, "nextActivateTick");
            TryZero(__instance, "lastActivatedAt");
            TryZero(__instance, "abilityCooldown");
            TryZero(__instance, "abilityCooldownRemaining");
        }

        private static void TryZero(object obj, string fieldName)
        {
            var f = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) return;

            var t = f.FieldType;
            if (t == typeof(float)) f.SetValue(obj, 0f);
            else if (t == typeof(double)) f.SetValue(obj, 0d);
            else if (t == typeof(int)) f.SetValue(obj, 0);
            else if (t == typeof(long)) f.SetValue(obj, 0L);
        }
    }
}

