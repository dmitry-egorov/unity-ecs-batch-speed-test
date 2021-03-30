using System;
using System.Runtime.CompilerServices;
using UnityEngine.Profiling;
using static System.Runtime.CompilerServices.MethodImplOptions;

namespace Game.Mechanics.Containers {
    public static class CoreProfiler
    {
        [MethodImpl(AggressiveInlining)]
        public static SampleScope Profile(string name)
        {
            Profiler.BeginSample(name);
            return new SampleScope();
        }

        public struct SampleScope: IDisposable
        {
            [MethodImpl(AggressiveInlining)]
            public void Dispose() => Profiler.EndSample();
        }
    }
}