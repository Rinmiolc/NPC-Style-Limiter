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
        private static int generationDepth;

        [ThreadStatic]
        private static PawnGenerationContext currentContext;

        public static bool IsGenerating => generationDepth > 0;
        
        // Only target non-player pawns or all if specified. 
        // Typically "NPC Style Limiter" should target NonPlayer context.
        public static bool IsGeneratingNPC => IsGenerating && currentContext == PawnGenerationContext.NonPlayer;

        public static void Enter(PawnGenerationContext context)
        {
            generationDepth++;
            currentContext = context;
        }

        public static void Exit()
        {
            generationDepth = Math.Max(0, generationDepth - 1);
            if (generationDepth == 0)
            {
                currentContext = PawnGenerationContext.All; // Reset
            }
        }
    }
}
