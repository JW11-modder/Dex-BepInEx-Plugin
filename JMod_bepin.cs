#define NOINPUTSYSTEM
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JModder
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class JMod : BaseUnityPlugin
    {
        public const string GUID = "jw11-modder.JMod";
        public const string NAME = "JMod_bepin";
        public const string AUTHOR = "jw11-modder";
        public const string VERSION = "1.0.0";

        public static BaseUnityPlugin Instance { get; private set; }
        public static ConfigEntry<bool> JModEnabled;
        public static new ManualLogSource Logger;
        private static BaseUnityPlugin jPlugin;
        private static bool showCheatsPopup = false;
        private static bool isConfigLoaded = false;

        private static string confFileName;
        private static ConfigFile configFile;

        private static GUIStyle JModStyleT = new GUIStyle();
        private static GUIStyle JModStyleH = new GUIStyle();
        private static GUIStyle JModStyleP = new GUIStyle();
        private static GUIStyle JModStylePV = new GUIStyle();
        private static GUIStyle JModStyleB = new GUIStyle();
        private static GUIStyle JModStyleS = new GUIStyle();
        private static GUIStyle JModStyleST = new GUIStyle();

        private static Color JModColor = new Color(0.0f, 0.85f, 0.85f);

        private static Texture2D consoleBackground = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);

        private static Texture2D sliderBackground = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);

        private enum ConfigCategory
        {
            Toggles,
            MultFloat,
            MultInt
        }

        private static List<List<ConfigDefinition>> configCategoryList = new List<List<ConfigDefinition>>();

        private static List<string> configCategoryNameList = new List<string>();

        public static ConfigEntry<KeyCode> configMenuToggle;

        private static CursorLockMode lastLockMode;
        private static bool lastVisibleState;

#if !NOINPUTSYSTEM
        public static Key KeycodeToKey(KeyCode keyCode)
        {
            foreach (Key key in System.Enum.GetValues(typeof(Key)))
            {
                if (key.ToString().ToLower() == keyCode.ToString().ToLower())
                {
                    return key;
                }
            }

            LogWarning("can't find Key matching KeyCode " + keyCode.ToString());
            return Key.None;
        }
