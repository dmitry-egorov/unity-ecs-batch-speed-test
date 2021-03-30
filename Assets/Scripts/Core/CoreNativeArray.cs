using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Game.Mechanics.Containers {
    public static class CoreNativeArray {
        public static ref T get_ref<T>(this in NativeArray<T> array, int index)
            where T : unmanaged
        {
            // You might want to validate the index first, as the unsafe method won't do that.
            if (index < 0 || index >= array.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            
            return ref array.get_nocheck_ref(index);
        }
        
        public static unsafe ref T get_nocheck_ref<T>(this in NativeArray<T> array, int index)
            where T : unmanaged =>
            ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafePtr(), index);
        
        
        public static ref T get_ref<T>(this in NativeList<T> array, int index)
            where T : unmanaged
        {
            // You might want to validate the index first, as the unsafe method won't do that.
            if (index < 0 || index >= array.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            
            return ref array.get_nocheck_ref(index);
        }
        
        public static unsafe ref T get_nocheck_ref<T>(this in NativeList<T> array, int index)
            where T : unmanaged =>
            ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafePtr(), index);
    }
}