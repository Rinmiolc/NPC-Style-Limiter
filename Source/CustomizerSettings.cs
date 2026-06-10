// Copyright (c) 2026 rinmiolc
// Licensed under the GNU General Public License v3.0.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Verse;
using RimWorld;

namespace NPCStyleLimiter
{
    public class RaceSettings : IExposable
    {
        public bool useSpecificConfig = false;
        public bool useGenderConfig = false;
        public bool adjustGenderRatio = false;
        public float maleRatio = 0.5f;

        public Dictionary<string, float> weights = new Dictionary<string, float>();
        public Dictionary<string, float> weightsMale = new Dictionary<string, float>();
        public Dictionary<string, float> weightsFemale = new Dictionary<string, float>();

        // Runtime caches
        public readonly float[] fastWeights = new float[CustomizerSettings.MaxFastIndex];
        public readonly float[] fastWeightsMale = new float[CustomizerSettings.MaxFastIndex];
        public readonly float[] fastWeightsFemale = new float[CustomizerSettings.MaxFastIndex];

        public RaceSettings()
        {
            ResetCaches();
        }

        public void ResetCaches()
        {
            for (int i = 0; i < CustomizerSettings.MaxFastIndex; i++)
            {
                fastWeights[i] = 1f;
                fastWeightsMale[i] = 1f;
                fastWeightsFemale[i] = 1f;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref useSpecificConfig, "useSpecificConfig", false);
            Scribe_Values.Look(ref useGenderConfig, "useGenderConfig", false);
            Scribe_Values.Look(ref adjustGenderRatio, "adjustGenderRatio", false);
            Scribe_Values.Look(ref maleRatio, "maleRatio", 0.5f);

            Scribe_Collections.Look(ref weights, "weights", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref weightsMale, "weightsMale", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref weightsFemale, "weightsFemale", LookMode.Value, LookMode.Value);

            if (weights == null) weights = new Dictionary<string, float>();
            if (weightsMale == null) weightsMale = new Dictionary<string, float>();
            if (weightsFemale == null) weightsFemale = new Dictionary<string, float>();
        }
    }

    public class CustomizerSettings : ModSettings
    {
        public bool applyToPlayerPawns = false;
        public bool debugMode = false;
        public string currentProfileName = "Default";

        public Dictionary<string, RaceSettings> raceSettings = new Dictionary<string, RaceSettings>();
        public const string GlobalKey = "__Global";

        public RaceSettings GlobalSettings
        {
            get
            {
                if (raceSettings == null) raceSettings = new Dictionary<string, RaceSettings>();
                if (!raceSettings.TryGetValue(GlobalKey, out var s))
                {
                    s = new RaceSettings { useSpecificConfig = true }; // Global is always "specific"
                    raceSettings[GlobalKey] = s;
                }
                return s;
            }
        }

        public RaceSettings GetSettingsForRace(string raceDefName)
        {
            if (string.IsNullOrEmpty(raceDefName) || raceDefName == GlobalKey) return GlobalSettings;
            if (raceSettings != null && raceSettings.TryGetValue(raceDefName, out var s) && s.useSpecificConfig) return s;
            return GlobalSettings;
        }

        public RaceSettings GetSettingsForRaceRaw(string raceDefName)
        {
            if (string.IsNullOrEmpty(raceDefName)) return GlobalSettings;
            if (raceSettings == null) raceSettings = new Dictionary<string, RaceSettings>();
            if (!raceSettings.TryGetValue(raceDefName, out var s))
            {
                s = new RaceSettings();
                raceSettings[raceDefName] = s;
            }
            return s;
        }

        // Fast O(1) lookup constants
        public const int MaxFastIndex = 524288;

        public static int GetFastIndex(Def def)
        {
            if (def == null) return 0;
            int typeOffset = 0;
            if (def is HairDef) typeOffset = 0;
            else if (def is BeardDef) typeOffset = 16384;
            else if (def is BodyTypeDef) typeOffset = 32768;
            else if (def is ThingDef) typeOffset = 49152;
            
            int idx = typeOffset + def.index;
            if (idx >= MaxFastIndex) return 0;
            return idx;
        }

