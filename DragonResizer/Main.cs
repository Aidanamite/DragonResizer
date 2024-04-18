using BepInEx;
using BepInEx.Configuration;
using ConfigTweaks;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using System.Globalization;
using Object = UnityEngine.Object;
using System.Collections.Concurrent;

namespace DragonResizer
{
    [BepInPlugin("com.aidanamite.DragonResizer", "Dragon Resizer", "1.0.1")]
    [BepInDependency("com.aidanamite.ConfigTweaks")]
    public class Main : BaseUnityPlugin
    {
        static Main instance;
        [ConfigField]
        public static Dictionary<string, Vector3> DragonScales = new Dictionary<string, Vector3>();
        [ConfigField]
        public static Dictionary<string, Vector3> DragonAgeScales = new Dictionary<string, Vector3>();
        public void Awake()
        {
            isMain = true;
            instance = this;
            new Harmony("com.aidanamite.DragonResizer").PatchAll();
            Config.ConfigReloaded += (x,y) => UpdateAllScales();
            Logger.LogInfo("Loaded");
        }
        public static bool UpdateBoneScales(SanctuaryPet pet, bool force, bool save)
        {
            var name = pet.GetTypeSettings()._Name;
            var age = name + "-" + pet.pData.pStage.ToString();
            var flag = force;
            var changes = false;
            if (!DragonScales.ContainsKey(name))
            {
                flag = true;
                DragonScales[name] = Vector3.one;
                if (save)
                    instance.Config.Save();
                changes = true;
            }
            if (!DragonAgeScales.ContainsKey(age))
            {
                flag = true;
                DragonAgeScales[age] = Vector3.one;
                if (save)
                    instance.Config.Save();
                changes = true;
            }
            if (flag)
                foreach (var petAgeBoneData in pet.pCurAgeData._BoneInfo)
                    if (petAgeBoneData._BoneName.Contains("Root"))
                    {
                        var s = petAgeBoneData._Scale;
                        s.Scale(DragonScales[name]);
                        s.Scale(DragonAgeScales[age]);
                        pet.SetBoneScale0(petAgeBoneData._BoneName, s);
                    }
            return changes;
        }
        [ThreadStatic]
        static bool isMain = false;
        static ConcurrentQueue<Action> runOnMain = new ConcurrentQueue<Action>();
        void Update()
        {
            while (runOnMain.TryDequeue(out var a))
                try { a(); } catch (Exception e) { Logger.LogError(e); }
        }

        public static void UpdateAllScales()
        {
            if (!isMain)
            {
                runOnMain.Enqueue(UpdateAllScales);
                return;
            }
            var changed = false;
            foreach (var p in Resources.FindObjectsOfTypeAll<SanctuaryPet>())
                if (p.gameObject.activeInHierarchy)
                    if (UpdateBoneScales(p, true, false))
                        changed = true;
            if (changed)
                instance.Config.Save();
        }
        

        static Main()
        {
            if (!TomlTypeConverter.CanConvert(typeof(Dictionary<string, Vector3>)))
            TomlTypeConverter.AddConverter(typeof(Dictionary<string, Vector3>), new TypeConverter()
            {
                ConvertToObject = (str, type) =>
                {
                    var d = new Dictionary<string, Vector3>();
                    if (str == null)
                        return d;
                    var split = str.Split('|');
                    foreach (var i in split)
                        if (i.Length != 0)
                        {
                            var parts = i.Split(',');
                            if (parts.Length != 4)
                                Debug.LogWarning($"Could not load entry \"{i}\". Entries must have exactly 4 values divided by commas");
                            else
                            {
                                if (d.ContainsKey(parts[0]))
                                    Debug.LogWarning($"Duplicate entry name \"{parts[0]}\" from \"{i}\". Only last entry will be kept");
                                var vector = new Vector3();
                                for (int j = 0; j < 3; j++)
                                    if (parts[j + 1].Length == 0)
                                        vector[j] = 0;
                                    else if (float.TryParse(parts[j + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                                        vector[j] = v;
                                    else
                                        Debug.LogWarning($"Value \"{parts[j + 1]}\" in \"{i}\". Could not be parsed as a number");
                                d[parts[0]] = vector;
                            }
                        }
                    return d;
                },
                ConvertToString = (obj, type) =>
                {
                    if (!(obj is Dictionary<string, Vector3> d))
                        return "";
                    var str = new StringBuilder();
                    var k = d.Keys.ToList();
                    k.Sort();
                    foreach (var key in k)
                    {
                        if (str.Length > 0)
                            str.Append("|");
                        str.Append(key);
                        for (int i = 0; i < 3; i++)
                        {
                            str.Append(",");
                            str.Append(d[key][i].ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    return str.ToString();
                }
            });
        }
    }

    [HarmonyPatch(typeof(SanctuaryPet),"SetAge")]
    static class Patch_SetPetAge
    {
        static void Postfix(SanctuaryPet __instance) => Main.UpdateBoneScales(__instance, false, true);
    }
}