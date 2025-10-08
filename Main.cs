using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.ModOptions;
using Il2CppAssets.Scripts.Simulation.Towers.Behaviors.Abilities;
using MelonLoader;
using UnityEngine;
using HarmonyLib;

[assembly: MelonInfo(typeof(UnlimitedAbilities.Main), "Unlimited Abilities (No Cooldown)", "5.0.0", "Myself")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace UnlimitedAbilities
{
    public class Main : BloonsTD6Mod
    {
        [HarmonyPatch(typeof(Ability), nameof(Ability.Process))]
        internal static class Ability_Process
        {
            [HarmonyPrefix]
            internal static void Prefix(Ability __instance)
            {
                if (NoCoolDown)
                {
                    __instance.CooldownRemaining = 0;
                    __instance.activationsThisRound = 0;
                }
            }
        }
        
        public static readonly ModSettingBool NoCoolDown = new(true)
        {
            displayName = "No Ability Cooldown",
            button = true,
            enabledText = "ON",
            disabledText = "OFF"
        };

        public static readonly ModSettingHotkey ToggleKey = new(KeyCode.F9)
        {
            displayName = "Toggle NoCoolDown ON/OFF",
            description = "Toggle No Cooldown for Abilities ON/OFF"
        };

        public override void OnUpdate()
        {
            if (ToggleKey.JustPressed())
            {
                NoCoolDown.SetValueAndSave(!NoCoolDown);
            }
        }

    }
}
