using System;
using System.Threading;

namespace NPCStyleLimiter
{
    public static class PawnGenerationState
    {
        private static readonly ThreadLocal<int> generationDepth = new ThreadLocal<int>(() => 0);

        public static bool IsGenerating => generationDepth.Value > 0;

        public static void Enter()
        {
            generationDepth.Value++;
        }

        public static void Exit()
        {
            generationDepth.Value = Math.Max(0, generationDepth.Value - 1);
        }
    }
}
