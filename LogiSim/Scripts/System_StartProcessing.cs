using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Windows;

namespace LogiSim
{
    /// <summary>
    /// A system that checks if a machine has the required input packets to start processing. If it does, it subtracts the required
    /// quantities from the storage buffer and sets the Processing flag to true.
    /// </summary>
    public partial class StartProcessingSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystem;

        protected override void OnCreate()
        {
            commandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();

            // Get the buffer lookups for the RecipeInput and Storage components
            var recipeInputLookup = GetBufferLookup<RecipeInputElement>(true);
            var storageBufferLookup = GetBufferLookup<StorageBufferElement>(false);

            // Iterate over all entities with a Machine component and without a ProcessingFinished component
            Entities
                .WithNone<IsTransporter, NotPowered, OutputBufferFull>()
                .WithNativeDisableParallelForRestriction(recipeInputLookup)
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                .WithNone<ProcessingFinished>().ForEach((Entity entity, int entityInQueryIndex, ref Machine machine) =>
                {
                    bool debug = false;
                    //just testing the one machine
                    if (entity.Index == 50)
                    {
                        debug = false;
                    }

                    var inputs = recipeInputLookup[entity];
                    var storageBuffer = storageBufferLookup[entity];

                    if (debug) { Debug.Log($"InputElements: {inputs.Length} - {!machine.Processing} && {!machine.Disabled}"); }

                    if (!machine.Processing && !machine.Disabled)
                    {
                        NativeHashMap<int, float> foundPackets = new NativeHashMap<int, float>(inputs.Length, Allocator.Temp);
                        var helperFunctions = new HelperFunctions();

                        if (debug) { Debug.Log($"Entity {entity.Index} has {storageBuffer.Length} StorageBuffers and {inputs.Length} Inputs"); }
                        // Check if the machine has the required input items
                        bool hasRequiredItems = true;
                        foreach (RecipeInputElement requiredPacket in inputs)
                        {
                            if (debug) { Debug.Log($"Checking {requiredPacket.Packet.Quantity} {requiredPacket.Packet.Type}"); }

                            if (helperFunctions.MatchesRequirement(requiredPacket.Packet.ItemProperties,machine.PowerType))
                            {
                                continue;
                            }

                            bool found = false;
                            for (int i = 0; i < storageBuffer.Length; i++)
                            {
                                //Debug.Log($"Checking {requiredPacket.Packet.Quantity} {requiredPacket.Packet.Type} against {storageBuffer[i].Packet.Quantity} {storageBuffer[i].Packet.Type} = {helperFunctions.MatchesRequirement(storageBuffer[i].Packet, requiredPacket.Packet)}");
                                Packet req = requiredPacket.Packet;
                                Packet sto = storageBuffer[i].Packet;
                                if (debug) { Debug.Log($"Checking {req.Quantity} {req.Type} against {sto.Quantity} {sto.Type} = {helperFunctions.MatchesRequirement(sto, req)}"); }
                                if (helperFunctions.MatchesRequirement(sto, req))
                                {
                                    found = true;
                                    foundPackets.Add(i, req.Quantity);
                                    break;
                                }
                            }

                            if (!found)
                            {
                                hasRequiredItems = false;
                                break;
                            }
                        }

                        if (debug) { Debug.Log($"Has Required Items: {hasRequiredItems}"); }

                        // If the machine has the required input items, subtract the required quantities and set Processing to true
                        if (hasRequiredItems)
                        {
                            if (debug) { Debug.Log($"Action Loop for Starting Processing"); }
                            NativeHashMap<int, float>.Enumerator foundPacketsEnumerator = foundPackets.GetEnumerator();

                            while (foundPacketsEnumerator.MoveNext())
                            {
                                var pair = foundPacketsEnumerator.Current;
                                int index = pair.Key;
                                float quantityToSubtract = pair.Value;

                                Packet packetInStorage = storageBuffer[index].Packet;
                                packetInStorage.Quantity -= quantityToSubtract;
                                storageBuffer[index] = new StorageBufferElement { Packet = packetInStorage };
                            }
                            if (debug) { Debug.Log($"Processing Initiated."); }
                            machine.Processing = true;
                        }

                        foundPackets.Dispose();
                    }

                }).ScheduleParallel();

            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
