using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.ModOptions;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Towers;
using Il2CppAssets.Scripts.Simulation.Towers;
using Il2CppAssets.Scripts.Simulation.Towers.Behaviors.Abilities;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[assembly: MelonInfo(typeof(UnlimitedAbilities_NoPatch_v3.Main), "Unlimited Abilities (NoPatch) v3", "3.0.0", "Myself")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace UnlimitedAbilities_NoPatch_v3
{
    // ===================== NON-EXTENSION MATERIALIZER (NO foreach) =====================
    internal static class EnumerableHelpers
    {
        // Converts Il2CppSystem.Collections.Generic.IEnumerable<T> -> managed T[] safely.
        // Order of attempts:
        //  1) Count + indexer Item[int] (list-like)
        //  2) Length + indexer/get_Item (array-like)
        //  3) Reflected enumerator loop (MoveNext + Current) — NO C# foreach (avoids CS0202/CS0117)
        public static T[] ToArray<T>(global::Il2CppSystem.Collections.Generic.IEnumerable<T> src)
        {
            if (src == null) return Array.Empty<T>();
            object o = src;
            var type = o.GetType();

            // ---- Fast path: Count + indexer ----
            try
            {
                var countProp = type.GetProperty("Count", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (countProp != null)
                {
                    PropertyInfo itemProp = null;
                    foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (p.Name != "Item") continue;
                        var idx = p.GetIndexParameters();
                        if (idx != null && idx.Length == 1 && idx[0].ParameterType == typeof(int))
                        {
                            itemProp = p;
                            break;
                        }
                    }

                    if (itemProp != null)
                    {
                        int n = (int)(countProp.GetValue(o) ?? 0);
                        if (n <= 0) return Array.Empty<T>();
                        var arr = new T[n];
                        for (int i = 0; i < n; i++)
                        {
                            var val = itemProp.GetValue(o, new object[] { i });
                            arr[i] = val is T tv ? tv : (val == null ? default : (T)val);
                        }
                        return arr;
                    }
                }
            }
            catch { /* fall through */ }

            // ---- Fast path: Length + indexer/get_Item ----
            try
            {
                var lengthProp = type.GetProperty("Length", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (lengthProp != null)
                {
                    var getItem = type.GetMethod("get_Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int) }, null)
                               ?? type.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int) }, null);
                    int n = (int)(lengthProp.GetValue(o) ?? 0);
                    if (n <= 0) return Array.Empty<T>();
                    if (getItem != null)
                    {
                        var arr = new T[n];
                        for (int i = 0; i < n; i++)
                        {
                            var val = getItem.Invoke(o, new object[] { i });
                            arr[i] = val is T tv ? tv : (val == null ? default : (T)val);
                        }
                        return arr;
                    }
                }
            }
            catch { /* fall through */ }

            // ---- Reflected enumerator (NO foreach) ----
            try
            {
                var getEnum = type.GetMethod("GetEnumerator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (getEnum != null)
                {
                    var e = getEnum.Invoke(o, null);
                    if (e != null)
                    {
                        var et = e.GetType();
                        var moveNext = et.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var current = et.GetProperty("Current", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (moveNext != null && current != null)
                        {
                            var list = new List<T>(16);
                            while ((bool)moveNext.Invoke(e, null))
                            {
                                var val = current.GetValue(e);
                                list.Add(val is T tv ? tv : (val == null ? default : (T)val));
                            }
                            return list.ToArray();
                        }
                    }
                }
            }
            catch { /* return empty as last resort */ }

            return Array.Empty<T>();
        }
    }

    public class Main : BloonsTD6Mod
    {
        // Toggle + runtime enable
        public static readonly ModSettingBool StartEnabled = new(true)
        {
            displayName = "Enable No-Cooldown on launch",
            description = "Default ON. Use hotkey to toggle."
        };
        public static readonly ModSettingHotkey ToggleKey = new(KeyCode.F8)
        {
            displayName = "Toggle No-Cooldown",
            description = "Press to toggle No-Cooldown during play."
        };
        private static bool _enabled;

        // Time-based per-tower burst after upgrades/model changes (seconds, not frames)
        private const float UnlockBurstSeconds = 12.0f; // covers your observed 5–6s
        private static readonly Dictionary<Tower, float> _burstSeconds = new();

        public override void OnApplicationStart()
        {
            _enabled = StartEnabled;
            MelonLogger.Msg($"[NoPatch NoCD v3] Startup: enabled={_enabled}");
        }

        public override void OnUpdate()
        {
            if (ToggleKey.JustPressed())
            {
                _enabled = !_enabled;
                MelonLogger.Msg($"[NoPatch NoCD v3] {(_enabled ? "ENABLED" : "DISABLED")}");
            }

            if (!_enabled || _burstSeconds.Count == 0) return;

            // Process bursts in seconds (deltaTime accounts for game speed)
            var keys = new List<Tower>(_burstSeconds.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var t = keys[i];
                if (t == null) { _burstSeconds.Remove(t); continue; }

                if (!_burstSeconds.TryGetValue(t, out var timeLeft) || timeLeft <= 0f)
                {
                    _burstSeconds.Remove(t);
                    continue;
                }

                TryZeroCooldowns(t);
                timeLeft -= Time.deltaTime; // or Time.unscaledDeltaTime if you prefer
                if (timeLeft <= 0f) _burstSeconds.Remove(t);
                else _burstSeconds[t] = timeLeft;
            }
        }

        // Always zero after any ability activates (keeps it at 0 post-usage)
        public override void OnAbilityCast(Ability ability)
        {
            if (!_enabled || ability == null) return;
            try
            {
                if (ability.cooldownTimeRemaining > 0f)
                    ability.cooldownTimeRemaining = 0f;
            }
            catch (global::System.Exception ex)
            {
                MelonLogger.Warning($"[NoPatch NoCD v3] OnAbilityCast zero failed: {ex.Message}");
            }
        }

        // Upgrade/model-change: zero now + start a time-based burst to catch late resets
        public override void OnTowerModelChanged(Tower tower, Model newModel)
        {
            TryZeroCooldowns(tower);
            StartBurst(tower, UnlockBurstSeconds);
        }

        public override void OnTowerUpgraded(Tower tower, string upgradeName, TowerModel newModel)
        {
            TryZeroCooldowns(tower);
            StartBurst(tower, UnlockBurstSeconds);
        }

        private static void StartBurst(Tower tower, float seconds)
        {
            if (!_enabled || tower == null) return;
            if (_burstSeconds.TryGetValue(tower, out var left))
            {
                if (seconds > left) _burstSeconds[tower] = seconds;
            }
            else
            {
                _burstSeconds[tower] = seconds;
            }
        }

        // Core: zero the *simulation* abilities on a tower
        private static bool TryZeroCooldowns(Tower tower)
        {
            try
            {
                var entity = tower != null ? tower.entity : null;
                if (entity == null) return false;

                var abilitiesEnum = entity.GetBehaviors<Ability>(); // SIM side
                if (abilitiesEnum == null) return false;

                var abilities = EnumerableHelpers.ToArray(abilitiesEnum); // NON-EXTENSION, no foreach
                if (abilities.Length == 0) return false;

                bool changed = false;
                for (int i = 0; i < abilities.Length; i++)
                {
                    var ab = abilities[i];
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
                MelonLogger.Warning($"[NoPatch NoCD v3] TryZeroCooldowns failed: {ex.Message}");
                return false;
            }
        }
    }
}
