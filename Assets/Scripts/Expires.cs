using Unity.Entities;
using UnityEngine;
using static Instantiation;

namespace DefaultNamespace {
    [RequireComponent(typeof(ConvertToEntity))]
    [DisallowMultipleComponent]
    public class Expires: MonoBehaviour, IConvertGameObjectToEntity {
        public uint Frames;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) => 
            dstManager.AddComponentData(entity, new expires(Frames));
    }
}