        // Legacy fields for migration
        private bool? legacy_useGenderConfig;
        private bool? legacy_adjustGenderRatio;
        private float? legacy_maleRatio;
        private Dictionary<string, float> legacy_weights;
        private Dictionary<string, float> legacy_weightsMale;
        private Dictionary<string, float> legacy_weightsFemale;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref applyToPlayerPawns, "applyToPlayerPawns", false);
            Scribe_Values.Look(ref debugMode, "debugMode", false);
            Scribe_Values.Look(ref currentProfileName, "currentProfileName", "Default");

            Scribe_Collections.Look(ref raceSettings, "raceSettings", LookMode.Value, LookMode.Deep);
            if (raceSettings == null) raceSettings = new Dictionary<string, RaceSettings>();

            // Legacy Loading
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref legacy_useGenderConfig, "useGenderConfig");
                Scribe_Values.Look(ref legacy_adjustGenderRatio, "adjustGenderRatio");
                Scribe_Values.Look(ref legacy_maleRatio, "maleRatio");
                Scribe_Collections.Look(ref legacy_weights, "weights", LookMode.Value, LookMode.Value);
                Scribe_Collections.Look(ref legacy_weightsMale, "weightsMale", LookMode.Value, LookMode.Value);
                Scribe_Collections.Look(ref legacy_weightsFemale, "weightsFemale", LookMode.Value, LookMode.Value);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (legacy_weights != null || legacy_useGenderConfig.HasValue)
                {
                    var g = GlobalSettings;
                    if (legacy_useGenderConfig.HasValue) g.useGenderConfig = legacy_useGenderConfig.Value;
                    if (legacy_adjustGenderRatio.HasValue) g.adjustGenderRatio = legacy_adjustGenderRatio.Value;
                    if (legacy_maleRatio.HasValue) g.maleRatio = legacy_maleRatio.Value;
                    if (legacy_weights != null) g.weights = legacy_weights;
                    if (legacy_weightsMale != null) g.weightsMale = legacy_weightsMale;
                    if (legacy_weightsFemale != null) g.weightsFemale = legacy_weightsFemale;
                    
                    // Clear legacy
                    legacy_weights = null; legacy_weightsMale = null; legacy_weightsFemale = null;
                }
                ResolveRuntimeWeights();
            }
        }

        public void ResolveRuntimeWeights()
        {
            if (raceSettings == null) return;
            foreach (var kvp in raceSettings)
            {
                var s = kvp.Value;
                if (s == null) continue;
                MigrateLegacyKeys(s.weights);
                MigrateLegacyKeys(s.weightsMale);
                MigrateLegacyKeys(s.weightsFemale);

                s.ResetCaches();
                ResolveToCaches(s);
            }
        }

        private void ResolveToCaches(RaceSettings s)
        {
            foreach (var kvp in s.weights) { Def d = FindDef(kvp.Key); if (d != null) s.fastWeights[GetFastIndex(d)] = kvp.Value; }
            foreach (var kvp in s.weightsMale) { Def d = FindDef(kvp.Key); if (d != null) s.fastWeightsMale[GetFastIndex(d)] = kvp.Value; }
            foreach (var kvp in s.weightsFemale) { Def d = FindDef(kvp.Key); if (d != null) s.fastWeightsFemale[GetFastIndex(d)] = kvp.Value; }
        }

        private bool MigrateLegacyKeys(Dictionary<string, float> dict)
        {
            if (dict == null) return false;
            List<string> legacyKeys = dict.Keys.Where(key => key != null && !key.Contains(":")).ToList();
            if (legacyKeys.Count == 0) return false;

            bool anyMigrated = false;
            foreach (var legacyKey in legacyKeys)
            {
                float val = dict[legacyKey];
                Def def = FindDef(legacyKey);
                if (def != null)
                {
                    dict.Remove(legacyKey);
                    dict[GetConfigKey(def)] = val;
                    anyMigrated = true;
                }
            }
            return anyMigrated;
        }

        public string GetConfigKey(Def def) => def == null ? null : def.GetType().Name + ":" + def.defName;

        public void InitializeSets()
        {
            if (raceSettings == null) raceSettings = new Dictionary<string, RaceSettings>();
            var g = GlobalSettings; // Ensure global exists
        }

        private Def FindDef(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            int colonIdx = defName.IndexOf(':');
            if (colonIdx >= 0)
            {
                string typeName = defName.Substring(0, colonIdx);
                string realDefName = defName.Substring(colonIdx + 1);
                if (typeName == nameof(HairDef)) return DefDatabase<HairDef>.GetNamedSilentFail(realDefName);
                if (typeName == nameof(BeardDef)) return DefDatabase<BeardDef>.GetNamedSilentFail(realDefName);
                if (typeName == nameof(ThingDef)) return DefDatabase<ThingDef>.GetNamedSilentFail(realDefName);
                if (typeName == nameof(BodyTypeDef)) return DefDatabase<BodyTypeDef>.GetNamedSilentFail(realDefName);
            }
            return (Def)DefDatabase<HairDef>.GetNamedSilentFail(defName) ?? DefDatabase<BeardDef>.GetNamedSilentFail(defName) ??
                   (Def)DefDatabase<ThingDef>.GetNamedSilentFail(defName) ?? DefDatabase<BodyTypeDef>.GetNamedSilentFail(defName);
        }

        public void ResetToDefaults()
        {
            if (currentProfileName != "Default")
            {
                if (!LoadProfile("Default"))
                {
                    raceSettings.Clear();
                    currentProfileName = "Default";
                    ResolveRuntimeWeights();
                }
            }
            else
            {
                raceSettings.Clear();
                ResolveRuntimeWeights();
            }
        }

        public float GetWeight(Def def, Gender gender, string raceDefName = null)
        {
            if (def == null) return 1.0f;
            var s = GetSettingsForRace(raceDefName);
            int idx = GetFastIndex(def);
            if (s.useGenderConfig && gender != Gender.None) return (gender == Gender.Female) ? s.fastWeightsFemale[idx] : s.fastWeightsMale[idx];
            return s.fastWeights[idx];
        }

        public void SetWeight(Def def, Gender gender, float weight, string raceDefName = null)
        {
            if (def == null) return;
            var s = GetSettingsForRaceRaw(raceDefName);
            string key = GetConfigKey(def);
            int idx = GetFastIndex(def);

            if (s.useGenderConfig)
            {
                if (gender == Gender.Female) { s.weightsFemale[key] = weight; s.fastWeightsFemale[idx] = weight; }
                else { s.weightsMale[key] = weight; s.fastWeightsMale[idx] = weight; }
            }
            else { s.weights[key] = weight; s.fastWeights[idx] = weight; }
        }

        public bool IsDisabled(Def def, Gender gender, string raceDefName = null) => (def != null && (def.defName == "Bald" || def.defName == "NoBeard")) ? false : GetWeight(def, gender, raceDefName) <= 0f;

        public static string ProfilesFolder => Path.Combine(GenFilePaths.ConfigFolderPath, "NPCStyleLimiter_Profiles");
        public static string GetProfilePath(string name) => Path.Combine(ProfilesFolder, SanitizeFileName(name) + ".xml");
        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unnamed";
            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = new string(name.Where(c => !invalid.Contains(c)).ToArray());
            return string.IsNullOrEmpty(safe) ? "unnamed" : safe;
        }

        public List<string> ListProfiles()
        {
            if (!Directory.Exists(ProfilesFolder)) return new List<string>();
            return Directory.GetFiles(ProfilesFolder, "*.xml").Select(f => Path.GetFileNameWithoutExtension(f)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public bool SaveProfile(string name)
        {
            if (name == "Default") return false;
            try
            {
                string safeName = SanitizeFileName(name);
                Directory.CreateDirectory(ProfilesFolder);
                XElement root = new XElement("NPCStyleLimiterProfile",
                    new XElement("profileName", safeName),
                    new XElement("version", "2"),
                    new XElement("applyToPlayerPawns", applyToPlayerPawns),
                    new XElement("debugMode", debugMode),
                    new XElement("raceSettings", raceSettings.Select(kv => SerializeRaceSettings(kv.Key, kv.Value)))
                );
                new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(GetProfilePath(safeName));
                currentProfileName = safeName;
                Write();
                return true;
            }
            catch (Exception e) { Log.Error("NPCStyleLimiter: Save failed: " + e.Message); return false; }
        }

        private XElement SerializeRaceSettings(string raceKey, RaceSettings s)
        {
            return new XElement("race",
                new XAttribute("defName", raceKey),
                new XElement("useSpecificConfig", s.useSpecificConfig),
                new XElement("useGenderConfig", s.useGenderConfig),
                new XElement("adjustGenderRatio", s.adjustGenderRatio),
                new XElement("maleRatio", s.maleRatio),
                new XElement("weights", s.weights.Select(kv => new XElement("entry", new XAttribute("key", kv.Key), new XAttribute("value", kv.Value)))),
                new XElement("weightsMale", s.weightsMale.Select(kv => new XElement("entry", new XAttribute("key", kv.Key), new XAttribute("value", kv.Value)))),
                new XElement("weightsFemale", s.weightsFemale.Select(kv => new XElement("entry", new XAttribute("key", kv.Key), new XAttribute("value", kv.Value))))
            );
        }

        public bool LoadProfile(string name)
        {
            try
            {
                string path = GetProfilePath(name);
                if (!File.Exists(path)) return false;
                XElement root = XDocument.Load(path).Root;
                if (root == null) return false;

                string version = (string)root.Element("version") ?? "1";
                applyToPlayerPawns = (bool?)root.Element("applyToPlayerPawns") ?? false;
                debugMode = (bool?)root.Element("debugMode") ?? false;

                raceSettings.Clear();
                if (version == "1")
                {
                    // Legacy profile load
                    var g = GlobalSettings;
                    g.useGenderConfig = (bool?)root.Element("useGenderConfig") ?? false;
                    g.adjustGenderRatio = (bool?)root.Element("adjustGenderRatio") ?? false;
                    g.maleRatio = (float?)root.Element("maleRatio") ?? 0.5f;
                    g.weights = ReadWeightDict(root.Element("weights"));
                    g.weightsMale = ReadWeightDict(root.Element("weightsMale"));
                    g.weightsFemale = ReadWeightDict(root.Element("weightsFemale"));
                }
                else
                {
                    var raceContainer = root.Element("raceSettings");
                    if (raceContainer != null)
                    {
                        foreach (var el in raceContainer.Elements("race"))
                        {
                            string defName = (string)el.Attribute("defName");
                            if (defName == null) continue;
                            var s = new RaceSettings();
                            s.useSpecificConfig = (bool?)el.Element("useSpecificConfig") ?? false;
                            s.useGenderConfig = (bool?)el.Element("useGenderConfig") ?? false;
                            s.adjustGenderRatio = (bool?)el.Element("adjustGenderRatio") ?? false;
                            s.maleRatio = (float?)el.Element("maleRatio") ?? 0.5f;
                            s.weights = ReadWeightDict(el.Element("weights"));
                            s.weightsMale = ReadWeightDict(el.Element("weightsMale"));
                            s.weightsFemale = ReadWeightDict(el.Element("weightsFemale"));
                            raceSettings[defName] = s;
                        }
                    }
                }

                InitializeSets(); ResolveRuntimeWeights();
                currentProfileName = name; Write();
                return true;
            }
            catch (Exception e) { Log.Error("NPCStyleLimiter: Load failed: " + e.Message); return false; }
        }

        private Dictionary<string, float> ReadWeightDict(XElement container) => container?.Elements("entry").ToDictionary(e => (string)e.Attribute("key"), e => (float)e.Attribute("value")) ?? new Dictionary<string, float>();

        public bool DeleteProfile(string name) { if (name == "Default" || name == currentProfileName) return false; try { string path = GetProfilePath(name); if (File.Exists(path)) File.Delete(path); return true; } catch { return false; } }
        public bool RenameProfile(string oldName, string newName)
        {
            if (oldName == "Default") return false;
            string safeNewName = SanitizeFileName(newName);
            try { string oldPath = GetProfilePath(oldName), newPath = GetProfilePath(safeNewName); if (!File.Exists(oldPath)) return false; if (File.Exists(newPath)) File.Delete(newPath); File.Move(oldPath, newPath);
                XDocument doc = XDocument.Load(newPath); if (doc.Root?.Element("profileName") != null) doc.Root.Element("profileName").Value = safeNewName; doc.Save(newPath);
                if (currentProfileName == oldName) { currentProfileName = safeNewName; Write(); } return true; } catch { return false; }
        }
    }
}
