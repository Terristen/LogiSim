using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using System;

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
            //var transferBufferLookup = GetBufferLookup<TransferBufferElement>(false);
            //var connectionBufferLookup = GetBufferLookup<ConnectionBufferElement>(true);
            var machinePortBufferLookup = GetBufferLookup<MachinePort>(true); // New lookup for MachinePort buffer

            Entities
                .WithAll<IsTransporter>()
                //.WithNone<OutputBufferFull>()
                .WithNativeDisableParallelForRestriction(storageCapacityBufferLookup)
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                .WithNativeDisableParallelForRestriction(machinePortBufferLookup)
                //.WithNativeDisableParallelForRestriction(connectionBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine, ref RecipeData recipeData, ref IsTransporter transporter) =>
                {
                    if (!machine.Disabled)
                    {
                        var storageBuffer = storageBufferLookup[entity];
                        var machinePortBuffer = machinePortBufferLookup[entity];

                        var helperFunctions = new HelperFunctions();

                        // Iterate over all packets in the machine's storage, backwards
                        for (int i = 0; i < storageBuffer.Length; i++)
                        {
                            Packet packet = storageBuffer[i].Packet;

                            // Increment the packet's ElapsedTime and cap it to the total transfer time
                            float totalTransferTime = recipeData.ProcessingTime * transporter.Length;

                            packet.ElapsedTime = Math.Min(packet.ElapsedTime + SystemAPI.Time.DeltaTime, totalTransferTime);

                            // Check if the packet's ElapsedTime is greater than or equal to the total transfer time
                            if (packet.ElapsedTime >= totalTransferTime)
                            {

                                for (int p = 0; p < machinePortBuffer.Length; p++)
                                {
                                    var port = machinePortBuffer[p];
                                    var targetStorageCapacityBuffer = storageCapacityBufferLookup[port.ConnectedEntity];
                                    if (port.PortDirection == Direction.Out && port.AssignedPacketType == packet.Type && helperFunctions.GetCapacityAvailable(packet, targetStorageCapacityBuffer) > packet.Quantity)
                                    {
                                        var transferPacket = new TransferBufferElement { Packet = new Packet { ElapsedTime = 0, ItemProperties = packet.ItemProperties, Quantity = packet.Quantity, Type = packet.Type } };
                                        commandBuffer.AppendToBuffer<TransferBufferElement>(entityInQueryIndex, port.ConnectedEntity, transferPacket);
                                        packet.Quantity = 0;
                                        storageBuffer[i] = new StorageBufferElement { Packet = packet };
                                        break;
                                    }
                                }


                                //// Iterate over all output connections
                                //for (int j = 0; j < connectionBuffer.Length; j++)
                                //{
                                //    Connection connection = connectionBuffer[j].connection;
                                //    var targetStorageCapacityBuffer = storageCapacityBufferLookup[connection.ConnectedEntity];

                                //    if(helperFunctions.GetCapacityAvailable(packet,targetStorageCapacityBuffer) > 0)
                                //    {
                                //        packet.ElapsedTime = 0;
                                //        // Send the packet to the compatible output's target
                                //        var transferPacket = new TransferBufferElement { Packet = new Packet { ElapsedTime = 0, ItemProperties = packet.ItemProperties, Quantity = packet.Quantity, Type = packet.Type } };
                                //        commandBuffer.AppendToBuffer<TransferBufferElement>(entityInQueryIndex, connection.ConnectedEntity, transferPacket);

                                //        packet.Quantity = 0;

                                //        storageBuffer[i] = new StorageBufferElement { Packet = packet };

                                //        break;
                                //    }

                                //}
                            }
                            else
                            {
                                // Update the packet in the machine's storage
                                storageBuffer[i] = new StorageBufferElement { Packet = packet };
                            }

                            helperFunctions.CleanBuffer(storageBuffer);
                        }
                    }

                }).ScheduleParallel();

            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
