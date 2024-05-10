using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Collections;

namespace LogiSim
{
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
            

            var commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();


            Entities
                .WithNone<IsTransporter>()
                .WithNativeDisableParallelForRestriction(storageCapacityLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine, ref RecipeData recipeData) =>
                {
                    var storageCapacityBuffer = storageCapacityLookup[entity];
                    
                    // Calculate the power required for the next tick
                    float powerRequired = machine.PowerConsumption * SystemAPI.Time.DeltaTime;
                    var helperFunctions = new HelperFunctions();

                    // Check if the machine has enough power
                    bool hasEnoughPower = false;
                    if (machine.PowerType == ItemProperty.None)
                    {
                        hasEnoughPower = true;
                    }
                    else if (!machine.Disabled) //machine.Processing && //removed processing check because we want to check for power even if the machine is not processing
                    {
                        for (int i = 0; i < storageCapacityBuffer.Length; i++)
                        {
                            //if(entity.Index == 52) Debug.Log($"Checking {storageCapacityBuffer[i].BinType} against {machine.PowerType} = {helperFunctions.MatchesRequirement(storageCapacityBuffer[i].BinType, machine.PowerType)} && {storageCapacityBuffer[i].CurrentQuantity} >= {powerRequired}");

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
}
