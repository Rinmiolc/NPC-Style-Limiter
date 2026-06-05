// Copyright (c) 2026 rinmiolc
// Licensed under the GNU General Public License v3.0.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Verse;
using RimWorld;

namespace NPCStyleLimiter
{
    public class CustomizerSettings : ModSettings
    {
        public bool useGenderConfig = false;
        public bool adjustGenderRatio = false;
        public float maleRatio = 0.5f;

        public Dictionary<string, float> weights = new Dictionary<string, float>();
        public Dictionary<string, float> weightsMale = new Dictionary<string, float>();
        public Dictionary<string, float> weightsFemale = new Dictionary<string, float>();

        public readonly Dictionary<Def, float> runtimeWeights = new Dictionary<Def, float>();
        public readonly Dictionary<Def, float> runtimeWeightsMale = new Dictionary<Def, float>();
        public readonly Dictionary<Def, float> runtimeWeightsFemale = new Dictionary<Def, float>();

        // Fast O(1) lookup arrays indexed by custom index mapped from def type and shortHash
        private readonly float[] fastWeights = new float[262144];
        private readonly float[] fastWeightsMale = new float[262144];
        private readonly float[] fastWeightsFemale = new float[262144];

        public static int GetFastIndex(Def def)
        {
            if (def == null) return 0;
            int typeOffset = 0;
            if (def is HairDef) typeOffset = 0;
            else if (def is BeardDef) typeOffset = 65536;
            else if (def is ThingDef) typeOffset = 131072;
            else if (def is BodyTypeDef) typeOffset = 196608;
            return typeOffset + def.shortHash;
        }

        public CustomizerSettings()
        {
            for (int i = 0; i < 262144; i++)
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
            Scribe_Values.Look(ref maleRatio, "maleRatio", 0.5f);

            Scribe_Collections.Look(ref weights, "weights", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref weightsMale, "weightsMale", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref weightsFemale, "weightsFemale", LookMode.Value, LookMode.Value);

            // Load legacy lists if present
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Collections.Look(ref disabledHairNames, "disabledHairNames", LookMode.Value);
                Scribe_Collections.Look(ref disabledBeardNames, "disabledBeardNames", LookMode.Value);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InitializeSets();

                // Migrate legacy disabled items to weight = 0
                if (disabledHairNames != null)
                {
                    foreach (var name in disabledHairNames)
                    {
                        if (!string.IsNullOrEmpty(name))
                        {
                            weights[name] = 0f;
                        }
                    }
                    disabledHairNames = null;
                }

                if (disabledBeardNames != null)
                {
                    foreach (var name in disabledBeardNames)
                    {
                        if (!string.IsNullOrEmpty(name))
                        {
                            weights[name] = 0f;
                        }
                    }
                    disabledBeardNames = null;
                }
            }
        }

        private bool MigrateLegacyKeys(Dictionary<string, float> dict)
        {
            if (dict == null) return false;
            List<string> legacyKeys = new List<string>();
            foreach (var key in dict.Keys)
            {
                if (key != null && !key.Contains(":"))
                {
                    legacyKeys.Add(key);
                }
            }

            if (legacyKeys.Count == 0) return false;

            bool anyMigrated = false;
            foreach (var legacyKey in legacyKeys)
            {
                float val = dict[legacyKey];
                Def def = FindDef(legacyKey);
                if (def != null)
                {
                    dict.Remove(legacyKey);
                    string newKey = GetConfigKey(def);
                    dict[newKey] = val;
                    anyMigrated = true;
                }
            }
            return anyMigrated;
        }

        public string GetConfigKey(Def def)
        {
            if (def == null) return null;
            return def.GetType().Name + ":" + def.defName;
        }

        public void InitializeSets()
        {
            if (weights == null) weights = new Dictionary<string, float>();
            if (weightsMale == null) weightsMale = new Dictionary<string, float>();
            if (weightsFemale == null) weightsFemale = new Dictionary<string, float>();
        }

        public void ResolveRuntimeWeights()
        {
            bool migrated = MigrateLegacyKeys(weights) || 
                            MigrateLegacyKeys(weightsMale) || 
                            MigrateLegacyKeys(weightsFemale);
            if (migrated)
            {
                Write();
            }

            runtimeWeights.Clear();
            runtimeWeightsMale.Clear();
            runtimeWeightsFemale.Clear();

            ResolveDictionary(weights, runtimeWeights);
            ResolveDictionary(weightsMale, runtimeWeightsMale);
            ResolveDictionary(weightsFemale, runtimeWeightsFemale);

            // Rebuild fast weights cache
            for (int i = 0; i < 262144; i++)
            {
                fastWeights[i] = 1f;
                fastWeightsMale[i] = 1f;
                fastWeightsFemale[i] = 1f;
            }

            foreach (var kvp in runtimeWeights)
            {
                if (kvp.Key != null) fastWeights[GetFastIndex(kvp.Key)] = kvp.Value;
            }
            foreach (var kvp in runtimeWeightsMale)
            {
                if (kvp.Key != null) fastWeightsMale[GetFastIndex(kvp.Key)] = kvp.Value;
            }
            foreach (var kvp in runtimeWeightsFemale)
            {
                if (kvp.Key != null) fastWeightsFemale[GetFastIndex(kvp.Key)] = kvp.Value;
            }
        }

