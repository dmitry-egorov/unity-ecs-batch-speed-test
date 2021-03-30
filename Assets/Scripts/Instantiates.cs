using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using static Instantiation;

namespace DefaultNamespace {
    [RequireComponent(typeof(ConvertToEntity))]
    [DisallowMultipleComponent]
    public class Instantiates: MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs {
        public GameObject Prefab;
        public uint Count;

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs) => referencedPrefabs.Add(Prefab);

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
            var prefab = conversionSystem.GetPrimaryEntity(Prefab);
            dstManager.AddComponentData(entity, new spawns(prefab, Count));
        }
    }
}