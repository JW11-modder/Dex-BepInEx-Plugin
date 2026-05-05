using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using PixelCrushers.DialogueSystem.ChatMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using JModder;

namespace DexPlugin
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        public static BaseUnityPlugin Instance { get; private set; }

        private static ConfigEntry<bool> configNoPlayerDamage;
        private static ConfigEntry<bool> configInvisible;
        private static ConfigEntry<bool> configNoCooldown;
        private static ConfigEntry<bool> configNoSuitDamage;
        private static ConfigEntry<bool> configNoUpgradeCost;
        private static ConfigEntry<bool> configMaxEnergy;
        private static ConfigEntry<bool> configNoReload;
        private static ConfigEntry<bool> configUnlimitedAmmo;
        private static ConfigEntry<float> configPlayerDamageMult;

        private static ConfigEntry<int> configPlayerXPMult;
        private static ConfigEntry<int> configPlayerMoneyMult;

        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;

            Instance = this;

            configNoPlayerDamage = Config.Bind("Toggles", "NoPlayerDamage", false, "Disable incoming player damage");
            configInvisible = Config.Bind("Toggles", "Invisible", false, "Disable player detection by enemies");
            configNoCooldown = Config.Bind("Toggles", "NoCooldown", false, "Disable abilities cooldown");

            configNoSuitDamage = Config.Bind("Toggles", "NoSuitDamage", false, "Disable suit damage");
            configNoUpgradeCost = Config.Bind("Toggles", "NoUpgradeCost", false, "Disable upgrade cost");
            configMaxEnergy = Config.Bind("Toggles", "MaxEnergy", false, "Enable always max energy");
            configNoReload = Config.Bind("Toggles", "NoReload", false, "Enable no reload for player gun");
            configUnlimitedAmmo = Config.Bind("Toggles", "UnlimitedAmmo", false, "Enable unlimited ammo");
            configPlayerDamageMult = Config.Bind("MultFloat", "PlayerDamageMult", 1f, new ConfigDescription("Player Damage multiplier", new AcceptableValueRange<float>(1f, 20f)));
            
            configPlayerXPMult = Config.Bind("MultInt", "PlayerXPMult", 1, "Player XP multiplier");
            configPlayerMoneyMult = Config.Bind("MultInt", "PlayerMoneyMult", 1, "Player Money income multiplier");


            //JMod.ConfFileInit(MyPluginInfo.PLUGIN_GUID);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }

        //Player
        //this.characterMoveController.currentHitPoints = this.characterMoveController.maxHitPointsWithoutImplants;
        // m_MoveController public CharacterPlatformController characterMoveController
        //characterMoveController isPlayer 

        //public Weapon weaponObject - this.shooting.weapon
        //
        //Weapon bool shootPrimary(out bool empty, float recoilTimeReduction)
        //this.m_BulletsInCurrentClip -= 1f;

        [HarmonyPatch(typeof(CharacterPlatformController), nameof(CharacterPlatformController.hit))]
        class PlayerHitPatch1
        {
            static bool Prefix(CharacterPlatformController __instance, ref float damage)
            {
                if (!configNoPlayerDamage.Value)
                    return true;
                if (__instance.isPlayer)
                {
                    damage = 0;
                }
                return true;
            }
        }

        //configNoReload
        [HarmonyPatch(typeof(Weapon), nameof(Weapon.shootPrimary))]
        class ShootGunPatch1
        {
            static void Postfix(Weapon __instance, ref float ___m_BulletsInCurrentClip)
            {
                if (!configNoReload.Value)
                    return;
                Player player = DataManager.Instance.player;
                if (player.characterMoveController.shooting.weapon == __instance)
                {
                    Logger.LogInfo("Found player weapon: " + __instance.ToString());
                    ___m_BulletsInCurrentClip += 1;
                }
            }
        }


        //configPlayerDamageMult
        [HarmonyPatch(typeof(Weapon), nameof(Weapon.getDamage))]
        class GunDamagePatch1
        {
            static void Postfix(Weapon __instance, ref int __result)
            {
                if (configPlayerDamageMult.Value <=1)
                    return;
                Player player = DataManager.Instance.player;
                if (player.characterMoveController.shooting.weapon == __instance && __instance != null)
                {
                    Logger.LogInfo("Found player weapon: " + __instance.ToString());
                    __result = (int)(__result * configPlayerDamageMult.Value);
                }
            }
        }

        [HarmonyPatch(typeof(Weapon), nameof(Weapon.primaryDamage), MethodType.Getter)]
        class GunDamagePatch2
        {
            static void Postfix(Weapon __instance, ref float __result)
            {
                if (configPlayerDamageMult.Value <= 1)
                    return;
                Player player = DataManager.Instance.player;
                if (player.characterMoveController.shooting.weapon == __instance && __instance != null)
                {
                    Logger.LogInfo("Found player weapon: " + __instance.ToString());
                    __result = __result * configPlayerDamageMult.Value;
                }
            }
        }

        //configPlayerXPMult
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.addExp))]
        class PlayerXPPatch1
        {
            static bool Prefix(ref Inventory __instance, ref int newExp)
            {
                if (configPlayerXPMult.Value <= 1)
                    return true;
                Player player = DataManager.Instance.player;
                if (player.characterMoveController.inventory == __instance)
                {
                    newExp *= configPlayerXPMult.Value;
                    if (__instance.level >= 19)
                    {
                        __instance.m_Level = 18;
                    }
                }
                return true;
            }
        }

        //configPlayerMoneyMult
        [HarmonyPatch(typeof(Money), nameof(Money.setDropItem))]
        class PlayerMoneyPatch1
        {
            static bool Prefix(Money __instance)
            {
                if (configPlayerMoneyMult.Value <= 1)
                    return true;
                Player player = DataManager.Instance.player;
                if (player.characterMoveController.inventory == __instance)
                {
                    __instance.m_Value *= configPlayerMoneyMult.Value;
                }
                return true;
            }
        }



        //configInvisible

        //configMaxEnergy
        //ARHandler
        //ARScene.current.playerAREntity.ar.focus
        [HarmonyPatch(typeof(ARHandler), nameof(ARHandler.changeFocus))]
        class PlayerFocusPatch1
        {
            static bool Prefix(ARHandler __instance, ref float delta)
            {
                if (!configMaxEnergy.Value)
                    return true;
                Player player = DataManager.Instance.player;
                if (player.characterMoveController.ar == __instance && delta < 0)
                {
                    delta = 0;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(ARAvatar), nameof(ARAvatar.changeEnergy))]
        class PlayerFocusPatch2
        {
            static bool Prefix(ARAvatar __instance, ref float ___m_Energy)
            {
                if (!configMaxEnergy.Value)
                    return true;
                ___m_Energy = __instance.energyMax;
                return false;
            }
        }

    }
}
