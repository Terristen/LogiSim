using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Collections;

namespace LogiSim
{
    /// <summary>
    /// A system that generates the output of the recipe and stores it in the storage buffer of the machine. It is responsible for updating
    /// the ProcessingFinished tag and turning off the Processing flag.
    /// </summary>
    public partial class ProcessFinishedSystem : SystemBase
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

            var recipeOutputLookup = GetBufferLookup<RecipeOutputElement>(true);
            var storageBufferLookup = GetBufferLookup<StorageBufferElement>(false);
            var storageCapacittyLookup = GetBufferLookup<StorageCapacity>(false);

            // Iterate over all entities with a Machine and Connections component
            Entities
                .WithNone<IsTransporter, NotPowered>()
                .WithNativeDisableParallelForRestriction(recipeOutputLookup)
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                .WithNativeDisableParallelForRestriction(storageCapacittyLookup)
                .WithAll<ProcessingFinished>().ForEach((Entity entity, int entityInQueryIndex, ref Machine machine) =>
                {

                    var outputs = recipeOutputLookup[entity];
                    var storageBuffer = storageBufferLookup[entity];
                    var storageCapacityBuffer = storageCapacittyLookup[entity];

                    var helperFunctions = new HelperFunctions();

                    bool canComplete = true;
                    for (int i = 0; i < outputs.Length; i++)
                    {
                        if (helperFunctions.GetCapacityAvailable(outputs[i].Packet, storageCapacityBuffer) <= 0)
                        {
                            canComplete = false;
                        }
                    }

                    if (canComplete)
                    {
                        // Create packets according to the RecipeOutput and add them to the Storage buffer
                        for (int i = 0; i < outputs.Length; i++)
                        {
                            Packet outputPacket = outputs[i].Packet;
                            // Create a new packet with the same properties as the output packet
                            Packet newPacket = new Packet { Type = outputPacket.Type, Quantity = outputPacket.Quantity, ItemProperties = outputPacket.ItemProperties, ElapsedTime = 0 };

                            // Add the new packet to the machine's storage
                            storageBuffer.Add(new StorageBufferElement { Packet = newPacket });
                        }

                        // Remove the ProcessingFinished tag
                        commandBuffer.RemoveComponent<ProcessingFinished>(entityInQueryIndex, entity);
                        // turn off the Processing flag
                        machine.Processing = false;
                    }

                }).ScheduleParallel();

            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
