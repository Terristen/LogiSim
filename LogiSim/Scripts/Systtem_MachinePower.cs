using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace LogiSim
{
    public partial class MachinePowerSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystem;

        protected override void OnCreate()
        {
            commandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var storageCapacityLookup = GetBufferLookup<StorageCapacity>(true);
            var storageBufferLookup = GetBufferLookup<StorageBufferElement>(false);
            

            var commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();


            Entities
                .WithNone<IsTransporter, NotPowered, OutputBufferFull>()
                .WithNativeDisableParallelForRestriction(storageCapacityLookup)
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine, ref RecipeData recipeData) =>
                {
                    var storageBuffer = storageBufferLookup[entity];
                    var storageCapacityBuffer = storageCapacityLookup[entity];
                    
                    if (!machine.Disabled && machine.Processing)
                    {
                        // Calculate the power required for the next tick
                        float powerRequired = machine.PowerConsumption * SystemAPI.Time.DeltaTime;
                        var helperFunctions = new HelperFunctions();

                        // Check if the machine has enough power
                        bool hasEnoughPower = false;
                        float powerCollected = 0f;

                        for (int i = 0; i < storageCapacityBuffer.Length; i++)
                        {
                            if (helperFunctions.MatchesRequirement(storageCapacityBuffer[i].BinType, machine.PowerType) && storageCapacityBuffer[i].CurrentQuantity >= powerRequired)
                            {
                                hasEnoughPower = true;
                                break;
                            }
                        }


                        if (hasEnoughPower)
                        {
                            
                            for (int i=0; i < storageBuffer.Length; i++)
                            {
                                if (helperFunctions.MatchesRequirement(storageBuffer[i].Packet.ItemProperties, machine.PowerType))
                                {
                                    // Calculate how many units we can collect from this packet
                                    float unitsToCollect = Math.Min(storageBuffer[i].Packet.Quantity, powerRequired - powerCollected);

                                    powerCollected += unitsToCollect;

                                    // Subtract the collected units from the packet quantity
                                    storageBuffer[i] = new StorageBufferElement
                                    {
                                        Packet = new Packet
                                        {
                                            Type = storageBuffer[i].Packet.Type,
                                            Quantity = storageBuffer[i].Packet.Quantity - unitsToCollect,
                                            ItemProperties = storageBuffer[i].Packet.ItemProperties,
                                            ElapsedTime = storageBuffer[i].Packet.ElapsedTime
                                        }
                                    };

                                   

                                    // If we've collected enough units, break the loop
                                    if (powerCollected >= powerRequired)
                                    {
                                        break;
                                    }
                                }
                            }
                        }


                    }


                }).ScheduleParallel();

            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
