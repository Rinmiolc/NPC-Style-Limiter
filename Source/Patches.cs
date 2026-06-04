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
            PawnGenerationState.Enter();

            // Adjust gender ratio if enabled and request does not dictate a fixed gender (only for humanlikes to avoid breaking animals/mechanoids)
            // 若启用了自定义男女比例，且当前请求未固定性别，则重新分配性别（仅针对人类，避免干扰动物和机械族）
            if (CustomizerMod.Settings.adjustGenderRatio && !request.FixedGender.HasValue &&
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
            if (__result && PawnGenerationState.IsGenerating)
            {
                // Defensive null checks
                // 防御性空值检查
                if (styleItemDef == null || pawn == null) return;

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
            if (PawnGenerationState.IsGenerating && __result > 0f && styleItem != null && pawn != null)
            {
                float w = CustomizerMod.Settings.GetWeight(styleItem, pawn.gender);
                __result *= w;
            }
        }
    }

    // Double security: Patch PawnStyleItemChooser.RandomHairFor to reroll if it selected a disabled hairstyle
    // 双重保险：补丁 PawnStyleItemChooser.RandomHairFor，如果选到了禁用的发型则进行重滚
    [HarmonyPatch(typeof(PawnStyleItemChooser), nameof(PawnStyleItemChooser.RandomHairFor))]
    public static class Patch_PawnStyleItemChooser_RandomHairFor
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref HairDef __result)
        {
            if (PawnGenerationState.IsGenerating && __result != null && pawn != null)
            {
                if (CustomizerMod.Settings.IsDisabled(__result, pawn.gender))
                {
                    // Overwrite the chosen disabled style with a random valid fallback
                    // 用随机的有效后备发型覆盖已选中的禁用发型
                    HairDef fallback = GetFallbackHairFor(pawn);
                    if (fallback != null)
                    {
                        __result = fallback;
                    }
                }
            }
        }

        private static HairDef GetFallbackHairFor(Pawn pawn)
        {
            HairDef systemBald = HairDefOf.Bald ?? DefDatabase<HairDef>.GetNamedSilentFail("Bald");
            if (pawn == null) return systemBald;

            List<HairDef> allHairs = DefDatabase<HairDef>.AllDefsListForReading;
            if (allHairs.Count == 0) return systemBald;

            // Zero-allocation wrap-around search starting from a random index
            // 零分配的环绕式检索，从随机的起始索引开始
            int count = allHairs.Count;
            int start = Rand.Range(0, count);
            HairDef absoluteFallback = null;

            for (int i = 0; i < count; i++)
            {
                HairDef hair = allHairs[(start + i) % count];
                if (hair == null) continue;

                if (hair.defName == "Bald")
                {
                    absoluteFallback = hair;
                }

                // Skip disabled styles
                // 跳过禁用的样式
                if (CustomizerMod.Settings.IsDisabled(hair, pawn.gender))
                {
                    continue;
                }

                // Verify with WantsToUseStyle (which will respect other restrictions like gender/mod-patches)
                // 使用 WantsToUseStyle 进行验证（它将遵循性别/Mod 补丁等其他限制条件）
                if (PawnStyleItemChooser.WantsToUseStyle(pawn, hair))
                {
                    return hair;
                }
            }

            return absoluteFallback ?? systemBald ?? allHairs[0];
        }
    }

    // Double security: Patch PawnStyleItemChooser.RandomBeardFor to reroll if it selected a disabled beard
    // 双重保险：补丁 PawnStyleItemChooser.RandomBeardFor，如果选到了禁用的胡须则进行重滚
    [HarmonyPatch(typeof(PawnStyleItemChooser), nameof(PawnStyleItemChooser.RandomBeardFor))]
    public static class Patch_PawnStyleItemChooser_RandomBeardFor
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref BeardDef __result)
        {
            if (PawnGenerationState.IsGenerating && __result != null && pawn != null)
            {
                if (CustomizerMod.Settings.IsDisabled(__result, pawn.gender))
                {
                    // Overwrite the chosen disabled style with a random valid fallback
                    // 用随机的有效后备胡须覆盖已选中的禁用胡须
                    BeardDef fallback = GetFallbackBeardFor(pawn);
                    if (fallback != null)
                    {
                        __result = fallback;
                    }
                }
            }
        }

        private static BeardDef GetFallbackBeardFor(Pawn pawn)
        {
            BeardDef systemNoBeard = BeardDefOf.NoBeard ?? DefDatabase<BeardDef>.GetNamedSilentFail("NoBeard");
            if (pawn == null) return systemNoBeard;

            List<BeardDef> allBeards = DefDatabase<BeardDef>.AllDefsListForReading;
            if (allBeards.Count == 0) return systemNoBeard;

            // Zero-allocation wrap-around search starting from a random index
            // 零分配的环绕式检索，从随机的起始索引开始
            int count = allBeards.Count;
            int start = Rand.Range(0, count);
            BeardDef absoluteFallback = null;

            for (int i = 0; i < count; i++)
            {
                BeardDef beard = allBeards[(start + i) % count];
                if (beard == null) continue;

                if (beard.defName == "NoBeard")
                {
                    absoluteFallback = beard;
                }

                // Skip disabled styles
                // 跳过禁用的样式
                if (CustomizerMod.Settings.IsDisabled(beard, pawn.gender))
                {
                    continue;
                }

                // Verify with WantsToUseStyle (which will respect other restrictions like gender/mod-patches)
                // 使用 WantsToUseStyle 进行验证（它将遵循性别/Mod 补丁等其他限制条件）
                if (PawnStyleItemChooser.WantsToUseStyle(pawn, beard))
                {
                    return beard;
                }
            }

            return absoluteFallback ?? systemNoBeard ?? allBeards[0];
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
                            cachedAdultBodyTypes = new List<BodyTypeDef>
                            {
                                BodyTypeDefOf.Male,
                                BodyTypeDefOf.Female,
                                BodyTypeDefOf.Thin,
                                BodyTypeDefOf.Hulk,
                                BodyTypeDefOf.Fat
                            };
                        }
                    }
                }
                return cachedAdultBodyTypes;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref BodyTypeDef __result)
        {
            if (PawnGenerationState.IsGenerating && __result != null && pawn != null)
            {
                // Only adjust for standard humans and standard adult body types to prevent compatibility issues with alien races
                // 仅针对标准人类以及处于正常成年体型范围的 Pawn 进行调整，以防破坏异形种族的贴图和骨骼
                if (pawn.def == ThingDefOf.Human && __result != BodyTypeDefOf.Baby && __result != BodyTypeDefOf.Child)
                {
                    if (AdultBodyTypes.Contains(__result))
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
            var bodyTypes = AdultBodyTypes;
            float totalWeight = 0f;

            // Pass 1: Sum the weights of candidates with zero memory allocation
            // 第一遍扫描：计算所有候选体型的权重总和，零内存分配
            for (int i = 0; i < bodyTypes.Count; i++)
            {
                var bodyType = bodyTypes[i];
                if (bodyType == null) continue;
                totalWeight += GetPawnBodyTypeWeight(bodyType, pawn, original);
            }

            if (totalWeight <= 0f)
            {
                return original; // Fallback to original if everything has 0 weight
            }

            // Pass 2: Weighted random selection with zero memory allocation
            // 第二遍扫描：加权随机抽取，零内存分配
            float rand = Rand.Value * totalWeight;
            float currentSum = 0f;
            for (int i = 0; i < bodyTypes.Count; i++)
            {
                var bodyType = bodyTypes[i];
                if (bodyType == null) continue;
                float w = GetPawnBodyTypeWeight(bodyType, pawn, original);
                if (w > 0f)
                {
                    currentSum += w;
                    if (rand <= currentSum)
                    {
                        return bodyType;
                    }
                }
            }
            return original;
        }

        private static float GetPawnBodyTypeWeight(BodyTypeDef bodyType, Pawn pawn, BodyTypeDef original)
        {
            if (pawn == null || bodyType == null || CustomizerMod.Settings.IsDisabled(bodyType, pawn.gender))
            {
                return 0f;
            }

            // Base preference: highly prefer the body type originally chosen by the game (respects traits/gender)
            // 基础概率：极大偏好游戏原本选出的体型（尊重背景特征/性别等）
            float baseWeight = 0.1f;
            if (bodyType == original)
            {
                baseWeight = 1.0f;
            }
            else if (pawn.gender == Gender.Female && bodyType == BodyTypeDefOf.Male)
            {
                baseWeight = 0.0f; // Female pawns should never spawn with Male body type
            }
            else if (pawn.gender == Gender.Male && bodyType == BodyTypeDefOf.Female)
            {
                baseWeight = 0.0f; // Male pawns should never spawn with Female body type
            }

            float userMultiplier = CustomizerMod.Settings.GetWeight(bodyType, pawn.gender);
            return baseWeight * userMultiplier;
        }
    }

    // ThreadLocal tracking class to capture the Pawn currently undergoing apparel generation (Stack-based for nesting support)
    // 线程局部变量跟踪类，用于捕获当前正在生成服装的 Pawn 实例（基于栈以支持嵌套调用）
    [HarmonyPatch(typeof(PawnApparelGenerator), nameof(PawnApparelGenerator.GenerateStartingApparelFor))]
    public static class Patch_PawnApparelGenerator_GenerateStartingApparelFor
    {
        private static readonly System.Threading.ThreadLocal<System.Collections.Generic.Stack<Pawn>> pawnStack = 
            new System.Threading.ThreadLocal<System.Collections.Generic.Stack<Pawn>>(() => new System.Collections.Generic.Stack<Pawn>());

        public static Pawn CurrentPawn => (pawnStack.Value.Count > 0) ? pawnStack.Value.Peek() : null;

        [HarmonyPrefix]
        public static void Prefix(Pawn pawn)
        {
            pawnStack.Value.Push(pawn);
        }

        [HarmonyFinalizer]
        public static void Finalizer()
        {
            if (pawnStack.Value.Count > 0)
            {
                pawnStack.Value.Pop();
            }
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
            // CurrentPawn != null already implies apparel generation is in progress,
            // which saves an extra ThreadLocal lookup on PawnGenerationState.IsGenerating.
            if (pawn != null && __result > 0f)
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
            if (__result && PawnGenerationState.IsGenerating)
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
