using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using System;

namespace LogiSim
{
    /// <summary>
    /// A system that transfers packets from one machine to another based on the outgoing connections between them.
    /// An item is only transferred if the connection type matches the packet type and the target machine has the required 
    /// input and is ready to receive. Does not handle transfers from transporters.
    /// </summary>
    /// 
    public partial class PacketTransferSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystem;
        protected override void OnCreate()
        {
            commandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

        }

        /// <changeplan>
        /// resolve the transfer of packets using the new port system and remove the item-level matching logic. only match against the port type. 
        /// also use the new PortID to match connections.
        /// </changeplan>
        protected override void OnUpdate()
        {
            // Create a parallel writer for the command buffer
            var commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();

            // Get the buffer lookups for the Connection, Storage, and Transfer components

            var storageBufferLookup = GetBufferLookup<StorageBufferElement>(false);
            var transferBufferLookup = GetBufferLookup<TransferBufferElement>(false);
            var storageCapacityBufferLookup = GetBufferLookup<StorageCapacity>(false);
            //var recipeOutputElementBufferLookup = GetBufferLookup<RecipeOutputElement>(true);
            var machinePortBufferLookup = GetBufferLookup<MachinePort>(false); // New lookup for MachinePort buffer


            Entities
                .WithNone<IsTransporter>()
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                .WithNativeDisableParallelForRestriction(transferBufferLookup)
                .WithNativeDisableParallelForRestriction(storageCapacityBufferLookup)
                //.WithNativeDisableParallelForRestriction(recipeOutputElementBufferLookup)
                .WithNativeDisableParallelForRestriction(machinePortBufferLookup) // Add restriction for MachinePort buffer
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine) =>
                {


                    var storageBuffer = storageBufferLookup[entity];
                    var transferBuffer = transferBufferLookup[entity];
                    var machinePortBuffer = machinePortBufferLookup[entity]; // Get the MachinePort buffer
                    //var recipeOutputBuffer = recipeOutputElementBufferLookup[entity];


                    HelperFunctions helperFunctions = new HelperFunctions();



                    for (int p = 0; p < machinePortBuffer.Length; p++) //for each port
                    {
                        var port = machinePortBuffer[p];

                        if (port.PortDirection == Direction.In) //ignore in-ports
                        {
                            continue;
                        }

                        if (port.RefractoryTimer < port.RefractoryTime) //ignore ports that are not ready to send
                        {
                            continue;
                        }

                        if (port.ConnectedEntity == Entity.Null) //ignore ports that are not connected
                        {
                            continue;
                        }

                        if (port.ToPortID != 0) //ignore ports that are not connected to a port
                        {
                            continue;
                        }

                        if (port.AssignedPacketType == -1) //ignore ports that are not assigned a packet type
                        {
                            continue;
                        }

                        var selfStorageCapacityBuffer = storageCapacityBufferLookup[entity];
                        var targetStorageCapacityBuffer = storageCapacityBufferLookup[port.ConnectedEntity];
                        Packet recipePacket = new Packet { Type = port.AssignedPacketType, Quantity = port.RecipeQuantity, ItemProperties = port.PortProperty, ElapsedTime = 0 };


                        var storageInfo = helperFunctions.GetCapacityData(recipePacket, selfStorageCapacityBuffer);
                        var capacityData = helperFunctions.GetCapacityData(recipePacket, targetStorageCapacityBuffer);
                        if (helperFunctions.GetCapacityAvailable(recipePacket, targetStorageCapacityBuffer) >= recipePacket.Quantity && storageInfo.CurrentQuantity >= recipePacket.Quantity)
                        {
                            // Create a new packet to transfer
                            Packet transferPacket = new Packet { Type = recipePacket.Type, Quantity = recipePacket.Quantity, ItemProperties = recipePacket.ItemProperties, ElapsedTime = 0 };
                            //Packet removePacket = new Packet { Type = recipePacket.Type, Quantity = -recipePacket.Quantity, ItemProperties = recipePacket.ItemProperties, ElapsedTime = 0 };

                            // Add the transfer packet to the connected entity's buffer
                            commandBuffer.AppendToBuffer<TransferBufferElement>(entityInQueryIndex, port.ConnectedEntity, new TransferBufferElement { Packet = transferPacket });
                            //commandBuffer.AppendToBuffer<TransferBufferElement>(entityInQueryIndex, entity, new TransferBufferElement { Packet = removePacket });

                            // Reset the connection's refractory timer
                            port.RefractoryTimer = 0;
                            machinePortBuffer[p] = port;


                            //remove the packet from the storage
                            float transferAmount = transferPacket.Quantity;
                            float collectedUnits = 0;
                            for (int j = 0; j < storageBuffer.Length; j++)
                            {
                                if (storageBuffer[j].Packet.Type == transferPacket.Type && storageBuffer[j].Packet.Quantity > 0)
                                {
                                    // Calculate how many units we can collect from this packet
                                    float unitsToCollect = Mathf.Min(storageBuffer[j].Packet.Quantity, transferAmount - collectedUnits);

                                    // Subtract the collected units from the packet quantity
                                    storageBuffer[j] = new StorageBufferElement
                                    {
                                        Packet = new Packet
                                        {
                                            Type = storageBuffer[j].Packet.Type,
                                            Quantity = storageBuffer[j].Packet.Quantity - unitsToCollect,
                                            ItemProperties = storageBuffer[j].Packet.ItemProperties,
                                            ElapsedTime = storageBuffer[j].Packet.ElapsedTime
                                        }
                                    };

                                    // Add the collected units to our total
                                    collectedUnits += unitsToCollect;

                                    // If we've collected enough units, break the loop
                                    if (collectedUnits >= transferAmount)
                                    {
                                        break;
                                    }
                                }
                            }

                            if (collectedUnits < transferAmount)
                            {
                                Debug.LogWarning($"Not enough units were found in the storage buffer to satisfy the transfer amount. Machine {entity.Index} : Item {transferPacket.Type} ");
                            }

                        }
                    }
















                    //// Iterate over the connections
                    //for (int i = 0; i < connectionBuffer.Length; i++)
                    //{
                    //    var connection = connectionBuffer[i].connection;
                    //    var port = helperFunctions.GetLocalPort(machinePortBuffer, connection);

                    //    if (connection.ConnectionDirection == Direction.Out && connectionBuffer[i].RefractoryTimer >= port.RefractoryTime) // only sending packets out and that the port is ready to send an item as tracked by the connection's refractory timer
                    //    {

                    //        //if the connection is Out then we need to find the target machine's port
                    //        var targetPortBuffer = machinePortBufferLookup[connection.ConnectedEntity]; // Get the target port buffer
                    //        var targetStorageCapacityBuffer = storageCapacityBufferLookup[connection.ConnectedEntity];
                    //        var targetPortCapacities = storageCapacityBufferLookup[connection.ConnectedEntity];

                    //        Packet recipePacket = new Packet { ItemProperties = connection.ItemProperties, Type = connection.Type};

                    //        var capacityData = helperFunctions.GetCapacityData(new Packet { ItemProperties = connection.ItemProperties }, targetStorageCapacityBuffer);
                    //        float capacityLeft = (capacityData.Capacity == 0)? 0 : capacityData.Capacity - capacityData.CurrentQuantity;

                    //        if (helperFunctions.HasPort(connection.ConnectedEntity, connection.ToPortID, targetPortBuffer))
                    //        {
                    //            var recipe = helperFunctions.GetPortRecipe(recipeOutputBuffer, connection.ItemProperties);

                    //            for(int j = 0; j < storageBuffer.Length; j++)
                    //            {
                    //                Packet packet = storageBuffer[j].Packet;
                    //                if (helperFunctions.MatchesRequirement(packet, recipePacket))
                    //                {
                    //                    if (packet.Quantity >= recipePacket.Quantity && capacityLeft >= recipePacket.Quantity)
                    //                    {
                    //                        // Create a new packet to transfer
                    //                        Packet transferPacket = new Packet { Type = recipePacket.Type, Quantity = recipePacket.Quantity, ItemProperties = recipePacket.ItemProperties, ElapsedTime = 0 };

                    //                        // Add the transfer packet to the connected entity's buffer
                    //                        commandBuffer.AppendToBuffer<TransferBufferElement>(entityInQueryIndex, connection.ConnectedEntity, new TransferBufferElement { Packet = transferPacket });

                    //                        // Decrease the quantity of the packet in the machine's storage
                    //                        packet.Quantity -= recipePacket.Quantity;
                    //                        storageBuffer[j] = new StorageBufferElement { Packet = packet };

                    //                        // Reset the connection's refractory timer
                    //                        connectionBuffer[i] = new ConnectionBufferElement { connection = connection, RefractoryTimer = 0 };

                    //                        break;
                    //                    }
                    //                }
                    //            }   
                    //        }
                    //    }
                    //}


                }).ScheduleParallel();

            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
