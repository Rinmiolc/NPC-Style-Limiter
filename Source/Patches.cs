// Copyright (c) 2026 rinmiolc
// Licensed under the GNU General Public License v3.0.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace NPCStyleLimiter
{
    // Patch PawnGenerator.GeneratePawn to track when pawn generation is in progress and adjust gender ratio
    // 补丁 PawnGenerator.GeneratePawn 以便在生成 Pawn 时进行追踪并调节男女比例
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    public static class Patch_PawnGenerator_GeneratePawn
    {
        [HarmonyPrefix]
        public static void Prefix(ref PawnGenerationRequest request)
        {
            PawnGenerationState.Enter(request.Context);

            // Adjust gender ratio if enabled for this race
            if (PawnGenerationState.IsTargetGeneration && CustomizerMod.Settings != null && !request.FixedGender.HasValue &&
                request.KindDef?.RaceProps != null && request.KindDef.RaceProps.Humanlike)
            {
                string raceDefName = request.KindDef.race?.defName;
                var raceSettings = CustomizerMod.Settings.GetSettingsForRace(raceDefName);

                if (raceSettings.adjustGenderRatio)
                {
                    if (request.KindDef.fixedGender.HasValue) return;

                    Gender chosenGender = (Rand.Value < raceSettings.maleRatio) ? Gender.Male : Gender.Female;
                    request.FixedGender = chosenGender;
                }
            }
        }

        [HarmonyFinalizer]
        public static void PostfixOrFinalizer()
        {
            PawnGenerationState.Exit();
        }
    }

    // Patch PawnStyleItemChooser.WantsToUseStyle to prevent disabled hairs/beards during pawn generation
    // 补丁 PawnStyleItemChooser.WantsToUseStyle 以便在生成 Pawn 时阻止已被禁用的发型/胡须
    [HarmonyPatch(typeof(PawnStyleItemChooser), nameof(PawnStyleItemChooser.WantsToUseStyle))]
    public static class Patch_PawnStyleItemChooser_WantsToUseStyle
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, StyleItemDef styleItemDef, ref bool __result)
        {
            if (__result && PawnGenerationState.IsTargetGeneration && pawn != null && pawn.RaceProps.Humanlike)
            {
                // Defensive null checks
                if (styleItemDef == null || CustomizerMod.Settings == null) return;

                // Never restrict Bald or NoBeard (safe fallbacks)
                // 绝不限制光头（Bald）或无胡须（NoBeard）（作为安全的后备选项）
                if (styleItemDef.defName == "Bald" || styleItemDef.defName == "NoBeard") return;

                if (CustomizerMod.Settings.IsDisabled(styleItemDef, pawn.gender, pawn.def?.defName))
                {
                    __result = false;
                }
            }
        }
    }

    // Patch PawnStyleItemChooser.TotalStyleItemLikelihood to multiply custom spawn weight
    // 补丁 PawnStyleItemChooser.TotalStyleItemLikelihood 以乘算自定义生成权重
    [HarmonyPatch(typeof(PawnStyleItemChooser), "TotalStyleItemLikelihood")]
    public static class Patch_PawnStyleItemChooser_TotalStyleItemLikelihood
    {
        [HarmonyPostfix]
        public static void Postfix(StyleItemDef styleItem, Pawn pawn, ref float __result)
        {
            if (PawnGenerationState.IsTargetGeneration && __result > 0f && styleItem != null && pawn != null && CustomizerMod.Settings != null)
            {
                float w = CustomizerMod.Settings.GetWeight(styleItem, pawn.gender, pawn.def?.defName);
                __result *= w;
            }
        }
    }

    // Patch PawnGenerator.GetBodyTypeFor to filter and adjust ratios of adult body types
    // 补丁 PawnGenerator.GetBodyTypeFor 以过滤并调整成年人体型的比例
    [HarmonyPatch(typeof(PawnGenerator), "GetBodyTypeFor")]
    public static class Patch_PawnGenerator_GetBodyTypeFor
    {
        private static readonly object lockObj = new object();
        private static List<BodyTypeDef> cachedAdultBodyTypes;

        private static List<BodyTypeDef> AdultBodyTypes
        {
            get
            {
                if (cachedAdultBodyTypes == null)
                {
                    lock (lockObj)
                    {
                        if (cachedAdultBodyTypes == null)
                        {
                            var list = new List<BodyTypeDef>();
                            var allDefs = DefDatabase<BodyTypeDef>.AllDefsListForReading;
                            for (int i = 0; i < allDefs.Count; i++)
                            {
                                var def = allDefs[i];
                                if (def != null && def.defName != "Baby" && def.defName != "Child")
                                {
                                    list.Add(def);
                                }
                            }
                            cachedAdultBodyTypes = list;
                        }
                    }
                }
                return cachedAdultBodyTypes;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref BodyTypeDef __result)
        {
            if (PawnGenerationState.IsTargetGeneration && __result != null && pawn != null && CustomizerMod.Settings != null)
            {
                // Expanded compatibility for HAR and other humanlikes
                // 扩大对 HAR 及其他类人种族的兼容性
                if (pawn.RaceProps.Humanlike && __result != BodyTypeDefOf.Baby && __result != BodyTypeDefOf.Child)
                {
                    string raceDefName = pawn.def?.defName;
                    var settings = CustomizerMod.Settings;
                    bool isHuman = pawn.def == ThingDefOf.Human;
                    bool hasSpecificConfig = settings.raceSettings != null && 
                                            settings.raceSettings.TryGetValue(raceDefName, out var s) && 
                                            s.useSpecificConfig;

                    if (isHuman || hasSpecificConfig)
                    {
                        BodyTypeDef customType = GetWeightedBodyTypeFor(pawn, __result);
                        if (customType != null)
                        {
                            __result = customType;
                        }
                    }
                }
            }
        }

        private static BodyTypeDef GetWeightedBodyTypeFor(Pawn pawn, BodyTypeDef original)
        {
            var settings = CustomizerMod.Settings;
            if (settings == null) return original;
            
            var bodyTypes = AdultBodyTypes;
            if (bodyTypes == null || bodyTypes.Count == 0) return original;

            float totalWeight = 0f;
            int count = bodyTypes.Count;

            // Pass 1: Sum the weights considering the original preference
            for (int i = 0; i < count; i++)
            {
                var bodyType = bodyTypes[i];
                float userWeight = settings.GetWeight(bodyType, pawn.gender, pawn.def?.defName);
                totalWeight += GetFinalBodyTypeWeight(bodyType, userWeight, pawn, original);
            }

            if (totalWeight <= 0f) return original;

            // Pass 2: Selection
            float rand = Rand.Value * totalWeight;
            float currentSum = 0f;
            for (int i = 0; i < count; i++)
            {
                var bodyType = bodyTypes[i];
                float userWeight = settings.GetWeight(bodyType, pawn.gender, pawn.def?.defName);
                float w = GetFinalBodyTypeWeight(bodyType, userWeight, pawn, original);
                if (w > 0f)
                {
                    currentSum += w;
                    if (rand <= currentSum) return bodyType;
                }
            }
            return original;
        }

        private static float GetFinalBodyTypeWeight(BodyTypeDef bodyType, float userMultiplier, Pawn pawn, BodyTypeDef original)
        {
            if (userMultiplier <= 0f) return 0f;

            // Base preference
            float baseWeight = (bodyType == original) ? 1.0f : 0.1f;
            
            // Gender safety
            if (pawn.gender == Gender.Female && bodyType == BodyTypeDefOf.Male) return 0f;
            if (pawn.gender == Gender.Male && bodyType == BodyTypeDefOf.Female) return 0f;

            // Prevent cross-species body type contamination.
            // If bodyType is not original, and is not a vanilla body type (Male, Female, Thin, Fat, Hulk), 
            // then it belongs to another alien race. We should never assign it.
            if (bodyType != original && 
                bodyType != BodyTypeDefOf.Male && 
                bodyType != BodyTypeDefOf.Female && 
                bodyType != BodyTypeDefOf.Thin && 
                bodyType != BodyTypeDefOf.Fat && 
                bodyType != BodyTypeDefOf.Hulk)
            {
                return 0f;
            }

            return baseWeight * userMultiplier;
        }
    }

    // ThreadStatic tracking class to capture the Pawn currently undergoing apparel generation
    // 线程静态变量跟踪类，用于捕获当前正在生成服装的 Pawn 实例
    [HarmonyPatch(typeof(PawnApparelGenerator), nameof(PawnApparelGenerator.GenerateStartingApparelFor))]
    public static class Patch_PawnApparelGenerator_GenerateStartingApparelFor
    {
        [ThreadStatic]
        private static Pawn currentPawn;

        [ThreadStatic]
        private static bool disableApparelFilter;

        private static System.Reflection.MethodInfo canUsePairMethod;
        private static System.Reflection.FieldInfo allApparelPairsField;

        public static Pawn CurrentPawn => currentPawn;
        public static bool DisableApparelFilter => disableApparelFilter;

        private static object GetDefaultValue(System.Type t)
        {
            if (t.IsValueType) return System.Activator.CreateInstance(t);
            return null;
        }

        [HarmonyPrefix]
        public static void Prefix(Pawn pawn)
        {
            currentPawn = pawn;
            disableApparelFilter = false;

            // Safety check: Prevent complete apparel block which causes loop crashes in vanilla generator
            if (pawn != null && CustomizerMod.Settings != null && PawnGenerationState.IsTargetGeneration)
            {
                if (allApparelPairsField == null)
                {
                    allApparelPairsField = typeof(PawnApparelGenerator).GetField("allApparelPairs", 
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                }

                if (allApparelPairsField != null)
                {
                    var allPairs = (System.Collections.Generic.List<ThingStuffPair>)allApparelPairsField.GetValue(null);
                    if (allPairs != null)
                    {
                        if (canUsePairMethod == null)
                        {
                            canUsePairMethod = typeof(PawnApparelGenerator).GetMethod("CanUsePair", 
                                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                        }

                        if (canUsePairMethod != null)
                        {
                            var paramInfos = canUsePairMethod.GetParameters();
                            object[] parameters = new object[paramInfos.Length];
                            bool anyAllowed = false;
                            int count = allPairs.Count;

                            for (int i = 0; i < count; i++)
                            {
                                var pair = allPairs[i];
                                if (pair.thing != null)
                                {
                                    if (parameters.Length > 0) parameters[0] = pair;
                                    if (parameters.Length > 1) parameters[1] = pawn;
                                    for (int p = 2; p < parameters.Length; p++)
                                    {
                                        var pInfo = paramInfos[p];
                                        var defVal = pInfo.DefaultValue;
                                        if (defVal != System.DBNull.Value)
                                        {
                                            parameters[p] = defVal;
                                        }
                                        else
                                        {
                                            var pType = pInfo.ParameterType;
                                            if (pType == typeof(bool))
                                            {
                                                parameters[p] = false;
                                            }
                                            else if (pType == typeof(float) || pType == typeof(double))
                                            {
                                                parameters[p] = 1f;
                                            }
                                            else
                                            {
                                                parameters[p] = GetDefaultValue(pType);
                                            }
                                        }
                                    }

                                    bool canUse = (bool)canUsePairMethod.Invoke(null, parameters);
                                    if (canUse)
                                    {
                                        if (!CustomizerMod.Settings.IsDisabled(pair.thing, pawn.gender, pawn.def?.defName))
                                        {
                                            anyAllowed = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (!anyAllowed)
                            {
                                disableApparelFilter = true;
                            }
                        }
                    }
                }
            }
        }

        [HarmonyFinalizer]
        public static void Finalizer()
        {
            currentPawn = null;
            disableApparelFilter = false;
        }
    }

    // Patch ThingStuffPair.Commonality property to apply user custom weight to apparel selection
    // 补丁 ThingStuffPair.Commonality 属性以应用用户自定义服装选择权重
    [HarmonyPatch(typeof(ThingStuffPair), "get_Commonality")]
    public static class Patch_ThingStuffPair_get_Commonality
    {
        [HarmonyPostfix]
        public static void Postfix(ThingStuffPair __instance, ref float __result)
        {
            Pawn pawn = Patch_PawnApparelGenerator_GenerateStartingApparelFor.CurrentPawn;
            if (pawn != null && !Patch_PawnApparelGenerator_GenerateStartingApparelFor.DisableApparelFilter && 
                __result > 0f && PawnGenerationState.IsTargetGeneration && CustomizerMod.Settings != null)
            {
                if (__instance.thing != null)
                {
                    float multiplier = CustomizerMod.Settings.GetWeight(__instance.thing, pawn.gender, pawn.def?.defName);
                    __result *= multiplier;
                }
            }
        }
    }

    // Patch PawnApparelGenerator.CanUsePair to hard-disable apparel if weight is 0
    // 补丁 PawnApparelGenerator.CanUsePair，若权重归零则完全禁用该服装
    [HarmonyPatch(typeof(PawnApparelGenerator), "CanUsePair")]
    public static class Patch_PawnApparelGenerator_CanUsePair
    {
        [HarmonyPostfix]
        public static void Postfix(ThingStuffPair pair, Pawn pawn, ref bool __result)
        {
            if (__result && !Patch_PawnApparelGenerator_GenerateStartingApparelFor.DisableApparelFilter && 
                PawnGenerationState.IsTargetGeneration && CustomizerMod.Settings != null)
            {
                if (pawn != null && pair.thing != null)
                {
                    if (CustomizerMod.Settings.IsDisabled(pair.thing, pawn.gender, pawn.def?.defName))
                    {
                        __result = false;
                    }
                }
            }
        }
    }

    // Patch TaleRecorder.RecordTale to prevent accessing TicksAbs during scenario/QuickTest initialization.
    // 补丁 TaleRecorder.RecordTale 以防止在场景或快速测试初始化期间访问 TicksAbs。
    [HarmonyPatch(typeof(TaleRecorder), nameof(TaleRecorder.RecordTale))]
    public static class Patch_TaleRecorder_RecordTale
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            // Skip recording tales if the game hasn't fully loaded into the playing state.
            // This prevents "Accessing TicksAbs but gameStartAbsTick is not set yet" errors.
            if (Current.ProgramState != ProgramState.Playing)
            {
                return false;
            }
            return true;
        }
    }

    // Patch Pawn_StoryTracker.TryGetRandomHeadFromSet to customize and filter head types
    // 补丁 Pawn_StoryTracker.TryGetRandomHeadFromSet 以过滤并调整选定脸型
    [HarmonyPatch(typeof(Pawn_StoryTracker), nameof(Pawn_StoryTracker.TryGetRandomHeadFromSet))]
    public static class Patch_Pawn_StoryTracker_TryGetRandomHeadFromSet
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn_StoryTracker __instance, Pawn ___pawn, ref IEnumerable<HeadTypeDef> options, ref bool __result)
        {
            if (PawnGenerationState.IsTargetGeneration && __instance != null && ___pawn != null && 
                ___pawn.RaceProps.Humanlike && CustomizerMod.Settings != null && options != null)
            {
                string raceDefName = ___pawn.def?.defName;
                var settings = CustomizerMod.Settings;
                bool isHuman = ___pawn.def == ThingDefOf.Human;
                bool hasSpecificConfig = settings.raceSettings != null && 
                                        settings.raceSettings.TryGetValue(raceDefName, out var s) && 
                                        s.useSpecificConfig;

                if (isHuman || hasSpecificConfig)
                {
                    var filteredOptions = new List<HeadTypeDef>();
                    foreach (var head in options)
                    {
                        if (head != null && !settings.IsDisabled(head, ___pawn.gender, raceDefName))
                        {
                            filteredOptions.Add(head);
                        }
                    }

                    if (filteredOptions.Count == 0)
                    {
                        foreach (var head in options)
                        {
                            if (head != null) filteredOptions.Add(head);
                        }
                    }

                    if (filteredOptions.Count > 0)
                    {
                        float totalWeight = 0f;
                        var weights = new List<float>();
                        for (int i = 0; i < filteredOptions.Count; i++)
                        {
                            float weight = settings.GetWeight(filteredOptions[i], ___pawn.gender, raceDefName);
                            if (weight < 0f) weight = 0f;
                            weights.Add(weight);
                            totalWeight += weight;
                        }

                        HeadTypeDef chosenHead = null;
                        if (totalWeight > 0f)
                        {
                            float rand = Rand.Value * totalWeight;
                            float currentSum = 0f;
                            for (int i = 0; i < filteredOptions.Count; i++)
                            {
                                currentSum += weights[i];
                                if (rand <= currentSum)
                                {
                                    chosenHead = filteredOptions[i];
                                    break;
                                }
                            }
                        }

                        if (chosenHead == null)
                        {
                            chosenHead = filteredOptions.RandomElement();
                        }

                        if (chosenHead != null)
                        {
                            __instance.headType = chosenHead;
                            __result = true;
                            return false; // Skip original method
                        }
                    }
                }
            }
            return true; // Let vanilla run
        }
    }
}

