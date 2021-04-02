using System;
using Game.Mechanics.Containers;
using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Game.Mechanics.Containers.CoreProfiler;
using static Unity.Collections.Allocator;
using static Unity.Mathematics.math;
using static UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

public static class Instantiation {
    const uint frames_per_iteration = 60 * 2;

    [Serializable] public struct has_current_iteration : IComponentData {
        public uint remaining_frames;
        public has_current_iteration(uint remaining_frames) => this.remaining_frames = remaining_frames;
    }
    
    [Serializable] public struct spawns: IComponentData {
        public Entity prefab;
        public uint count_per_fame;

        public spawns(Entity prefab, uint count_per_fame) {
            this.prefab = prefab;
            this.count_per_fame = count_per_fame;
        }
    }

    [Serializable] public struct expires: IComponentData {
        public uint frames;
        public expires(uint frames) => this.frames = frames;
    }
    
    [Serializable] public struct is_a_test_subject: IComponentData {}

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(UpdateWorldTimeSystem))]
    [UsedImplicitly] public class init_fixed_step : SystemBase {
        protected override void OnStartRunning() {
            const float timestep = 1 / 60f;
            World.MaximumDeltaTime = timestep;
            World.GetExistingSystem<FixedStepSimulationSystemGroup>().Timestep = timestep;
            Enabled = false;
        }

        protected override void OnUpdate() { }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    [UsedImplicitly] public class manage_iteration : SystemBase {
        public bool final_frame;

        protected override void OnCreate() {
            EntityManager.CreateEntity(typeof(has_current_iteration));
            SetSingleton(new has_current_iteration(1));
        }

        protected override void OnUpdate() {
            var hci = GetSingleton<has_current_iteration>();

            final_frame = hci.remaining_frames == 0;
            var new_frame = final_frame ? frames_per_iteration : hci.remaining_frames - 1;
            
            SetSingleton(new has_current_iteration(new_frame));
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(expire_entities))]
    [UsedImplicitly] public class destroy_and_instantiate : SystemBase {
        EntityQuery subjects_query;
        EntityQuery spawns_query;

        protected override void OnUpdate() {
            // remaining frames of the current iteration
            var remaining_frames = GetSingleton<has_current_iteration>().remaining_frames;
            
            // randomly determine entities to destroy. Gather them in the destroyed_entities list.
            var subjects_count = subjects_query.CalculateEntityCount();
            using var destroyed_entities = new NativeList<Entity>(subjects_count, TempJob);
            var j1 = Entities.WithName("EnqueueDeletes")
            .WithAll<is_a_test_subject>()
            .ForEach((int entityInQueryIndex, Entity e) => {
                if (hash(uint2((uint) entityInQueryIndex, remaining_frames)) < (uint.MaxValue / 4 * 3)) 
                    destroyed_entities.AddNoResize(e);
            })
            .WithStoreEntityQueryInField(ref subjects_query)
            .Schedule(Dependency);

            // gather entities to spawn, generate positions for them
            var count = spawns_query.CalculateEntityCount();
            using var spawn_prefabs = new NativeList<Entity>(count, TempJob);
            using var spawn_counts = new NativeList<int>(count, TempJob);
            using var positions = new NativeList<float2>(count * 100, TempJob);
            var j2 = Entities.WithName("GeneratePositions")
            .ForEach((int entityInQueryIndex, in spawns spawns, in Translation translation) => {
                var current_prefab_i = spawn_prefabs.Length - 1;
                var instance_count = (int)spawns.count_per_fame;
                var prefab = spawns.prefab;
                if (current_prefab_i != -1 && spawn_prefabs[current_prefab_i] == prefab)
                    spawn_counts.get_ref(current_prefab_i) += instance_count;
                else {
                    spawn_prefabs.Add(prefab);
                    spawn_counts.Add(instance_count);
                }

                var spawn_hash = hash(uint2((uint)entityInQueryIndex, remaining_frames));
                var random = Random.CreateFromIndex(spawn_hash);
                for (var i = 0; i < instance_count; i++) {
                    var position = translation.Value.xz + random.NextFloat2(new float2(-100, -100), new float2(100, 100));
                    positions.Add(position);
                }
            })
            .WithStoreEntityQueryInField(ref spawns_query)
            .Schedule(Dependency);
            
            // wait for the scheduled jobs to complete
            using (Profile("Wait for the jobs"))
                JobHandle.CombineDependencies(j1, j2).Complete();
            
            // destroy entities
            using (Profile($"Destroy {destroyed_entities.Length} subject entities"))
                EntityManager.DestroyEntity(destroyed_entities);

            // instantiate entities
            var instances_count = positions.Length;
            using var instances = new NativeArray<Entity>(instances_count, TempJob);
            using (Profile($"Instantiate {instances_count} entities")) {
                var data_index = 0;
                for (var spawn_i = 0; spawn_i < spawn_prefabs.Length; spawn_i++) {
                    var spawn_count = spawn_counts[spawn_i];
                    var sub_array = instances.GetSubArray(data_index, spawn_count);
                    EntityManager.Instantiate(spawn_prefabs[spawn_i], sub_array);
                    data_index += spawn_count;
                }
            }
            
            // set positions of the instantiated entities
            using (Profile("Set positions")) {
                Dependency = new set_positions_job {
                    instances = instances, 
                    positions = positions, 
                    translation_w = GetComponentDataFromEntity<Translation>()
                }.ScheduleParallel(instances_count, 7, Dependency);
            }
            
            Dependency.Complete();
        }

        [BurstCompile] struct set_positions_job: IJobFor {
            [ReadOnly] public NativeArray<Entity> instances;
            [ReadOnly] public NativeArray<float2> positions;
            [NativeDisableContainerSafetyRestriction] [WriteOnly] public ComponentDataFromEntity<Translation> translation_w;
            public void Execute(int i) => 
                translation_w[instances[i]] = new Translation { Value = positions[i].x0y() };
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UsedImplicitly] public class expire_entities : SystemBase {
        EntityQuery query;
        protected override void OnUpdate() {
            using var expired = new NativeList<Entity>(query.CalculateEntityCount(), TempJob);

            Entities.ForEach((Entity e, ref expires expires) => {
                if (expires.frames <= 0)
                    expired.AddNoResize(e);
                expires.frames -= 1;
            }).WithStoreEntityQueryInField(ref query).Schedule();
            
            using(Profile("Wait for the job"))
            Dependency.Complete();
            
            using(Profile("Destroy expired entities"))
            EntityManager.DestroyEntity(expired);
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(destroy_and_instantiate))]
    [UsedImplicitly] public class calculate_hash_when_iteration_finishes : SystemBase {
        manage_iteration iteration_system;

        protected override void OnCreate() => iteration_system = World.GetOrCreateSystem<manage_iteration>();

        protected override void OnUpdate() {
            if (!iteration_system.final_frame)
                return;
            
            using var ordered_hash_c = new NativeContainer<uint>(TempJob);
            using var unordered_hash_c = new NativeContainer<uint>(TempJob);
            
            Entities.ForEach((in Translation t) => {
                var position_hash = hash(t.Value);
                ordered_hash_c.Value = hash(uint2(ordered_hash_c.Value, position_hash));
                unordered_hash_c.Value += position_hash;
            }).Run();

            Log($"Ordered hash: {ordered_hash_c.Value}, unordered checksum: {unordered_hash_c.Value}");
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(calculate_hash_when_iteration_finishes))]
    [UsedImplicitly] public class remove_all_subject_entities_when_iteration_finishes : SystemBase {
        manage_iteration iteration_system;
        EntityQuery instances_query;

        protected override void OnCreate() {
            iteration_system = World.GetOrCreateSystem<manage_iteration>();
            instances_query = GetEntityQuery(typeof(is_a_test_subject));
        }

        protected override void OnUpdate() {
            if (!iteration_system.final_frame) 
                return;
            
            EntityManager.DestroyEntity(instances_query);
        }
    }
}