        private void ResolveDictionary(Dictionary<string, float> source, Dictionary<Def, float> target)
        {
            if (source == null) return;
            foreach (var kvp in source)
            {
                if (kvp.Key == null) continue;
                Def def = FindDef(kvp.Key);
                if (def != null)
                {
                    target[def] = kvp.Value;
                }
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

                if (typeName == nameof(HairDef))
                {
                    return DefDatabase<HairDef>.GetNamedSilentFail(realDefName);
                }
                if (typeName == nameof(BeardDef))
                {
                    return DefDatabase<BeardDef>.GetNamedSilentFail(realDefName);
                }
                if (typeName == nameof(ThingDef))
                {
                    return DefDatabase<ThingDef>.GetNamedSilentFail(realDefName);
                }
                if (typeName == nameof(BodyTypeDef))
                {
                    return DefDatabase<BodyTypeDef>.GetNamedSilentFail(realDefName);
                }
            }

            // Fallback for legacy key format (no prefix)
            Def def = DefDatabase<HairDef>.GetNamedSilentFail(defName);
            if (def != null) return def;

            def = DefDatabase<BeardDef>.GetNamedSilentFail(defName);
            if (def != null) return def;

            def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null) return def;

            def = DefDatabase<BodyTypeDef>.GetNamedSilentFail(defName);
            if (def != null) return def;

            return null;
        }

        public void ResetToDefaults()
        {
            useGenderConfig = false;
            adjustGenderRatio = false;
            maleRatio = 0.5f;
            if (weights != null) weights.Clear();
            if (weightsMale != null) weightsMale.Clear();
            if (weightsFemale != null) weightsFemale.Clear();
            runtimeWeights.Clear();
            runtimeWeightsMale.Clear();
            runtimeWeightsFemale.Clear();
            for (int i = 0; i < 262144; i++)
            {
                fastWeights[i] = 1f;
                fastWeightsMale[i] = 1f;
                fastWeightsFemale[i] = 1f;
            }
        }

        public float GetWeight(Def def, Gender gender)
        {
            if (def == null) return 1.0f;

            int idx = GetFastIndex(def);
            if (useGenderConfig && gender != Gender.None)
            {
                if (gender == Gender.Female)
                {
                    return fastWeightsFemale[idx];
                }
                else // Male
                {
                    return fastWeightsMale[idx];
                }
            }
            else
            {
                return fastWeights[idx];
            }
        }

        public float GetWeight(string key, Gender gender)
        {
            if (useGenderConfig && gender != Gender.None)
            {
                if (gender == Gender.Female)
                {
                    if (weightsFemale != null && weightsFemale.TryGetValue(key, out float w)) return w;
                }
                else // Male
                {
                    if (weightsMale != null && weightsMale.TryGetValue(key, out float w)) return w;
                }
            }
            else
            {
                if (weights != null && weights.TryGetValue(key, out float w)) return w;
            }
            return 1.0f; // Default weight is 1.0
        }

        public void SetWeight(Def def, Gender gender, float weight)
        {
            if (def == null) return;
            SetWeight(GetConfigKey(def), gender, weight);
        }

        public void SetWeight(string key, Gender gender, float weight)
        {
            InitializeSets();

            if (useGenderConfig)
            {
                if (gender == Gender.Female)
                {
                    weightsFemale[key] = weight;
                }
                else
                {
                    weightsMale[key] = weight;
                }
            }
            else
            {
                weights[key] = weight;
            }

            // Sync with runtime cache
            Def def = FindDef(key);
            if (def != null)
            {
                int idx = GetFastIndex(def);
                if (useGenderConfig)
                {
                    if (gender == Gender.Female)
                    {
                        runtimeWeightsFemale[def] = weight;
                        fastWeightsFemale[idx] = weight;
                    }
                    else
                    {
                        runtimeWeightsMale[def] = weight;
                        fastWeightsMale[idx] = weight;
                    }
                }
                else
                {
                    runtimeWeights[def] = weight;
                    fastWeights[idx] = weight;
                }
            }
        }

        public bool IsDisabled(Def def, Gender gender)
        {
            if (def != null && (def.defName == "Bald" || def.defName == "NoBeard")) return false;
            return GetWeight(def, gender) <= 0f;
        }

        public bool IsDisabled(string key, Gender gender)
        {
            return GetWeight(key, gender) <= 0f;
        }
    }
}

namespace GlobalHairBeardCustomizer
{
    // Backwards compatibility stub for users upgrading from older versions of the mod
    // to prevent Ludeon Scribe serialization errors for existing settings XML files.
    public class CustomizerSettings : NPCStyleLimiter.CustomizerSettings
    {
    }
}

