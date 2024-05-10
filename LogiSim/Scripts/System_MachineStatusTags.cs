using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace LogiSim
{
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
            //var connectionBufferLookup = GetBufferLookup<ConnectionBufferElement>(true);
            var machinePortBufferLookup = GetBufferLookup<MachinePort>(true); // New lookup for MachinePort buffer

            var commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();


            Entities
                .WithNone<IsTransporter>()
                .WithNativeDisableParallelForRestriction(storageCapacityLookup)
                .WithNativeDisableParallelForRestriction(storageBufferLookup)
                .WithNativeDisableParallelForRestriction(recipeInputBufferLookup)
                .WithNativeDisableParallelForRestriction(recipeOutputBufferLookup)
                .WithNativeDisableParallelForRestriction(machinePortBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine, ref RecipeData recipeData) =>
                {
                    var recipeInputs = recipeInputBufferLookup[entity];
                    var recipeOutputs = recipeOutputBufferLookup[entity];
                    var storageBuffer = storageBufferLookup[entity];
                    var storageCapacityBuffer = storageCapacityLookup[entity];
                    var machinePortBuffer = machinePortBufferLookup[entity];
                    
                    var runtimeStamp = SystemAPI.Time.ElapsedTime;

                   
                    bool hasWorking = false;
                    bool hasInputStarved = false;
                    bool hasInputBlocked = false;
                    bool hasOutputBlocked = false;
                    bool hasOutputBufferFull = false;
                    bool hasNoRecipe = false;
                    bool hasInputBufferFull = false;

                    
                    var helperFunctions = new HelperFunctions();

                    // Apply status tags based on the machine's status
                    if (machine.Processing && !machine.Disabled && !SystemAPI.HasComponent<NotPowered>(entity))
                    {
                        hasWorking = true;
                        
                    }

                    //port states
                    for (int i = 0; i < machinePortBuffer.Length; i++)
                    {
                        StorageCapacity capBuffer = new StorageCapacity { Capacity = -1 };
                        foreach (var storageCapacity in storageCapacityBuffer)
                        {
                            if (helperFunctions.MatchesRequirement(storageCapacity.BinType, machinePortBuffer[i].PortProperty))
                            {
                                capBuffer = storageCapacity;
                                break;
                            }
                        }

                        if(capBuffer.Capacity == -1) //no matching capacity found
                        {
                            if (machinePortBuffer[i].PortDirection == Direction.Out)
                            {
                                hasOutputBlocked = true;
                            }
                            else
                            {
                                hasInputBlocked = true;
                            }
                        } else if(capBuffer.CurrentQuantity < machinePortBuffer[i].RecipeQuantity) //not enough items in storage
                        {
                            if (machinePortBuffer[i].PortDirection == Direction.In)
                            {
                                hasInputStarved = true;
                            }
                        } else if(capBuffer.CurrentQuantity >= capBuffer.Capacity) //storage is full
                        {
                            if (machinePortBuffer[i].PortDirection == Direction.Out)
                            {
                                hasOutputBufferFull = true;
                            } else
                            {
                                hasInputBufferFull = true;
                            }
                        }
                    }


                    if (recipeInputs.Length == 0 && recipeOutputs.Length <= 0)
                    {
                        hasNoRecipe = true;
                    }


                    if (SystemAPI.HasComponent<OutputBlocked>(entity) && !hasOutputBlocked)
                    {
                        commandBuffer.RemoveComponent<OutputBlocked>(entityInQueryIndex, entity);
                    } else if (hasOutputBlocked)
                    {
                        commandBuffer.AddComponent<OutputBlocked>(entityInQueryIndex, entity);
                    }

                    if (SystemAPI.HasComponent<InputBlocked>(entity) && !hasInputBlocked)
                    {
                        commandBuffer.RemoveComponent<InputBlocked>(entityInQueryIndex, entity);
                    }
                    else if (hasInputBlocked)
                    {
                        commandBuffer.AddComponent<InputBlocked>(entityInQueryIndex, entity);
                    }

                    if (SystemAPI.HasComponent<InputStarved>(entity) && !hasInputStarved)
                    {
                        commandBuffer.RemoveComponent<InputStarved>(entityInQueryIndex, entity);
                    }
                    else if (hasInputStarved)
                    {
                        commandBuffer.AddComponent<InputStarved>(entityInQueryIndex, entity);
                    }
                    
                    if (SystemAPI.HasComponent<OutputBufferFull>(entity) && !hasOutputBufferFull)
                    {
                        commandBuffer.RemoveComponent<OutputBufferFull>(entityInQueryIndex, entity);
                    }
                    else if (hasOutputBufferFull)
                    {
                        commandBuffer.AddComponent<OutputBufferFull>(entityInQueryIndex, entity);
                    }

                    if (SystemAPI.HasComponent<InputBufferFull>(entity) && !hasInputBufferFull)
                    {
                        commandBuffer.RemoveComponent<InputBufferFull>(entityInQueryIndex, entity);
                    }
                    else if (hasInputBufferFull)
                    {
                        commandBuffer.AddComponent<InputBufferFull>(entityInQueryIndex, entity);
                    }

                    if (SystemAPI.HasComponent<Working>(entity) && !hasWorking)
                    {
                        commandBuffer.RemoveComponent<Working>(entityInQueryIndex, entity);
                    }
                    else if (hasWorking)
                    {
                        commandBuffer.AddComponent<Working>(entityInQueryIndex, entity);
                        // Set the % complete field
                        commandBuffer.SetComponent<Working>(entityInQueryIndex, entity, new Working { PercentComplete = machine.ProcessTimer / (recipeData.ProcessingTime * (1 / machine.Efficiency)) });
                    }

                    if (SystemAPI.HasComponent<NoRecipe>(entity) && !hasNoRecipe)
                    {
                        commandBuffer.RemoveComponent<NoRecipe>(entityInQueryIndex, entity);
                    }
                    else if (hasNoRecipe)
                    {
                        commandBuffer.AddComponent<NoRecipe>(entityInQueryIndex, entity);
                    }

                }).ScheduleParallel();

            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
