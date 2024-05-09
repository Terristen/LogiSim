using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Collections;

namespace LogiSim
{
    /// <summary>
    /// A system that aggregates packets in the storage buffer of a machine. We don't want hundreds of single packets in the buffer, 
    /// so we aggregate them.
    /// </summary>
    public partial class AggregateStorageSystem : SystemBase
    {
        /// <changeplan>
        /// modify the system to create new storageCapacityBuffer if the item type and properties are not in the buffer
        /// </changeplan>
        /// <complete />
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
                    NativeHashMap<PacketKey, float> itemTotals = new NativeHashMap<PacketKey, float>(storageBuffer.Length, Allocator.Temp);

                    // Iterate over all packets in the machine's storage
                    for (int j = 0; j < storageBuffer.Length; j++)
                    {
                        Packet packet = storageBuffer[j].Packet;
                        bool found = false;
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
                                found = true;
                                break;
                            }
                        }

                        //we're allowing new item types to be added to the storage capacity buffer so we need to calculate totals for those.
                        //if(!found)
                        //{
                        //    // Create a new storage capacity buffer element
                        //    var newStorageCapacity = new StorageCapacity
                        //    {
                        //        BinType = packet.ItemProperties,
                        //        Capacity = packet.Quantity,
                        //        CurrentQuantity = packet.Quantity
                        //    };

                        //    // Add the new storage capacity buffer element to the buffer
                        //    storageCapacityBuffer.Add(newStorageCapacity);
                        //}
                    }

                    // Iterate over all packets in the machine's storage
                    for (int j = 0; j < storageBuffer.Length; j++)
                    {
                        Packet packet = storageBuffer[j].Packet;
                        PacketKey key = new PacketKey(packet.Type, packet.ItemProperties);

                        // If the item type is already in the NativeHashMap, increase the quantity
                        if (itemTotals.TryGetValue(key, out float quantity))
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
                        float quantity = itemTotalsIterator.Values[i];

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

                    // Transporters have to do this before they can aggregate their storage because they keep packets separate and you can't add a buffer item while iterating over it.
                    //for(int i = 0; i < storageBuffer.Length; i++)
                    //{
                    //    bool found = false;
                    //    for (int j = 0; j < storageCapacityBuffer.Length; j++)
                    //    {
                    //        var scb = storageCapacityBuffer[j];
                    //        if (helperFunctions.IsCompatiblePort(storageBuffer[i].Packet, scb))
                    //        {
                    //            found = true;
                    //            break;
                    //        }
                    //    }
                    //    //we're allowing new item types to be added to the storage capacity buffer so we need to calculate totals for those.
                    //    if (!found)
                    //    {
                    //        // Create a new storage capacity buffer element
                    //        var newStorageCapacity = new StorageCapacity
                    //        {
                    //            BinType = storageBuffer[i].Packet.ItemProperties,
                    //            Capacity = storageBuffer[i].Packet.Quantity,
                    //            CurrentQuantity = storageBuffer[i].Packet.Quantity
                    //        };

                    //        // Add the new storage capacity buffer element to the buffer
                    //        storageCapacityBuffer.Add(newStorageCapacity);
                    //    }
                    //}


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
}
