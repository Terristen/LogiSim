using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using System;
using UnityEditor.Experimental.GraphView;

namespace LogiSim
{
    /// <summary>
    /// A system that processes the packets in the storage buffer of a machine according to the recipe data. It is also responsible for
    /// updating the ProcessTimer. For regular machines it is a sinple timer, for transporters it is a distance-based timer for each packet.
    /// </summary>
    public partial class ProcessMachineSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystem;

        protected override void OnCreate()
        {
            commandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            var deltaTime = SystemAPI.Time.DeltaTime;

            Entities
                .WithNone<IsTransporter, NotPowered, OutputBufferFull>()
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine, ref RecipeData recipeData) =>
                {
                    if (machine.Processing && !machine.Disabled)
                    {
                        machine.ProcessTimer += deltaTime;

                        // Calculate the adjusted processing time based on the machine's efficiency
                        float adjustedProcessingTime = recipeData.ProcessingTime / machine.Efficiency;

                        if (machine.ProcessTimer >= adjustedProcessingTime)
                        {
                            commandBuffer.AddComponent<ProcessingFinished>(entityInQueryIndex, entity);
                            machine.ProcessTimer = 0;
                        }
                    }

                }).ScheduleParallel();

            var storageCapacityBufferLookup = GetBufferLookup<StorageCapacity>(false);
            var storageBufferLookup = GetBufferLookup<StorageBufferElement>(false);
            var machinePortBufferLookup = GetBufferLookup<MachinePort>(false); // New lookup for MachinePort buffer

            Entities
                .WithAll<IsTransporter>()
                .WithNativeDisableParallelForRestriction(storageCapacityBufferLookup)
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                .WithNativeDisableParallelForRestriction(machinePortBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine, ref RecipeData recipeData, ref IsTransporter transporter) =>
                {

                    ///this system needs debugging. it's not working right yet.

                    if (machine.Disabled)
                    {
                        return;
                    }

                    var storageBuffer = storageBufferLookup[entity];
                    var machinePortBuffer = machinePortBufferLookup[entity];
                    var helperFunctions = new HelperFunctions();

                    if (storageBuffer.Length == 0)
                    {
                        return;
                    }

                    // Sorts oldest to the front
                    helperFunctions.SortPacketsByElapsedTimeDesc(ref storageBuffer);

                    // Calculate the total transfer time for the packet along the transporter
                    float totalTransferTime = recipeData.ProcessingTime * transporter.Length;


                    var packet = storageBuffer[0].Packet;
                    bool preventAdvancement = false;
                    if (packet.ElapsedTime + SystemAPI.Time.DeltaTime >= totalTransferTime)
                    {
                        float availableCapacity = 0;
                        MachinePort matchingPort = new MachinePort { PortID = -1 };
                        for (int p = 0; p < machinePortBuffer.Length; p++)
                        {
                            var port = machinePortBuffer[p];

                            if (port.PortDirection != Direction.Out)
                            {
                                continue;
                            }

                            if (!helperFunctions.MatchesRequirement(packet.ItemProperties, port.PortProperty))
                            {
                                continue;
                            }

                            if (port.RefractoryTimer < port.RefractoryTime)
                            {
                                continue;
                            }

                            var tgtCap = storageCapacityBufferLookup[matchingPort.ConnectedEntity];
                            availableCapacity = helperFunctions.GetCapacityAvailable(packet, tgtCap);
                            if (availableCapacity <= 0)
                            {
                                continue;
                            }
                            matchingPort = port;
                        }

                        if (matchingPort.PortID != -1) // found a matching port
                        {
                            preventAdvancement = false;

                            // Transfer the packet
                            float transferableQuantity = Mathf.Min(packet.Quantity, availableCapacity);
                            float leftoverQuantity = packet.Quantity - transferableQuantity;

                            var transferPacket = new TransferBufferElement { Packet = new Packet { ElapsedTime = 0, ItemProperties = packet.ItemProperties, Quantity = transferableQuantity, Type = packet.Type } };
                            commandBuffer.AppendToBuffer<TransferBufferElement>(entityInQueryIndex, matchingPort.ConnectedEntity, transferPacket);

                            if(storageBuffer.Length > 1)
                            {
                                var nxtPacket = storageBuffer[1].Packet;
                                nxtPacket.Quantity += leftoverQuantity;
                                storageBuffer[1] = new StorageBufferElement { Packet = nxtPacket };
                            } else
                            {
                                packet.Quantity = leftoverQuantity;
                                packet.ElapsedTime += SystemAPI.Time.DeltaTime; //extra time to make sure it's the next one taken
                                storageBuffer[0] = new StorageBufferElement { Packet = packet };
                            }
                        }
                        else //no matching port found for this packet
                        {
                            preventAdvancement = true;
                        }

                    }

                    if(!preventAdvancement)
                    {
                        for (int i = 1; i < storageBuffer.Length; i++)
                        {
                            var pckt = storageBuffer[i].Packet;
                            packet.ElapsedTime = Math.Min(packet.ElapsedTime + SystemAPI.Time.DeltaTime, totalTransferTime);
                            storageBuffer[i] = new StorageBufferElement { Packet = pckt };
                        }
                    }

                    helperFunctions.CleanBuffer(storageBuffer);

                }).ScheduleParallel();

            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
