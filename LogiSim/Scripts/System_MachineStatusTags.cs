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

                    // Remove all status tags
                    //if (SystemAPI.HasComponent<Working>(entity)) { commandBuffer.RemoveComponent<Working>(entityInQueryIndex, entity); }
                    //if (SystemAPI.HasComponent<InputStarved>(entity)) { commandBuffer.RemoveComponent<InputStarved>(entityInQueryIndex, entity); }
                    //if (SystemAPI.HasComponent<InputBlocked>(entity)) { commandBuffer.RemoveComponent<InputBlocked>(entityInQueryIndex, entity); }
                    //if (SystemAPI.HasComponent<OutputBlocked>(entity)) { commandBuffer.RemoveComponent<OutputBlocked>(entityInQueryIndex, entity); }
                    //if (SystemAPI.HasComponent<OutputBufferFull>(entity)) { commandBuffer.RemoveComponent<OutputBufferFull>(entityInQueryIndex, entity); }
                    //if (SystemAPI.HasComponent<NoRecipe>(entity)) { commandBuffer.RemoveComponent<NoRecipe>(entityInQueryIndex, entity); }
                    //if (SystemAPI.HasComponent<InputBufferFull>(entity)) { commandBuffer.RemoveComponent<InputBufferFull>(entityInQueryIndex, entity); }

                    //Debug.Log($"removed all status tags for {entity.Index} at {runtimeStamp}");
                    
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
                        //commandBuffer.AddComponent<Working>(entityInQueryIndex, entity);
                        //// Set the % complete field
                        //commandBuffer.SetComponent<Working>(entityInQueryIndex, entity, new Working { PercentComplete = machine.ProcessTimer / (recipeData.ProcessingTime * (1 / machine.Efficiency)) });
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
                                //if (!SystemAPI.HasComponent<OutputBlocked>(entity))
                                //{
                                //    commandBuffer.AddComponent<OutputBlocked>(entityInQueryIndex, entity);
                                //}
                                hasOutputBlocked = true;
                            }
                            else
                            {
                                //if (!SystemAPI.HasComponent<InputBlocked>(entity))
                                //{
                                //    commandBuffer.AddComponent<InputBlocked>(entityInQueryIndex, entity);
                                //}
                                hasInputBlocked = true;
                            }
                        } else if(capBuffer.CurrentQuantity < machinePortBuffer[i].RecipeQuantity) //not enough items in storage
                        {
                            if (machinePortBuffer[i].PortDirection == Direction.In)
                            {
                                //if (!SystemAPI.HasComponent<InputStarved>(entity))
                                //{
                                //    commandBuffer.AddComponent<InputStarved>(entityInQueryIndex, entity);
                                //}
                                hasInputStarved = true;
                            }
                        } else if(capBuffer.CurrentQuantity >= capBuffer.Capacity) //storage is full
                        {
                            if (machinePortBuffer[i].PortDirection == Direction.Out)
                            {
                                //if (!SystemAPI.HasComponent<OutputBufferFull>(entity))
                                //{
                                //    commandBuffer.AddComponent<OutputBufferFull>(entityInQueryIndex, entity);
                                //}
                                hasOutputBufferFull = true;
                            } else
                            {
                                //if (!SystemAPI.HasComponent<InputBufferFull>(entity))
                                //{
                                //    commandBuffer.AddComponent<InputBufferFull>(entityInQueryIndex, entity);
                                //}
                                hasInputBufferFull = true;
                            }
                        }
                    }


                    if (recipeInputs.Length == 0 && recipeOutputs.Length <= 0)
                    {
                        hasNoRecipe = true;
                        //commandBuffer.AddComponent<NoRecipe>(entityInQueryIndex, entity);
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

                    //// Iterate over all recipe inputs
                    //for (int i = 0; i < recipeInputs.Length; i++)
                    //{
                    //    // Get the current recipe input
                    //    RecipeInputElement recipeInput = recipeInputs[i];

                    //    // Count the number of items in storageBuffer that match the current recipe input
                    //    float storageCount = 0;
                    //    for (int j = 0; j < storageBuffer.Length; j++)
                    //    {
                    //        Packet packet = storageBuffer[j].Packet;
                    //        if (helperFunctions.MatchesRequirement(packet, recipeInput.Packet))
                    //        {
                    //            storageCount += packet.Quantity;
                    //            if (storageCount > 0)
                    //            {
                    //                break;
                    //            }
                    //        }
                    //    }

                    //    // If the storage count is 0, set the flag for InputStarved
                    //    if (storageCount == 0)
                    //    {
                    //        commandBuffer.AddComponent<InputStarved>(entityInQueryIndex, entity);
                    //        break; // No need to check the remaining recipe inputs
                    //    }
                    //}




                    //for (int p = 0; p < machinePortBuffer.Length; p++)
                    //{
                    //    var port = machinePortBuffer[p];
                    //    bool outputBlocked = false;
                    //    if (port.PortDirection == Direction.Out)
                    //    {
                    //        if (port.AssignedPacketType == -1)
                    //        {
                    //            outputBlocked = true;
                    //        }
                    //        else
                    //        {
                    //            var targetCapacityBuffer = storageCapacityLookup[port.ConnectedEntity];
                    //            if (helperFunctions.GetCapacityAvailable(new Packet { ItemProperties = port.PortProperty, Quantity = port.RecipeQuantity, Type = port.AssignedPacketType }, targetCapacityBuffer) <= 0)
                    //            {
                    //                outputBlocked = true;
                    //            }
                    //        }
                    //    }
                    //    if (outputBlocked)
                    //    {
                    //        commandBuffer.AddComponent<OutputBlocked>(entityInQueryIndex, entity);
                    //        break;
                    //    }
                    //}



                    //// Iterate over all recipe outputs
                    //for (int i = 0; i < recipeOutputs.Length; i++)
                    //{
                    //    // Get the current recipe output
                    //    RecipeOutputElement recipeOutput = recipeOutputs[i];

                    //    float capacity = helperFunctions.GetCapacityAvailable(recipeOutput.Packet, storageCapacityBuffer);

                    //    // Check if the total output quantity is greater than or equal to the capacity
                    //    if (capacity <= 0)
                    //    {
                    //        commandBuffer.AddComponent<OutputBufferFull>(entityInQueryIndex, entity);
                    //        break; // No need to check the remaining recipe outputs
                    //    }
                    //}






                    




                    ////var storageCapacityBuffer = storageCapacityLookup[entity]; //already defined above
                    //// Iterate over all recipe outputs
                    //for (int i = 0; i < recipeInputs.Length; i++)
                    //{
                    //    // Get the current recipe output
                    //    RecipeInputElement recipeInput = recipeInputs[i];

                    //    float capacity = helperFunctions.GetCapacityAvailable(recipeInput.Packet, storageCapacityBuffer);

                    //    // Check if the total output quantity is greater than or equal to the capacity
                    //    if (capacity <= 0)
                    //    {
                    //        commandBuffer.AddComponent<InputBufferFull>(entityInQueryIndex, entity);
                    //        break; // No need to check the remaining recipe inputs
                    //    }
                    //}


                    ///this might need to be done in machie-specific systems
                    //if (/* condition for BlockTransfer */)
                    //{
                    //    EntityManager.AddComponent<BlockTransfer>(entity);
                    //}

                }).ScheduleParallel();

            commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
