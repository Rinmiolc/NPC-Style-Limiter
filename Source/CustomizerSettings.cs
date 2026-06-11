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

        // Runtime caches (dynamic sized)
        public float[] fastHairWeights;
        public float[] fastHairWeightsMale;
        public float[] fastHairWeightsFemale;

        public float[] fastBeardWeights;
        public float[] fastBeardWeightsMale;
        public float[] fastBeardWeightsFemale;

        public float[] fastApparelWeights;
        public float[] fastApparelWeightsMale;
        public float[] fastApparelWeightsFemale;

        public float[] fastHeadWeights;
        public float[] fastHeadWeightsMale;
        public float[] fastHeadWeightsFemale;

        public float[] fastBodyWeights;
        public float[] fastBodyWeightsMale;
        public float[] fastBodyWeightsFemale;

        // Apparel generation status cache (safety valve)
        public bool hasAnyApparelEnabled = true;
        public bool hasAnyApparelEnabledMale = true;
        public bool hasAnyApparelEnabledFemale = true;

        public RaceSettings()
        {
            ResetCaches();
        }

        public void ResetCaches()
        {
            if (CustomizerSettings.hairDefCount == 0) CustomizerSettings.InitializeDefCounts();

            InitializeArray(ref fastHairWeights, CustomizerSettings.hairDefCount);
            InitializeArray(ref fastHairWeightsMale, CustomizerSettings.hairDefCount);
            InitializeArray(ref fastHairWeightsFemale, CustomizerSettings.hairDefCount);

            InitializeArray(ref fastBeardWeights, CustomizerSettings.beardDefCount);
            InitializeArray(ref fastBeardWeightsMale, CustomizerSettings.beardDefCount);
            InitializeArray(ref fastBeardWeightsFemale, CustomizerSettings.beardDefCount);

            InitializeArray(ref fastApparelWeights, CustomizerSettings.apparelDefCount);
            InitializeArray(ref fastApparelWeightsMale, CustomizerSettings.apparelDefCount);
            InitializeArray(ref fastApparelWeightsFemale, CustomizerSettings.apparelDefCount);

            InitializeArray(ref fastHeadWeights, CustomizerSettings.headDefCount);
            InitializeArray(ref fastHeadWeightsMale, CustomizerSettings.headDefCount);
            InitializeArray(ref fastHeadWeightsFemale, CustomizerSettings.headDefCount);

            InitializeArray(ref fastBodyWeights, CustomizerSettings.bodyDefCount);
            InitializeArray(ref fastBodyWeightsMale, CustomizerSettings.bodyDefCount);
            InitializeArray(ref fastBodyWeightsFemale, CustomizerSettings.bodyDefCount);

            hasAnyApparelEnabled = true;
            hasAnyApparelEnabledMale = true;
            hasAnyApparelEnabledFemale = true;
        }

        private void InitializeArray(ref float[] arr, int size)
        {
            if (arr == null || arr.Length != size)
            {
                arr = new float[size];
            }
            for (int i = 0; i < size; i++)
            {
                arr[i] = 1f;
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

        // Dynamic Def counts
        public static int hairDefCount;
        public static int beardDefCount;
        public static int apparelDefCount;
        public static int headDefCount;
        public static int bodyDefCount;

        public static void InitializeDefCounts()
        {
            hairDefCount = DefDatabase<HairDef>.DefCount;
            beardDefCount = DefDatabase<BeardDef>.DefCount;
            apparelDefCount = DefDatabase<ThingDef>.DefCount;
            headDefCount = DefDatabase<HeadTypeDef>.DefCount;
            bodyDefCount = DefDatabase<BodyTypeDef>.DefCount;
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
            CustomizerMod.Instance?.ClearUICaches();
        }

        private void ResolveToCaches(RaceSettings s)
        {
            foreach (var kvp in s.weights)
            {
                Def d = FindDef(kvp.Key);
                if (d != null) SetFastWeight(s, d, Gender.None, kvp.Value);
            }
            foreach (var kvp in s.weightsMale)
            {
                Def d = FindDef(kvp.Key);
                if (d != null) SetFastWeight(s, d, Gender.Male, kvp.Value);
            }
            foreach (var kvp in s.weightsFemale)
            {
                Def d = FindDef(kvp.Key);
                if (d != null) SetFastWeight(s, d, Gender.Female, kvp.Value);
            }

            RefreshApparelSafetyCache(s);
        }

        private void SetFastWeight(RaceSettings s, Def d, Gender gender, float val)
        {
            int idx = d.index;
            float[] arr = null;

            if (d is HairDef) arr = (gender == Gender.None) ? s.fastHairWeights : ((gender == Gender.Male) ? s.fastHairWeightsMale : s.fastHairWeightsFemale);
            else if (d is BeardDef) arr = (gender == Gender.None) ? s.fastBeardWeights : ((gender == Gender.Male) ? s.fastBeardWeightsMale : s.fastBeardWeightsFemale);
            else if (d is ThingDef) arr = (gender == Gender.None) ? s.fastApparelWeights : ((gender == Gender.Male) ? s.fastApparelWeightsMale : s.fastApparelWeightsFemale);
            else if (d is HeadTypeDef) arr = (gender == Gender.None) ? s.fastHeadWeights : ((gender == Gender.Male) ? s.fastHeadWeightsMale : s.fastHeadWeightsFemale);
            else if (d is BodyTypeDef) arr = (gender == Gender.None) ? s.fastBodyWeights : ((gender == Gender.Male) ? s.fastBodyWeightsMale : s.fastBodyWeightsFemale);

            if (arr != null && idx >= 0 && idx < arr.Length)
            {
                arr[idx] = val;
            }
        }

        private void RefreshApparelSafetyCache(RaceSettings s)
        {
            s.hasAnyApparelEnabled = false;
            s.hasAnyApparelEnabledMale = false;
            s.hasAnyApparelEnabledFemale = false;

            if (s.fastApparelWeights == null) return;

            var allApparel = DefDatabase<ThingDef>.AllDefsListForReading;
            int count = allApparel.Count;
            for (int i = 0; i < count; i++)
            {
                if (s.hasAnyApparelEnabled && s.hasAnyApparelEnabledMale && s.hasAnyApparelEnabledFemale)
                {
                    break;
                }
                var def = allApparel[i];
                if (def != null && def.IsApparel)
                {
                    int idx = def.index;
                    if (idx >= 0 && idx < s.fastApparelWeights.Length)
                    {
                        if (s.fastApparelWeights[idx] > 0f) s.hasAnyApparelEnabled = true;
                    }
                    if (s.fastApparelWeightsMale != null && idx >= 0 && idx < s.fastApparelWeightsMale.Length)
                    {
                        if (s.fastApparelWeightsMale[idx] > 0f) s.hasAnyApparelEnabledMale = true;
                    }
                    if (s.fastApparelWeightsFemale != null && idx >= 0 && idx < s.fastApparelWeightsFemale.Length)
                    {
                        if (s.fastApparelWeightsFemale[idx] > 0f) s.hasAnyApparelEnabledFemale = true;
                    }
                }
            }
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
                if (typeName == nameof(HeadTypeDef)) return DefDatabase<HeadTypeDef>.GetNamedSilentFail(realDefName);
            }
            return (Def)DefDatabase<HairDef>.GetNamedSilentFail(defName) ??
                   (Def)DefDatabase<BeardDef>.GetNamedSilentFail(defName) ??
                   (Def)DefDatabase<ThingDef>.GetNamedSilentFail(defName) ??
                   (Def)DefDatabase<BodyTypeDef>.GetNamedSilentFail(defName) ??
                   (Def)DefDatabase<HeadTypeDef>.GetNamedSilentFail(defName);
        }

        public void ResetToDefaults()
        {
            if (!currentProfileName.Equals("Default", StringComparison.OrdinalIgnoreCase))
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
            int idx = def.index;
            
            float[] arr = null;
            float[] arrMale = null;
            float[] arrFemale = null;

            if (def is HairDef) { arr = s.fastHairWeights; arrMale = s.fastHairWeightsMale; arrFemale = s.fastHairWeightsFemale; }
            else if (def is BeardDef) { arr = s.fastBeardWeights; arrMale = s.fastBeardWeightsMale; arrFemale = s.fastBeardWeightsFemale; }
            else if (def is ThingDef) { arr = s.fastApparelWeights; arrMale = s.fastApparelWeightsMale; arrFemale = s.fastApparelWeightsFemale; }
            else if (def is HeadTypeDef) { arr = s.fastHeadWeights; arrMale = s.fastHeadWeightsMale; arrFemale = s.fastHeadWeightsFemale; }
            else if (def is BodyTypeDef) { arr = s.fastBodyWeights; arrMale = s.fastBodyWeightsMale; arrFemale = s.fastBodyWeightsFemale; }

            if (arr == null || idx < 0 || idx >= arr.Length) return 1.0f;

            if (s.useGenderConfig && gender != Gender.None)
            {
                if (gender == Gender.Female) return (arrFemale != null && idx < arrFemale.Length) ? arrFemale[idx] : 1.0f;
                return (arrMale != null && idx < arrMale.Length) ? arrMale[idx] : 1.0f;
            }
            return arr[idx];
        }

        private float[] GetGenderedFastArray(Def def, Gender gender, RaceSettings s)
        {
            if (def is HairDef) return (gender == Gender.None) ? s.fastHairWeights : ((gender == Gender.Male) ? s.fastHairWeightsMale : s.fastHairWeightsFemale);
            if (def is BeardDef) return (gender == Gender.None) ? s.fastBeardWeights : ((gender == Gender.Male) ? s.fastBeardWeightsMale : s.fastBeardWeightsFemale);
            if (def is ThingDef) return (gender == Gender.None) ? s.fastApparelWeights : ((gender == Gender.Male) ? s.fastApparelWeightsMale : s.fastApparelWeightsFemale);
            if (def is HeadTypeDef) return (gender == Gender.None) ? s.fastHeadWeights : ((gender == Gender.Male) ? s.fastHeadWeightsMale : s.fastHeadWeightsFemale);
            if (def is BodyTypeDef) return (gender == Gender.None) ? s.fastBodyWeights : ((gender == Gender.Male) ? s.fastBodyWeightsMale : s.fastBodyWeightsFemale);
            return null;
        }

        public void SetWeight(Def def, Gender gender, float weight, string raceDefName = null)
        {
            if (def == null) return;
            var s = GetSettingsForRaceRaw(raceDefName);
            string key = GetConfigKey(def);
            int idx = def.index;

            if (s.useGenderConfig)
            {
                if (gender == Gender.Female)
                {
                    s.weightsFemale[key] = weight;
                    var arr = GetGenderedFastArray(def, Gender.Female, s);
                    if (arr == null)
                    {
                        s.ResetCaches();
                        arr = GetGenderedFastArray(def, Gender.Female, s);
                    }
                    if (arr != null && idx >= 0 && idx < arr.Length) arr[idx] = weight;
                }
                else
                {
                    s.weightsMale[key] = weight;
                    var arr = GetGenderedFastArray(def, Gender.Male, s);
                    if (arr == null)
                    {
                        s.ResetCaches();
                        arr = GetGenderedFastArray(def, Gender.Male, s);
                    }
                    if (arr != null && idx >= 0 && idx < arr.Length) arr[idx] = weight;
                }
            }
            else
            {
                s.weights[key] = weight;
                var arr = GetGenderedFastArray(def, Gender.None, s);
                if (arr == null)
                {
                    s.ResetCaches();
                    arr = GetGenderedFastArray(def, Gender.None, s);
                }
                if (arr != null && idx >= 0 && idx < arr.Length) arr[idx] = weight;
            }

            if (def is ThingDef && ((ThingDef)def).IsApparel)
            {
                RefreshApparelSafetyCache(s);
            }
        }

        public bool IsDisabled(Def def, Gender gender, string raceDefName = null) => GetWeight(def, gender, raceDefName) <= 0f;

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
            return Directory.GetFiles(ProfilesFolder, "*.xml")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Where(n => !n.Equals("Default", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool SaveProfile(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Equals("Default", StringComparison.OrdinalIgnoreCase)) return false;
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

        private Dictionary<string, float> ReadWeightDict(XElement container)
        {
            var dict = new Dictionary<string, float>();
            if (container != null)
            {
                foreach (var e in container.Elements("entry"))
                {
                    string key = (string)e.Attribute("key");
                    string valStr = (string)e.Attribute("value");
                    if (key != null && valStr != null && float.TryParse(valStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
                    {
                        dict[key] = val;
                    }
                }
            }
            return dict;
        }

        public bool DeleteProfile(string name) { if (string.IsNullOrEmpty(name) || name.Equals("Default", StringComparison.OrdinalIgnoreCase) || name.Equals(currentProfileName, StringComparison.OrdinalIgnoreCase)) return false; try { string path = GetProfilePath(name); if (File.Exists(path)) File.Delete(path); return true; } catch { return false; } }
        public bool RenameProfile(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName) || oldName.Equals("Default", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrEmpty(newName) || newName.Equals("Default", StringComparison.OrdinalIgnoreCase)) return false;
            string safeNewName = SanitizeFileName(newName);
            try { string oldPath = GetProfilePath(oldName), newPath = GetProfilePath(safeNewName); if (!File.Exists(oldPath)) return false; if (File.Exists(newPath)) File.Delete(newPath); File.Move(oldPath, newPath);
                XDocument doc = XDocument.Load(newPath); if (doc.Root?.Element("profileName") != null) doc.Root.Element("profileName").Value = safeNewName; doc.Save(newPath);
                if (currentProfileName.Equals(oldName, StringComparison.OrdinalIgnoreCase)) { currentProfileName = safeNewName; Write(); } return true; } catch { return false; }
        }
    }
}