#endif

        public static void LogHandler(string log, LogType level)
        {
            switch (level)
            {
                case LogType.Log:
                    Logger.LogMessage(log);
                    return;
                case LogType.Warning:
                case LogType.Assert:
                    Logger.LogWarning(log);
                    return;
                case LogType.Exception:
                case LogType.Error:
                    Logger.LogError(log);
                    return;
            }
        }
        public static void Log( object message)
            => Log( message, LogType.Log);

        public static void LogWarning( object message)
            => Log( message, LogType.Warning);

        public static void LogError( object message)
            => Log( message, LogType.Error);

        internal static void Log( object message, LogType logType)
        {
            string log = message?.ToString() ?? "";

            switch (logType)
            {
                case LogType.Log:
                case LogType.Assert:
                    Logger.LogMessage(log); break;

                case LogType.Warning:
                    Logger.LogWarning(log); break;

                case LogType.Error:
                case LogType.Exception:
                    Logger.LogError(log); break;
            }
        }

        private static IEnumerable<ConfigSettingEntry> GetPluginConfig(BaseUnityPlugin plugin)
        {
            return plugin.Config.Select(kvp => new ConfigSettingEntry(kvp.Value, plugin));
        }


        public static void ConfFileInit()
        {
            configCategoryList.Clear();
            configCategoryNameList.Clear();

            BaseUnityPlugin[] plugins = Chainloader.PluginInfos.Values.Select(x => x.Instance)
                  .Where(plugin => plugin != null)
                  .Union(UnityEngine.Object.FindObjectsOfType(typeof(BaseUnityPlugin)).Cast<BaseUnityPlugin>())
                  .ToArray();

            foreach (var plugin in plugins)
            {
                try
                {
                    var type = plugin.GetType();
                    var pluginInfo = plugin.Info.Metadata;
                    var pluginName = pluginInfo?.Name ?? plugin.GetType().FullName;
                    var pluginGUID = pluginInfo.GUID;
                    if (pluginInfo.GUID == GUID)
                        continue;
                    if (pluginInfo.GUID.StartsWith("jw11-modder"))
                    {
                        var detected = new List<SettingEntryBase>();

                        detected.AddRange(GetPluginConfig(plugin).Cast<SettingEntryBase>());

                        Log("Plugin search result: " + pluginGUID);

                        jPlugin = plugin;
                    }
                }
                catch (Exception ex)
                {
                    string pluginName = plugin?.Info?.Metadata?.Name ?? plugin?.GetType().FullName;
                    LogError($"Failed to collect settings of the following plugin: {pluginName}");
                    LogError(ex);
                }
            }

            foreach (ConfigCategory category in Enum.GetValues(typeof(ConfigCategory)))
            {
                configCategoryNameList.Add(Enum.GetNames(typeof(ConfigCategory))[(int)category]);
                configCategoryList.Add(new List<ConfigDefinition>());
                Log("Category: " + configCategoryNameList[(int)category] + " added!");
            }

            foreach (ConfigDefinition definition in jPlugin.Config.Keys)
            {

                if (!configCategoryNameList.Contains(definition.Section))
                {
                    configCategoryNameList.Add(definition.Section);
                    configCategoryList.Add(new List<ConfigDefinition>());
                    Log("Custom category: " + definition.Section + " added!");
                }

                configCategoryList[configCategoryNameList.IndexOf(definition.Section)].Add(definition);
                Log("Definition: " + definition.ToString() + " from section " + definition.Section + " added!");
            }

            isConfigLoaded = true;
            Log("JMod Config Init complete!");
        }

        public void Awake()
        {
            Logger = base.Logger;
            Log("Starting JMod init!");
            Instance = this;
            base.enabled = true;

            JModEnabled = Config.Bind("JMod", "Mod Enabled", true, "Enable config mod GUI");

            configMenuToggle = Config.Bind("JMod", "MenuToggle", KeyCode.F7, "Main menu toggle key");

            JModStyleH.alignment = TextAnchor.MiddleCenter;
            JModStyleH.fontSize = 20;
            JModStyleH.fontStyle = FontStyle.Bold;
            JModStyleH.normal.textColor = JModColor;

            JModStyleP.fontSize = 16;
            JModStyleP.normal.textColor = JModColor;

            JModStylePV.fontSize = 16;
            JModStylePV.fontStyle = FontStyle.Bold;
            JModStylePV.normal.textColor = JModColor;

            JModStyleB.alignment = TextAnchor.UpperCenter;
            JModStyleB.fontSize = 24;
            JModStyleB.fontStyle = FontStyle.Bold;
            JModStyleB.normal.textColor = JModColor;
            JModStyleB.normal.background = consoleBackground;

            consoleBackground.SetPixel(0, 0, new Color(0.003f, 0.003f, 0.01f, 0.92f));
            consoleBackground.Apply();

            sliderBackground.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.05f));
            sliderBackground.Apply();


            Log("JMod Init complete!");
            if (!JModEnabled.Value)
                Log("GUI disabled!");
            else
                Log("Menu key: " + configMenuToggle.Value.ToString());
        }

        public void OnGUI()
        {
            if (!isConfigLoaded)
                ConfFileInit();
            if (showCheatsPopup)
                ShowMenu();
        }

        public void Update()
        {
            if (Input.GetKeyUp(configMenuToggle.Value) && JModEnabled.Value)
            {
                LogWarning("GUI Toggled!");
                SwitchMenu();
            }

        }

        public static void SwitchMenu()
        {
            if (!showCheatsPopup)
            {
                showCheatsPopup = !showCheatsPopup;
                lastLockMode = Cursor.lockState;
                lastVisibleState = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                showCheatsPopup = !showCheatsPopup;
                Cursor.lockState = lastLockMode;
                Cursor.visible = lastVisibleState;
                jPlugin.Config.Save();
            }
        }

        public static void CreateTogglesMenu(ref int xAxis, ref int yAxis, int catIndex)
        {
            foreach (ConfigDefinition definition in configCategoryList[catIndex])
            {
                jPlugin.Config[definition].BoxedValue = GUI.Toggle(new Rect(xAxis, yAxis, 400, 20), (bool)jPlugin.Config[definition].BoxedValue, jPlugin.Config[definition].Description.Description, JModStyleT);
                xAxis += 405;
                if (xAxis > 800)
                {
                    xAxis = 20;
                    yAxis += 25;
                }
            }
            if (xAxis != 20)
            {
                xAxis = 20;
                yAxis += 25;
            }
            yAxis += 10;
        }

        public static void CreateMultFloatMenu(ref int xAxis, ref int yAxis, int catIndex)
        {
            foreach (ConfigDefinition definition in configCategoryList[catIndex])
            {
                string multLabel = jPlugin.Config[definition].Description.Description;
                AcceptableValueRange<float> minMaxValues = (AcceptableValueRange<float>)jPlugin.Config[definition].Description.AcceptableValues;
                var minValue = minMaxValues.MinValue;
                var maxValue = minMaxValues.MaxValue;
                multLabel += " (" + minValue.ToString() + " - " + maxValue.ToString() + ")";
                GUI.Label(new Rect(xAxis, yAxis, 780, 20), multLabel, JModStyleP);
                float value = (float)jPlugin.Config[definition].BoxedValue;
                GUI.Label(new Rect(xAxis + 780, yAxis, 40, 20), value.ToString("0.0"), JModStylePV);
                yAxis += 25;
                jPlugin.Config[definition].BoxedValue = GUI.HorizontalSlider(new Rect(xAxis, yAxis, 810, 20), value, minValue, maxValue, JModStyleS, JModStyleST);
                yAxis += 15;
            }
        }

        public static void CreateMultIntMenu(ref int xAxis, ref int yAxis, int catIndex)
        {
            foreach (ConfigDefinition definition in configCategoryList[catIndex])
            {
                string multLabel = jPlugin.Config[definition].Description.Description;
                AcceptableValueRange<int> minMaxValues = (AcceptableValueRange<int>)jPlugin.Config[definition].Description.AcceptableValues;

                var minValue = minMaxValues.MinValue;
                var maxValue = minMaxValues.MaxValue;
                multLabel += " (" + minValue.ToString() + " - " + maxValue.ToString() + ")";
                GUI.Label(new Rect(xAxis, yAxis, 780, 20), multLabel, JModStyleP);
                int value = (int)jPlugin.Config[definition].BoxedValue;
                GUI.Label(new Rect(xAxis + 780, yAxis, 40, 20), value.ToString(), JModStylePV);
                yAxis += 25;
                jPlugin.Config[definition].BoxedValue = (int)GUI.HorizontalSlider(new Rect(xAxis, yAxis, 810, 20), value, minValue, maxValue, JModStyleS, JModStyleST);
                yAxis += 15;
            }
        }

        public static void ShowMenu()
        {
            if (showCheatsPopup)
            {
                var yAxis = 40;
                var xAxis = 20;

                JModStyleT = GUI.skin.GetStyle("toggle");
                JModStyleT.fontSize = 16;
                JModStyleT.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                JModStyleT.onNormal.textColor = JModColor;

                JModStyleS = GUI.skin.GetStyle("horizontalslider");
                JModStyleS.normal.background = sliderBackground;

                JModStyleST = GUI.skin.GetStyle("horizontalsliderthumb");

                GUI.BeginGroup(new Rect(Screen.width / 2 - 425, Screen.height / 2 - 425, 850, 850));
                GUI.Box(new Rect(0, 0, 850, 850), "MOD OPTIONS", JModStyleB);

                GUI.Label(new Rect(xAxis, yAxis, 810, 20), "Toggle Mod Options", JModStyleH);
                yAxis += 35;

                CreateTogglesMenu(ref xAxis, ref yAxis, 0);

                GUI.Label(new Rect(xAxis, yAxis, 810, 20), "Multiplier Mod Options", JModStyleH);
                yAxis += 25;

                CreateMultFloatMenu(ref xAxis, ref yAxis, 1);

                CreateMultIntMenu(ref xAxis, ref yAxis, 2);

                yAxis += 15;
                /*foreach (MelonPreferences_Category category in CustomCategoryList)
                {
                    GUI.Label(new Rect(xAxis, yAxis, 810, 20), category.DisplayName, JModStyleH);
                    yAxis += 35;
                    if (category.Identifier.EndsWith("Int"))
                    {
                        foreach (MelonPreferences_Entry<int> entry in category.Entries)
                        {
                            string multLabel = entry.DisplayName;
                            ValueRange<int> range = (ValueRange<int>)entry.Validator;
                            multLabel += " (" + range.MinValue.ToString() + " - " + range.MaxValue.ToString() + ") " + entry.Value.ToString();
                            GUI.Label(new Rect(xAxis, yAxis, 780, 20), multLabel, JModStyleP);
                            GUI.Label(new Rect(xAxis + 780, yAxis, 40, 20), entry.Value.ToString(), JModStylePV);
                            yAxis += 25;
                            entry.Value = (int)GUI.HorizontalSlider(new Rect(xAxis, yAxis, 810, 20), (float)entry.Value, (float)range.MinValue, (float)range.MaxValue, JModStyleS, JModStyleST);
                            yAxis += 15;
                        }
                    }
                    yAxis += 25;
                }*/
                if (GUI.Button(new Rect(325, 810, 200, 35),"Save settings and close"))
                {
                    SwitchMenu();
                }
                GUI.EndGroup();
            }
        }

    }
}
