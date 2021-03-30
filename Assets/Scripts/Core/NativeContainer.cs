using System;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using static Unity.Collections.NativeArrayOptions;

namespace Game.Mechanics.Containers {
    /// <summary>
    /// A single value native container to allow values to be passed between jobs.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="NativeContainer{T}"/>.</typeparam>
    [NativeContainer] [NativeContainerSupportsDeallocateOnJobCompletion]
    public unsafe struct NativeContainer<T> : IDisposable
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] void* m_Buffer;
        Allocator m_AllocatorLabel;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle m_Safety;
        [NativeSetClassTypeToNullOnSchedule] DisposeSentinel m_DisposeSentinel;
#endif

        public NativeContainer(Allocator allocator, NativeArrayOptions options = ClearMemory)
        {
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));

            var size = SizeOf<T>();
            m_Buffer = Malloc(size, AlignOf<T>(), allocator);
            m_AllocatorLabel = allocator;

            #if ENABLE_UNITY_COLLECTIONS_CHECKS
                DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 1, allocator);
            #endif

            if ((options & ClearMemory) == ClearMemory) 
                MemClear(m_Buffer, SizeOf<T>());
        }

        /// <summary>
        /// Gets or sets the value of the unit.
        /// </summary>
        public T Value
        {
            get
            {
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                #endif
                return ReadArrayElement<T>(m_Buffer, 0);
            }

            [WriteAccessRequired]
            readonly set
            {
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                #endif
                WriteArrayElement(m_Buffer, 0, value);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="NativeContainer{T}"/> has been initialized.
        /// </summary>
        public bool IsCreated => (IntPtr)m_Buffer != IntPtr.Zero;

        /// <inheritdoc/>
        [WriteAccessRequired]
        public void Dispose()
        {
            Assert.IsTrue(IsValidAllocator(m_AllocatorLabel));

            #if ENABLE_UNITY_COLLECTIONS_CHECKS
                DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
            #endif
            Free(m_Buffer, m_AllocatorLabel);
            m_Buffer = null;
        }
    }
}