// Copyright (c) 2026 rinmiolc
// Licensed under the GNU General Public License v3.0.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using RimWorld;

namespace NPCStyleLimiter
{
    public static class PawnGenerationState
    {
        [ThreadStatic]
        private static System.Collections.Generic.List<PawnGenerationContext> contextStack;

        private static System.Collections.Generic.List<PawnGenerationContext> ContextStack
        {
            get
            {
                if (contextStack == null) contextStack = new System.Collections.Generic.List<PawnGenerationContext>();
                return contextStack;
            }
        }

        public static bool IsGenerating => ContextStack.Count > 0;
        
        public static bool IsGeneratingNPC => IsGenerating && CurrentContext == PawnGenerationContext.NonPlayer;

        public static PawnGenerationContext CurrentContext => ContextStack.Count > 0 ? ContextStack[ContextStack.Count - 1] : PawnGenerationContext.All;

        public static bool IsTargetGeneration
        {
            get
            {
                if (ContextStack.Count == 0) return false;
                if (CustomizerMod.Settings != null && CustomizerMod.Settings.applyToPlayerPawns) return true;
                return CurrentContext == PawnGenerationContext.NonPlayer;
            }
        }

        public static void Enter(PawnGenerationContext context)
        {
            ContextStack.Add(context);
        }

        public static void Exit()
        {
            if (ContextStack.Count > 0)
            {
                ContextStack.RemoveAt(ContextStack.Count - 1);
            }
        }
    }
}
