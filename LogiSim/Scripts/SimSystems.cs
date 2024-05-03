using Unity.Entities;
using Unity.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.Entities.UniversalDelegates;
using Unity.VisualScripting;
using UnityEditor.MemoryProfiler;


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
        protected override void OnUpdate()
        {
            // Create a parallel writer for the command buffer
            var commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();

            // Get the buffer lookups for the Connection, Storage, and Transfer components
            var connectionBufferLookup = GetBufferLookup<ConnectionBufferElement>(false);
            var storageBufferLookup = GetBufferLookup<StorageBufferElement>(false);
            var transferBufferLookup = GetBufferLookup<TransferBufferElement>(false);
            var storageCapacityBufferLookup = GetBufferLookup<StorageCapacity>(false);
            var recipeInputElementBufferLookup = GetBufferLookup<RecipeInputElement>(true);

            Entities
                .WithNone<IsTransporter>()
                .WithNativeDisableParallelForRestriction(connectionBufferLookup)
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                .WithNativeDisableParallelForRestriction(transferBufferLookup)
                .WithNativeDisableParallelForRestriction(storageCapacityBufferLookup)
                .WithNativeDisableParallelForRestriction(recipeInputElementBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine) =>
                {
                    var connections = connectionBufferLookup[entity];
                    var storageBuffer = storageBufferLookup[entity];
                    var transferBuffer = transferBufferLookup[entity];
                    

                    HelperFunctions helperFunctions = new HelperFunctions();

                    // Iterate over all connections of the current machine
                    for (int i = 0; i < connections.Length; i++)
                    {
                        var cbe = connections[i];
                        Connection connection = connections[i].connection;

                        if (cbe.RefractoryTimer >= machine.OutputRefractory)
                        {
                            //Debug.Log($"Refractory Timer Past Due: {cbe.RefractoryTimer}");
                            // Get the StorageCapacity buffer of the target entity
                            var targetStorageCapacityBuffer = storageCapacityBufferLookup[connection.ConnectedEntity];
                            var recipeData = recipeInputElementBufferLookup[connection.ConnectedEntity];


                            // Iterate over all packets in the machine's storage
                            for (int j = 0; j < storageBuffer.Length; j++)
                            {
                                Packet packet = storageBuffer[j].Packet;
                                
                                // Create a new packet to transfer
                                Packet transferPacket = new Packet { Type = packet.Type, Quantity = 1, ItemProperties = packet.ItemProperties, ElapsedTime = 0 };

                                bool isImportable = helperFunctions.IsImportable(transferPacket, recipeData, targetStorageCapacityBuffer);
                                //Debug.Log($"Connection to {connection.ConnectedEntity.Index} IsImportable:{isImportable}");

                                // Check if the packet quantity is greater than 0
                                if (packet.Quantity > 0 && isImportable)
                                {

                                    // Create a new packet to transfer
                                    //Packet transferPacket = new Packet { Type = packet.Type, Quantity = 1, ItemProperties = packet.ItemProperties, ElapsedTime = 0 };

                                    // Add the transfer packet to the connected entity's buffer
                                    commandBuffer.AppendToBuffer<TransferBufferElement>(entityInQueryIndex, connection.ConnectedEntity, new TransferBufferElement { Packet = transferPacket });

                                    // Decrease the quantity of the packet in the machine's storage
                                    packet.Quantity--;
                                    storageBuffer[j] = new StorageBufferElement { Packet = packet };

                                    //Debug.Log($"transferred {transferPacket.Quantity} {transferPacket.Type}. Resetting Refractory from {cbe.RefractoryTimer} to 0");
                                    connections[i] = new ConnectionBufferElement { connection = connection, RefractoryTimer = 0 };
                                    
                                    break;
                                }
                            }

                            
                        }
                    }
                }).ScheduleParallel();
            
            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }


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

    

    /// <summary>
    /// A system that aggregates packets in the storage buffer of a machine. We don't want hundreds of single packets in the buffer, 
    /// so we aggregate them.
    /// </summary>
    public partial class AggregateStorageSystem : SystemBase
    {
        protected override void OnUpdate()
        {

            // Get the buffer lookups for the RecipeInput, RecipeOutput, Connection, Storage, and Transfer components
            var storageBufferLookup = GetBufferLookup<StorageBufferElement>(false);
            var transferBufferLookup = GetBufferLookup<TransferBufferElement>(false);

            var storageCapacityBufferLookup = GetBufferLookup<StorageCapacity>(false);
            
            Entities
                .WithNone<IsTransporter>()
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                .WithNativeDisableParallelForRestriction(storageCapacityBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine) =>
                {
                    var storageBuffer = storageBufferLookup[entity];
                    var storageCapacityBuffer = storageCapacityBufferLookup[entity];

                    var helperFunctions = new HelperFunctions();

                    // Reset the current items in the StorageCapacity buffer
                    for (int i = 0; i < storageCapacityBuffer.Length; i++)
                    {
                        var storageCapacity = storageCapacityBuffer[i];
                        storageCapacity.CurrentQuantity = 0;
                        storageCapacityBuffer[i] = storageCapacity;
                    }

                    // Create a NativeHashMap to hold the total quantity of each item type
                    NativeHashMap<PacketKey, int> itemTotals = new NativeHashMap<PacketKey, int>(storageBuffer.Length, Allocator.Temp);

                    // Iterate over all packets in the machine's storage
                    for (int j = 0; j < storageBuffer.Length; j++)
                    {
                        Packet packet = storageBuffer[j].Packet;

                        // Iterate over all storage bins in the StorageCapacity buffer
                        for (int k = 0; k < storageCapacityBuffer.Length; k++)
                        {
                            var scb = storageCapacityBuffer[k];

                            // Check if the storage bin is compatible with the item type
                            if (helperFunctions.IsCompatiblePort(packet, scb))
                            {
                                // Increase the current items in the storage bin
                                scb.CurrentQuantity += packet.Quantity;
                                storageCapacityBuffer[k] = scb;
                                break;
                            }
                        }
                    }

                    // Iterate over all packets in the machine's storage
                    for (int j = 0; j < storageBuffer.Length; j++)
                    {
                        Packet packet = storageBuffer[j].Packet;
                        PacketKey key = new PacketKey(packet.Type, packet.ItemProperties);

                        // If the item type is already in the NativeHashMap, increase the quantity
                        if (itemTotals.TryGetValue(key, out int quantity))
                        {
                            itemTotals[key] = quantity + packet.Quantity;
                        }
                        // Otherwise, add the item type to the NativeHashMap with its quantity
                        else
                        {
                            itemTotals.TryAdd(key, packet.Quantity);
                        }
                        packet.Quantity = 0;
                        storageBuffer[j] = new StorageBufferElement { Packet = packet };
                    }

                    // Create an iterator for the NativeHashMap
                    var itemTotalsIterator = itemTotals.GetKeyValueArrays(Allocator.Temp);

                    for (int i = 0; i < itemTotalsIterator.Length; i++)
                    {
                        // Get the key and value from the iterator
                        PacketKey key = itemTotalsIterator.Keys[i];
                        int quantity = itemTotalsIterator.Values[i];

                        // Create a new packet with the calculated total
                        Packet totalspacket = new Packet
                        {
                            Type = key.Type,
                            ItemProperties = key.Properties,
                            Quantity = quantity
                        };

                        // Add the new packet to the buffer
                        storageBuffer.Add(new StorageBufferElement { Packet = totalspacket });
                    }

                    // Dispose of the iterator to free up memory
                    itemTotalsIterator.Dispose();

                    helperFunctions.CleanBuffer(storageBuffer);

                    itemTotals.Dispose();

                }).ScheduleParallel();

            //Debug.Log("AggregateStorageSystem: Pre-Transport");
            Entities
                .WithAll<IsTransporter>()
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                .WithNativeDisableParallelForRestriction(storageCapacityBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine) =>
                {
                    var storageBuffer = storageBufferLookup[entity];
                    var storageCapacityBuffer = storageCapacityBufferLookup[entity];

                    var helperFunctions = new HelperFunctions();
                    //only one storage capacity for transporters
                    var storageCapacity = storageCapacityBuffer[0];
                    storageCapacity.CurrentQuantity = 0;

                    // Iterate over all packets in the machine's storage
                    for (int j = 0; j < storageBuffer.Length; j++)
                    {
                        Packet packet = storageBuffer[j].Packet;
                        
                        // Check if the storage bin is compatible with the item type
                        if (helperFunctions.IsCompatiblePort(packet, storageCapacity))
                        {
                            // Increase the current items in the storage bin
                            storageCapacity.CurrentQuantity += packet.Quantity;
                        }
                    }
                    storageCapacityBuffer[0] = storageCapacity;

                    helperFunctions.CleanBuffer(storageBuffer);

                }).ScheduleParallel();
        }
    }

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
                .WithNone<IsTransporter,NotPowered>()
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
            var connectionBufferLookup = GetBufferLookup<ConnectionBufferElement>(true);

            Entities
                .WithAll<IsTransporter>()
                .WithNativeDisableParallelForRestriction(storageCapacityBufferLookup)
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                //.WithNativeDisableParallelForRestriction(transferBufferLookup)
                .WithNativeDisableParallelForRestriction(connectionBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine, ref RecipeData recipeData, ref IsTransporter transporter) =>
                {
                    if (!machine.Disabled)
                    {
                        var storageBuffer = storageBufferLookup[entity];
                        var connectionBuffer = connectionBufferLookup[entity];

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
                                // Iterate over all output connections
                                for (int j = 0; j < connectionBuffer.Length; j++)
                                {
                                    Connection connection = connectionBuffer[j].connection;
                                    var targetStorageCapacityBuffer = storageCapacityBufferLookup[connection.ConnectedEntity];

                                    if(helperFunctions.GetCapacityAvailable(packet,targetStorageCapacityBuffer) > 0)
                                    {
                                        packet.ElapsedTime = 0;
                                        // Send the packet to the compatible output's target
                                        var transferPacket = new TransferBufferElement { Packet = new Packet { ElapsedTime = 0, ItemProperties = packet.ItemProperties, Quantity = packet.Quantity, Type = packet.Type } };
                                        commandBuffer.AppendToBuffer<TransferBufferElement>(entityInQueryIndex, connection.ConnectedEntity, transferPacket);

                                        packet.Quantity = 0;
                                        
                                        storageBuffer[i] = new StorageBufferElement { Packet = packet };
                                        
                                        break;
                                    }

                                }
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
                for(int i = 0; i < outputs.Length; i++)
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
                .WithNone<IsTransporter, NotPowered>()
                .WithNativeDisableParallelForRestriction(recipeInputLookup)
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                .WithNone<ProcessingFinished>().ForEach((Entity entity, int entityInQueryIndex, ref Machine machine) =>
                {
                    var inputs = recipeInputLookup[entity];
                    var storageBuffer = storageBufferLookup[entity];

                    if (!machine.Processing && !machine.Disabled)
                    {
                        NativeHashMap<int, int> foundPackets = new NativeHashMap<int, int>(inputs.Length, Allocator.Temp);
                        var helperFunctions = new HelperFunctions();

                        // Check if the machine has the required input items
                        bool hasRequiredItems = true;
                        foreach (RecipeInputElement requiredPacket in inputs)
                        {
                            bool found = false;
                            for (int i = 0; i < storageBuffer.Length; i++)
                            {
                                Packet req = requiredPacket.Packet;
                                Packet sto = storageBuffer[i].Packet;
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

                        // If the machine has the required input items, subtract the required quantities and set Processing to true
                        if (hasRequiredItems)
                        {
                            NativeHashMap<int, int>.Enumerator foundPacketsEnumerator = foundPackets.GetEnumerator();

                            while (foundPacketsEnumerator.MoveNext())
                            {
                                var pair = foundPacketsEnumerator.Current;
                                int index = pair.Key;
                                int quantityToSubtract = pair.Value;

                                Packet packetInStorage = storageBuffer[index].Packet;
                                packetInStorage.Quantity -= quantityToSubtract;
                                storageBuffer[index] = new StorageBufferElement { Packet = packetInStorage };
                            }

                            machine.Processing = true;
                        }

                        foundPackets.Dispose();
                    }

                }).ScheduleParallel();

            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }



    public partial class UpdateConnectionRefractoryTime : SystemBase
    {
        
        protected override void OnUpdate()
        {
            
            var connectionBufferLookup = GetBufferLookup<ConnectionBufferElement>(false);

            Entities
                .WithNativeDisableParallelForRestriction(connectionBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine) =>
                {
                    var connectionBuffer = connectionBufferLookup[entity];

                    if (connectionBuffer.Length > 0)
                    {
                        for (int i = 0; i < connectionBuffer.Length; i++)
                        {
                            var connection = connectionBuffer[i];
                            connection.RefractoryTimer += SystemAPI.Time.DeltaTime;
                            connectionBuffer[i] = connection;
                        }
                    }
                }).ScheduleParallel();

        }
    }

    /// <summary>
    /// Power System
    /// </summary>
    /// 
    public partial class MachinePowerStatusSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystem;

        protected override void OnCreate()
        {
            commandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var storageCapacityLookup = GetBufferLookup<StorageCapacity>(true);
            //var storageBufferLookup = GetBufferLookup<StorageBufferElement>(true);
            //var recipeInputBufferLookup = GetBufferLookup<RecipeInputElement>(true);
            //var recipeOutputBufferLookup = GetBufferLookup<RecipeOutputElement>(true);
            //var connectionBufferLookup = GetBufferLookup<ConnectionBufferElement>(true);

            var commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();


            Entities
                .WithNone<IsTransporter>()
                .WithNativeDisableParallelForRestriction(storageCapacityLookup)
                //.WithNativeDisableParallelForRestriction(storageBufferLookup)
                //.WithNativeDisableParallelForRestriction(recipeInputBufferLookup)
                //.WithNativeDisableParallelForRestriction(recipeOutputBufferLookup)
                //.WithNativeDisableParallelForRestriction(connectionBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine, ref RecipeData recipeData) =>
                {
                    //var recipeInputs = recipeInputBufferLookup[entity];
                    //var recipeOutputs = recipeOutputBufferLookup[entity];
                    //var storageBuffer = storageBufferLookup[entity];
                    var storageCapacityBuffer = storageCapacityLookup[entity];
                    //var connectionBuffer = connectionBufferLookup[entity];

                    // Calculate the power required for the next tick
                    float powerRequired = machine.PowerConsumption * SystemAPI.Time.DeltaTime;
                    var helperFunctions = new HelperFunctions();

                    // Check if the machine has enough power
                    bool hasEnoughPower = false;
                    if (machine.Processing && !machine.Disabled)
                    {
                        hasEnoughPower = true;
                    }
                    else
                    {
                        for (int i = 0; i < storageCapacityBuffer.Length; i++)
                        {
                            if (helperFunctions.MatchesRequirement(storageCapacityBuffer[i].BinType, machine.PowerType) && storageCapacityBuffer[i].CurrentQuantity >= powerRequired)
                            {
                                hasEnoughPower = true;
                                break;
                            }
                        }
                    }

                    // If the machine doesn't have enough power, add the NotPowered tag to it
                    if (!hasEnoughPower)
                    {
                        commandBuffer.AddComponent<NotPowered>(entityInQueryIndex, entity);
                    }
                    else if (SystemAPI.HasComponent<NotPowered>(entity))
                    {
                        commandBuffer.RemoveComponent<NotPowered>(entityInQueryIndex, entity);
                    }


                }).ScheduleParallel();

            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }

    public partial class MachinePowerSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystem;

        protected override void OnCreate()
        {
            commandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            //var storageCapacityLookup = GetBufferLookup<StorageCapacity>(true);
            var storageBufferLookup = GetBufferLookup<StorageBufferElement>(false);
            //var recipeInputBufferLookup = GetBufferLookup<RecipeInputElement>(true);
            //var recipeOutputBufferLookup = GetBufferLookup<RecipeOutputElement>(true);
            //var connectionBufferLookup = GetBufferLookup<ConnectionBufferElement>(true);

            var commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();


            Entities
                .WithNone<IsTransporter,NotPowered>()
                //.WithNativeDisableParallelForRestriction(storageCapacityLookup)
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                //.WithNativeDisableParallelForRestriction(recipeInputBufferLookup)
                //.WithNativeDisableParallelForRestriction(recipeOutputBufferLookup)
                //.WithNativeDisableParallelForRestriction(connectionBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine, ref RecipeData recipeData) =>
                {
                    //var recipeInputs = recipeInputBufferLookup[entity];
                    //var recipeOutputs = recipeOutputBufferLookup[entity];
                    var storageBuffer = storageBufferLookup[entity];
                    //var storageCapacityBuffer = storageCapacityLookup[entity];
                    //var connectionBuffer = connectionBufferLookup[entity];

                    if (!machine.Disabled && machine.Processing)
                    {
                        // Calculate the power required for the next tick
                        float powerRequired = machine.PowerConsumption * SystemAPI.Time.DeltaTime;
                        var helperFunctions = new HelperFunctions();

                        // Check if the machine has enough power
                        bool hasEnoughPower = false;
                        int powerPacketIndex = -1;
                        for (int i = 0; i < storageBuffer.Length; i++)
                        {
                            if (helperFunctions.MatchesRequirement(storageBuffer[i].Packet.ItemProperties, machine.PowerType) && storageBuffer[i].Packet.Quantity >= powerRequired)
                            {
                                hasEnoughPower = true;
                                powerPacketIndex = i;
                                break;
                            }
                        }

                        // If the machine has enough power, update the WorkTimer and consume power
                        if (hasEnoughPower)
                        {
                            machine.WorkTimer += SystemAPI.Time.DeltaTime;

                            // Calculate the time per unit
                            float timePerUnit = 1f / machine.PowerConsumption;

                            // If the WorkTimer has reached the time per unit, consume a unit of power and reset the WorkTimer
                            if (machine.WorkTimer >= timePerUnit)
                            {
                                storageBuffer[powerPacketIndex] = new StorageBufferElement
                                {
                                    Packet = new Packet
                                    {
                                        Type = storageBuffer[powerPacketIndex].Packet.Type,
                                        ItemProperties = storageBuffer[powerPacketIndex].Packet.ItemProperties,
                                        Quantity = storageBuffer[powerPacketIndex].Packet.Quantity - 1
                                    }
                                };

                                machine.WorkTimer -= timePerUnit;
                            }
                        }

                    }


                }).ScheduleParallel();

            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }



    public partial class MachineStatusSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystem;

        protected override void OnCreate()
        {
            commandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var storageCapacityLookup = GetBufferLookup<StorageCapacity>(true);
            var storageBufferLookup = GetBufferLookup<StorageBufferElement>(true);
            var recipeInputBufferLookup = GetBufferLookup<RecipeInputElement>(true);
            var recipeOutputBufferLookup = GetBufferLookup<RecipeOutputElement>(true);
            var connectionBufferLookup = GetBufferLookup<ConnectionBufferElement>(true);

            var commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();


            Entities
                .WithNone<IsTransporter>()
                .WithNativeDisableParallelForRestriction(storageCapacityLookup)
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                .WithNativeDisableParallelForRestriction(recipeInputBufferLookup)
                .WithNativeDisableParallelForRestriction(recipeOutputBufferLookup)
                .WithNativeDisableParallelForRestriction(connectionBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine, ref RecipeData recipeData) =>
            {
                var recipeInputs = recipeInputBufferLookup[entity];
                var recipeOutputs = recipeOutputBufferLookup[entity];
                var storageBuffer = storageBufferLookup[entity];
                var storageCapacityBuffer = storageCapacityLookup[entity];
                var connectionBuffer = connectionBufferLookup[entity];

                // Remove all status tags
                if (SystemAPI.HasComponent<Working>(entity)) { commandBuffer.RemoveComponent<Working>(entityInQueryIndex, entity); }
                if (SystemAPI.HasComponent<InputStarved>(entity)) { commandBuffer.RemoveComponent<InputStarved>(entityInQueryIndex, entity); }
                if (SystemAPI.HasComponent<OutputBlocked>(entity)) { commandBuffer.RemoveComponent<OutputBlocked>(entityInQueryIndex, entity); }
                if (SystemAPI.HasComponent<OutputBufferFull>(entity)) { commandBuffer.RemoveComponent<OutputBufferFull>(entityInQueryIndex, entity); }
                if (SystemAPI.HasComponent<NoRecipe>(entity)) { commandBuffer.RemoveComponent<NoRecipe>(entityInQueryIndex, entity); }
                if (SystemAPI.HasComponent<InputBufferFull>(entity)) { commandBuffer.RemoveComponent<InputBufferFull>(entityInQueryIndex, entity); }
                 


                var helperFunctions = new HelperFunctions();
                
                // Apply status tags based on the machine's status
                if (machine.Processing && !machine.Disabled)
                {
                    commandBuffer.AddComponent<Working>(entityInQueryIndex, entity);
                    // Set the % complete field
                    commandBuffer.SetComponent<Working>(entityInQueryIndex, entity, new Working { PercentComplete = machine.ProcessTimer / (recipeData.ProcessingTime * (1/machine.Efficiency)) });
                }



                // Iterate over all recipe inputs
                for (int i = 0; i < recipeInputs.Length; i++)
                {
                    // Get the current recipe input
                    RecipeInputElement recipeInput = recipeInputs[i];

                    // Count the number of items in storageBuffer that match the current recipe input
                    int storageCount = 0;
                    for (int j = 0; j < storageBuffer.Length; j++)
                    {
                        Packet packet = storageBuffer[j].Packet;
                        if (helperFunctions.MatchesRequirement(packet,recipeInput.Packet))
                        {
                            storageCount += packet.Quantity;
                            if (storageCount > 0)
                            {
                                break;
                            }
                        }
                    }

                    // If the storage count is 0, set the flag for InputStarved
                    if (storageCount == 0)
                    {
                        commandBuffer.AddComponent<InputStarved>(entityInQueryIndex, entity);
                        break; // No need to check the remaining recipe inputs
                    }
                }




                

                // Check if the number of connections is less than the number of recipe outputs
                if (connectionBuffer.Length < recipeOutputs.Length)
                {
                    commandBuffer.AddComponent<OutputBlocked>(entityInQueryIndex, entity);
                }
                else
                {
                    // Check if none of the connections are available to export
                    bool blockedConnection = false;
                    for (int i = 0; i < connectionBuffer.Length; i++)
                    {
                        Connection connection = connectionBuffer[i].connection;
                        
                        // Get the buffer lookup
                        var targetCapacityBuffer = storageCapacityLookup[connection.ConnectedEntity];

                        for(int j = 0; j < recipeOutputs.Length; j++)
                        {
                            Packet outputPacket = recipeOutputs[j].Packet;

                            // Check if the output buffer is compatible with the connected entity's storage capacity
                            if (helperFunctions.GetCapacityAvailable(outputPacket, targetCapacityBuffer) <= 0)
                            {
                                blockedConnection = true;
                                break;
                            }
                        }

                    }

                    if (blockedConnection)
                    {
                        commandBuffer.AddComponent<OutputBlocked>(entityInQueryIndex, entity);
                    }
                }




                
                // Iterate over all recipe outputs
                for (int i = 0; i < recipeOutputs.Length; i++)
                {
                    // Get the current recipe output
                    RecipeOutputElement recipeOutput = recipeOutputs[i];

                    int capacity = helperFunctions.GetCapacityAvailable(recipeOutput.Packet, storageCapacityBuffer);

                    // Check if the total output quantity is greater than or equal to the capacity
                    if (capacity <= 0)
                    {
                        commandBuffer.AddComponent<OutputBufferFull>(entityInQueryIndex, entity);
                        break; // No need to check the remaining recipe outputs
                    }
                }






                if (recipeInputs.Length == 0 && recipeOutputs.Length <= 0)
                {
                    commandBuffer.AddComponent<NoRecipe>(entityInQueryIndex, entity);
                }




                //var storageCapacityBuffer = storageCapacityLookup[entity]; //already defined above
                // Iterate over all recipe outputs
                for (int i = 0; i < recipeInputs.Length; i++)
                {
                    // Get the current recipe output
                    RecipeInputElement recipeInput = recipeInputs[i];

                    int capacity = helperFunctions.GetCapacityAvailable(recipeInput.Packet, storageCapacityBuffer);

                    // Check if the total output quantity is greater than or equal to the capacity
                    if (capacity <= 0)
                    {
                        commandBuffer.AddComponent<InputBufferFull>(entityInQueryIndex, entity);
                        break; // No need to check the remaining recipe inputs
                    }
                }


                ///this might need to be done in machie-specific systems
                //if (/* condition for BlockTransfer */)
                //{
                //    EntityManager.AddComponent<BlockTransfer>(entity);
                //}

            }).ScheduleParallel();

            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }


    public struct HelperFunctions
    {
        public bool IsImportable(Packet packet, DynamicBuffer<RecipeInputElement> recipeInputs, DynamicBuffer<StorageCapacity> storageCapacityBuffer, bool debug = false)
        {
            var currectSCB = GetCapacityData(packet, storageCapacityBuffer);
            if (debug)
            {
                Debug.Log($"IsImportable: {IsCompatiblePort(packet, currectSCB)} && {HasEnoughRoom(packet, currectSCB, debug)} && {IsCorrectType(packet, recipeInputs, currectSCB)}");
            }

            return IsCompatiblePort(packet, currectSCB) && HasEnoughRoom(packet, currectSCB) && IsCorrectType(packet, recipeInputs, currectSCB);
        }


        public bool IsCompatiblePort(Packet packet, StorageCapacity storageCapacity)
        {
            return (storageCapacity.BinType & packet.ItemProperties) == storageCapacity.BinType;
        }

        /// <summary>
        /// Returns true if the provided SCB and RIE buffers indicate that the item is importable.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="storageCapacityBuffer"></param>
        /// <param name="recipeInputs"></param>
        /// <returns></returns>
        public bool IsCorrectType(Packet packet, DynamicBuffer<RecipeInputElement> recipeInputs, StorageCapacity storageCapacity)
        {
            // Check if the item is in the receiving entity's recipes
            bool isInRecipe = false;
            for (int i = 0; i < recipeInputs.Length; i++)
            {
                if (recipeInputs[i].Packet.Type == packet.Type || (IsCompatiblePort(packet,storageCapacity) && recipeInputs[i].Packet.Type == 0)) //0 = Any
                {
                    isInRecipe = true;
                    break;
                }
            }

            return isInRecipe;
        }

        public bool HasEnoughRoom(Packet packet, StorageCapacity storageCapacity, bool debug = false)
        {
            if (debug)
            {
                Debug.Log($"HasEnoughRoom: {storageCapacity.CurrentQuantity} + {packet.Quantity} <= {storageCapacity.Capacity}");
            }
            return storageCapacity.CurrentQuantity + packet.Quantity <= storageCapacity.Capacity;
        }

        public bool HasEnoughRoom(Packet packet, DynamicBuffer<StorageCapacity> storageCapacityBuffer)
        {
            int idx = GetCompatibleOutput(packet, storageCapacityBuffer);
            if (idx == -1)
            {
                return false;
            }
            var storageCapacity = storageCapacityBuffer[idx];
            return storageCapacity.CurrentQuantity + packet.Quantity <= storageCapacity.Capacity;
        }

        public int GetCompatibleOutput(Packet packet, DynamicBuffer<StorageCapacity> storageCapacityBuffer)
        {
            for (int i = 0; i < storageCapacityBuffer.Length; i++)
            {
                if (IsCompatiblePort(packet, storageCapacityBuffer[i]) && HasEnoughRoom(packet, storageCapacityBuffer[i]))
                {
                    return i;
                }
            }

            return -1; // Return -1 if no compatible output is found
        }

        public StorageCapacity GetCapacityData(Packet packet, DynamicBuffer<StorageCapacity> storageCapacityBuffer)
        {
            for (int i = 0; i < storageCapacityBuffer.Length; i++)
            {
                if (IsCompatiblePort(packet, storageCapacityBuffer[i]))
                {
                    return storageCapacityBuffer[i];
                }
            }

            return default; 
        }

        public int GetCapacityAvailable(Packet packet, DynamicBuffer<StorageCapacity> storageCapacityBuffer)
        {
            for (int i = 0; i < storageCapacityBuffer.Length; i++)
            {
                if (IsCompatiblePort(packet, storageCapacityBuffer[i]))
                {
                    return storageCapacityBuffer[i].Capacity - storageCapacityBuffer[i].CurrentQuantity;
                }
            }

            return 0;
        }

        public void CleanBuffer(DynamicBuffer<StorageBufferElement> storageBuffer)
        {
            int startCount = storageBuffer.Length;
            
            for (int i = startCount - 1; i >= 0; i--)
            {
                Packet packet = storageBuffer[i].Packet;
                if (packet.Quantity == 0)
                {
                    storageBuffer.RemoveAt(i);
                }
            }

            //Debug.Log($"Cleaning Buffer: {startCount} -> {storageBuffer.Length}");
        }

        public bool MatchesRequirement(Packet packet, Packet requirement)
        {
            return 
            (
                (
                    (requirement.Type == 0 && (requirement.ItemProperties & packet.ItemProperties) == requirement.ItemProperties)
                 || (requirement.Type != 0 && requirement.Type == packet.Type)
                )
              && packet.Quantity >= requirement.Quantity
            );
            
        }

        public bool MatchesRequirement(ItemProperty itemProperties, ItemProperty requirement)
        {
            return (requirement & itemProperties) == requirement;
        }
    }
}