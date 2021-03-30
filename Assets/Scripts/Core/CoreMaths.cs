using Unity.Mathematics;

namespace Game.Mechanics.Containers {
    public static class CoreMaths {
        public static float3 x0y(this float2 f2) => new float3(f2.x, 0, f2.y); 
    }
}