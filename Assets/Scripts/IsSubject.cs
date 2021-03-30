using Unity.Entities;
using UnityEngine;
using static Instantiation;

namespace DefaultNamespace {
    [RequireComponent(typeof(ConvertToEntity))]
    [DisallowMultipleComponent]
    public class IsSubject: MonoBehaviour, IConvertGameObjectToEntity {
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) => 
            dstManager.AddComponent<is_a_test_subject>(entity);
    }
}