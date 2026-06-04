// Copyright (c) 2026 rinmiolc
// Licensed under the GNU General Public License v3.0.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading;

namespace NPCStyleLimiter
{
    public static class PawnGenerationState
    {
        [ThreadStatic]
        private static int generationDepth;

        public static bool IsGenerating => generationDepth > 0;

        public static void Enter()
        {
            generationDepth++;
        }

        public static void Exit()
        {
            generationDepth = Math.Max(0, generationDepth - 1);
        }
    }
}
