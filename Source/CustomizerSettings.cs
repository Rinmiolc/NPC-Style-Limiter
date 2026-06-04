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

                // Migrate all legacy plain-name keys to prefixed keys to avoid duplicates and resolve collisions
                MigrateLegacyKeys(weights);
                MigrateLegacyKeys(weightsMale);
                MigrateLegacyKeys(weightsFemale);
            }
        }

        private void MigrateLegacyKeys(Dictionary<string, float> dict)
        {
            if (dict == null) return;
            List<string> legacyKeys = new List<string>();
            foreach (var key in dict.Keys)
            {
                if (key != null && !key.Contains(":"))
                {
                    legacyKeys.Add(key);
                }
            }

            foreach (var legacyKey in legacyKeys)
            {
                float val = dict[legacyKey];
                dict.Remove(legacyKey);

                Def def = FindDef(legacyKey);
                if (def != null)
                {
                    string newKey = GetConfigKey(def);
                    dict[newKey] = val;
                }
            }
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
            runtimeWeights.Clear();
            runtimeWeightsMale.Clear();
            runtimeWeightsFemale.Clear();

            ResolveDictionary(weights, runtimeWeights);
            ResolveDictionary(weightsMale, runtimeWeightsMale);
            ResolveDictionary(weightsFemale, runtimeWeightsFemale);
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
        }

        public float GetWeight(Def def, Gender gender)
        {
            if (def == null) return 1.0f;

            if (useGenderConfig)
            {
                if (gender == Gender.Female)
                {
                    if (runtimeWeightsFemale.TryGetValue(def, out float w)) return w;
                }
                else // Male or None
                {
                    if (runtimeWeightsMale.TryGetValue(def, out float w)) return w;
                }
            }
            else
            {
                if (runtimeWeights.TryGetValue(def, out float w)) return w;
            }
            return 1.0f; // Default weight is 1.0
        }

        public float GetWeight(string key, Gender gender)
        {
            if (useGenderConfig)
            {
                if (gender == Gender.Female)
                {
                    if (weightsFemale != null && weightsFemale.TryGetValue(key, out float w)) return w;
                }
                else // Male or None
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
                if (useGenderConfig)
                {
                    if (gender == Gender.Female)
                    {
                        runtimeWeightsFemale[def] = weight;
                    }
                    else
                    {
                        runtimeWeightsMale[def] = weight;
                    }
                }
                else
                {
                    runtimeWeights[def] = weight;
                }
            }
        }

        public bool IsDisabled(Def def, Gender gender)
        {
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

