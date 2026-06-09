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

            // Adjust gender ratio if enabled and request does not dictate a fixed gender (only for humanlikes to avoid breaking animals/mechanoids)
            // 若启用了自定义男女比例，且当前请求未固定性别，则重新分配性别（仅针对人类，避免干扰动物和机械族）
            if (PawnGenerationState.IsTargetGeneration && CustomizerMod.Settings != null && CustomizerMod.Settings.adjustGenderRatio && !request.FixedGender.HasValue &&
                request.KindDef?.RaceProps != null && request.KindDef.RaceProps.Humanlike)
            {
                // Do not override if the specific PawnKindDef has a fixed gender requirement
                // 若该具体角色类型 (PawnKindDef) 自身有硬性性别限制，则不予干预
                if (request.KindDef.fixedGender.HasValue)
                {
                    return;
                }

                Gender chosenGender = (Rand.Value < CustomizerMod.Settings.maleRatio) ? Gender.Male : Gender.Female;
                request.FixedGender = chosenGender;
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
            if (__result && PawnGenerationState.IsTargetGeneration)
            {
                // Defensive null checks
                // 防御性空值检查
                if (styleItemDef == null || pawn == null || CustomizerMod.Settings == null) return;

                // Never restrict Bald or NoBeard (safe fallbacks)
                // 绝不限制光头（Bald）或无胡须（NoBeard）（作为安全的后备选项）
                if (styleItemDef.defName == "Bald" || styleItemDef.defName == "NoBeard") return;

                if (CustomizerMod.Settings.IsDisabled(styleItemDef, pawn.gender))
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
                float w = CustomizerMod.Settings.GetWeight(styleItem, pawn.gender);
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
                    BodyTypeDef customType = GetWeightedBodyTypeFor(pawn, __result);
                    if (customType != null)
                    {
                        __result = customType;
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
                float userWeight = settings.GetWeight(bodyType, pawn.gender);
                totalWeight += GetFinalBodyTypeWeight(bodyType, userWeight, pawn, original);
            }

            if (totalWeight <= 0f) return original;

            // Pass 2: Selection
            float rand = Rand.Value * totalWeight;
            float currentSum = 0f;
            for (int i = 0; i < count; i++)
            {
                var bodyType = bodyTypes[i];
                float userWeight = settings.GetWeight(bodyType, pawn.gender);
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

        public static Pawn CurrentPawn => currentPawn;

        [HarmonyPrefix]
        public static void Prefix(Pawn pawn)
        {
            currentPawn = pawn;
        }

        [HarmonyFinalizer]
        public static void Finalizer()
        {
            currentPawn = null;
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
            if (pawn != null && __result > 0f && PawnGenerationState.IsTargetGeneration && CustomizerMod.Settings != null)
            {
                if (__instance.thing != null)
                {
                    float multiplier = CustomizerMod.Settings.GetWeight(__instance.thing, pawn.gender);
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
            if (__result && PawnGenerationState.IsTargetGeneration && CustomizerMod.Settings != null)
            {
                if (pawn != null && pair.thing != null)
                {
                    if (CustomizerMod.Settings.IsDisabled(pair.thing, pawn.gender))
                    {
                        __result = false;
                    }
                }
            }
        }
    }
}
