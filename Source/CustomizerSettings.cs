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
    public class CustomizerSettings : ModSettings
    {
        public bool useGenderConfig = false;
        public bool adjustGenderRatio = false;
        public bool applyToPlayerPawns = false;
        public float maleRatio = 0.5f;
        public bool debugMode = false;
        public string currentProfileName = "Default";

        public Dictionary<string, float> weights = new Dictionary<string, float>();
        public Dictionary<string, float> weightsMale = new Dictionary<string, float>();
        public Dictionary<string, float> weightsFemale = new Dictionary<string, float>();

        public readonly Dictionary<Def, float> runtimeWeights = new Dictionary<Def, float>();
        public readonly Dictionary<Def, float> runtimeWeightsMale = new Dictionary<Def, float>();
        public readonly Dictionary<Def, float> runtimeWeightsFemale = new Dictionary<Def, float>();

        // Fast O(1) lookup arrays indexed by custom index mapped from def type and def.index
        private const int MaxFastIndex = 524288;
        private readonly float[] fastWeights = new float[MaxFastIndex];
        private readonly float[] fastWeightsMale = new float[MaxFastIndex];
        private readonly float[] fastWeightsFemale = new float[MaxFastIndex];

        public static int GetFastIndex(Def def)
        {
            if (def == null) return 0;
            int typeOffset = 0;
            if (def is HairDef) typeOffset = 0;
            else if (def is BeardDef) typeOffset = 16384;      // Up to 16k hairs
            else if (def is BodyTypeDef) typeOffset = 32768;    // Up to 16k body types
            else if (def is ThingDef) typeOffset = 49152;       // Up to 475k+ things
            
            int idx = typeOffset + def.index;
            if (idx >= MaxFastIndex) return 0; // Guard against overflow
            return idx;
        }

        public CustomizerSettings()
        {
            for (int i = 0; i < MaxFastIndex; i++)
            {
                fastWeights[i] = 1f;
                fastWeightsMale[i] = 1f;
                fastWeightsFemale[i] = 1f;
            }
        }

        // Legacy lists for backwards compatibility
        private List<string> disabledHairNames = null;
        private List<string> disabledBeardNames = null;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref useGenderConfig, "useGenderConfig", false);
            Scribe_Values.Look(ref adjustGenderRatio, "adjustGenderRatio", false);
            Scribe_Values.Look(ref applyToPlayerPawns, "applyToPlayerPawns", false);
            Scribe_Values.Look(ref maleRatio, "maleRatio", 0.5f);
            Scribe_Values.Look(ref debugMode, "debugMode", false);

            Scribe_Collections.Look(ref weights, "weights", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref weightsMale, "weightsMale", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref weightsFemale, "weightsFemale", LookMode.Value, LookMode.Value);

            Scribe_Values.Look(ref currentProfileName, "currentProfileName", "Default");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Collections.Look(ref disabledHairNames, "disabledHairNames", LookMode.Value);
                Scribe_Collections.Look(ref disabledBeardNames, "disabledBeardNames", LookMode.Value);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InitializeSets();
                if (disabledHairNames != null)
                {
                    foreach (var name in disabledHairNames) if (!string.IsNullOrEmpty(name)) weights[name] = 0f;
                    disabledHairNames = null;
                }
                if (disabledBeardNames != null)
                {
                    foreach (var name in disabledBeardNames) if (!string.IsNullOrEmpty(name)) weights[name] = 0f;
                    disabledBeardNames = null;
                }
                ResolveRuntimeWeights();
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
            if (weights == null) weights = new Dictionary<string, float>();
            if (weightsMale == null) weightsMale = new Dictionary<string, float>();
            if (weightsFemale == null) weightsFemale = new Dictionary<string, float>();
        }

        public void ResolveRuntimeWeights()
        {
            bool migrated = MigrateLegacyKeys(weights) || MigrateLegacyKeys(weightsMale) || MigrateLegacyKeys(weightsFemale);
            // Removed Write() call here as it triggers Scribe errors during mod initialization
            
            runtimeWeights.Clear(); runtimeWeightsMale.Clear(); runtimeWeightsFemale.Clear();
            ResolveDictionary(weights, runtimeWeights);
            ResolveDictionary(weightsMale, runtimeWeightsMale);
            ResolveDictionary(weightsFemale, runtimeWeightsFemale);

            for (int i = 0; i < MaxFastIndex; i++) { fastWeights[i] = 1f; fastWeightsMale[i] = 1f; fastWeightsFemale[i] = 1f; }
            foreach (var kvp in runtimeWeights) if (kvp.Key != null) fastWeights[GetFastIndex(kvp.Key)] = kvp.Value;
            foreach (var kvp in runtimeWeightsMale) if (kvp.Key != null) fastWeightsMale[GetFastIndex(kvp.Key)] = kvp.Value;
            foreach (var kvp in runtimeWeightsFemale) if (kvp.Key != null) fastWeightsFemale[GetFastIndex(kvp.Key)] = kvp.Value;
        }

        private void ResolveDictionary(Dictionary<string, float> source, Dictionary<Def, float> target)
        {
            if (source == null) return;
            foreach (var kvp in source)
            {
                Def def = FindDef(kvp.Key);
                if (def != null) target[def] = kvp.Value;
            }
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
            Def found = (Def)DefDatabase<HairDef>.GetNamedSilentFail(defName) ?? DefDatabase<BeardDef>.GetNamedSilentFail(defName);
            if (found != null) return found;
            return (Def)DefDatabase<ThingDef>.GetNamedSilentFail(defName) ?? DefDatabase<BodyTypeDef>.GetNamedSilentFail(defName);
        }

        public void ResetToDefaults()
        {
            if (currentProfileName != "Default")
            {
                if (!LoadProfile("Default"))
                {
                    ResetState();
                    currentProfileName = "Default";
                    ResolveRuntimeWeights();
                }
            }
            else
            {
                ResetState();
                ResolveRuntimeWeights();
            }
        }

        private void ResetState()
        {
            useGenderConfig = false; adjustGenderRatio = false; applyToPlayerPawns = false; maleRatio = 0.5f;
            weights.Clear(); weightsMale.Clear(); weightsFemale.Clear();
            runtimeWeights.Clear(); runtimeWeightsMale.Clear(); runtimeWeightsFemale.Clear();
            for (int i = 0; i < MaxFastIndex; i++) { fastWeights[i] = 1f; fastWeightsMale[i] = 1f; fastWeightsFemale[i] = 1f; }
        }

        public float GetWeight(Def def, Gender gender)
        {
            if (def == null) return 1.0f;
            int idx = GetFastIndex(def);
            if (useGenderConfig && gender != Gender.None) return (gender == Gender.Female) ? fastWeightsFemale[idx] : fastWeightsMale[idx];
            return fastWeights[idx];
        }

        public float GetWeight(string key, Gender gender)
        {
            if (useGenderConfig && gender != Gender.None)
            {
                var dict = (gender == Gender.Female) ? weightsFemale : weightsMale;
                if (dict != null && dict.TryGetValue(key, out float w)) return w;
            }
            else if (weights != null && weights.TryGetValue(key, out float w)) return w;
            return 1.0f;
        }

        public void SetWeight(Def def, Gender gender, float weight) { if (def != null) SetWeight(GetConfigKey(def), gender, weight); }

        public void SetWeight(string key, Gender gender, float weight)
        {
            InitializeSets();
            if (useGenderConfig) { if (gender == Gender.Female) weightsFemale[key] = weight; else weightsMale[key] = weight; }
            else weights[key] = weight;

            Def def = FindDef(key);
            if (def != null)
            {
                int idx = GetFastIndex(def);
                if (useGenderConfig) { if (gender == Gender.Female) { runtimeWeightsFemale[def] = weight; fastWeightsFemale[idx] = weight; } 
                else { runtimeWeightsMale[def] = weight; fastWeightsMale[idx] = weight; } }
                else { runtimeWeights[def] = weight; fastWeights[idx] = weight; }
            }
        }

        public bool IsDisabled(Def def, Gender gender) => (def != null && (def.defName == "Bald" || def.defName == "NoBeard")) ? false : GetWeight(def, gender) <= 0f;

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
                    new XElement("version", "1"),
                    new XElement("useGenderConfig", useGenderConfig),
                    new XElement("adjustGenderRatio", adjustGenderRatio),
                    new XElement("applyToPlayerPawns", applyToPlayerPawns),
                    new XElement("maleRatio", maleRatio),
                    new XElement("debugMode", debugMode),
                    new XElement("weights", weights.Select(kv => new XElement("entry", new XAttribute("key", kv.Key), new XAttribute("value", kv.Value)))),
                    new XElement("weightsMale", weightsMale.Select(kv => new XElement("entry", new XAttribute("key", kv.Key), new XAttribute("value", kv.Value)))),
                    new XElement("weightsFemale", weightsFemale.Select(kv => new XElement("entry", new XAttribute("key", kv.Key), new XAttribute("value", kv.Value))))
                );
                new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(GetProfilePath(safeName));
                currentProfileName = safeName;
                Write();
                return true;
            }
            catch (Exception e) { Log.Error("NPCStyleLimiter: Save failed: " + e.Message); return false; }
        }

        public bool LoadProfile(string name)
        {
            try
            {
                string path = GetProfilePath(name);
                if (!File.Exists(path)) return false;
                XElement root = XDocument.Load(path).Root;
                if (root == null) return false;
                useGenderConfig = (bool?)root.Element("useGenderConfig") ?? false;
                adjustGenderRatio = (bool?)root.Element("adjustGenderRatio") ?? false;
                applyToPlayerPawns = (bool?)root.Element("applyToPlayerPawns") ?? false;
                maleRatio = (float?)root.Element("maleRatio") ?? 0.5f;
                debugMode = (bool?)root.Element("debugMode") ?? false;
                weights = ReadWeightDict(root.Element("weights"));
                weightsMale = ReadWeightDict(root.Element("weightsMale"));
                weightsFemale = ReadWeightDict(root.Element("weightsFemale"));
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
