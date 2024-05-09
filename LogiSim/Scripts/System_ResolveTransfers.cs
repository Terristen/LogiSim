using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;


namespace LogiSim
{
    /// <summary>
    /// A system that resolves the transfers of packets between machines by moving them from the transfer buffer to the storage buffer.
    /// </summary>
    public partial class ResolveTransfersSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystem;

        protected override void OnCreate()
        {
            commandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();

            // Get the buffer lookups for the RecipeInput, RecipeOutput, Connection, Storage, and Transfer components
            var storageBufferLookup = GetBufferLookup<StorageBufferElement>(false);
            var transferBufferLookup = GetBufferLookup<TransferBufferElement>(false);

            // Iterate over all entities with a Machine and Connections component
            Entities
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                .WithNativeDisableParallelForRestriction(transferBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine) =>
                {

                    var storageBuffer = storageBufferLookup[entity];
                    var transferBuffer = transferBufferLookup[entity];

                    // Iterate over all packets in the transfer buffer
                    for (int i = 0; i < transferBuffer.Length; i++)
                    {
                        Packet packet = transferBuffer[i].Packet;
                        storageBuffer.Add(new StorageBufferElement { Packet = new Packet { Type = packet.Type, Quantity = packet.Quantity, ItemProperties = packet.ItemProperties, ElapsedTime = 0 } });

                    }

                    transferBuffer.Clear();
                }).ScheduleParallel();

            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
