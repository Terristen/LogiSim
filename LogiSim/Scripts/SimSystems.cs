using Unity.Entities;
using Unity.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.Entities.UniversalDelegates;
using Unity.VisualScripting;
using UnityEditor.MemoryProfiler;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor.MPE;
using System.Linq;


namespace LogiSim
{
    
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


        public bool IsCompatiblePort(Packet packet, StorageCapacity storageCapacity, bool debug = false)
        {
            if (debug)
            {
                Debug.Log($"IsCompatiblePort: {storageCapacity.BinType} & {packet.ItemProperties} == {storageCapacity.BinType}");
            }
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

        public bool HasEnoughRoom(Packet packet, DynamicBuffer<StorageCapacity> storageCapacityBuffer, bool debug = false)
        {
            int idx = GetCompatibleOutput(packet, storageCapacityBuffer, debug);
            if(debug)
            {
                Debug.Log($"CompatiblePortIndex: {idx}");
            }
            if (idx == -1)
            {
                return false;
            }
            var storageCapacity = storageCapacityBuffer[idx];
            if(debug)
            {
                Debug.Log($"HasEnoughRoom: {storageCapacity.CurrentQuantity} + {packet.Quantity} <= {storageCapacity.Capacity}");
            }
            return storageCapacity.CurrentQuantity + packet.Quantity <= storageCapacity.Capacity;
        }

        public int GetCompatibleOutput(Packet packet, DynamicBuffer<StorageCapacity> storageCapacityBuffer, bool debug = false)
        {
            for (int i = 0; i < storageCapacityBuffer.Length; i++)
            {
                if (debug)
                {
                    Debug.Log($"GetCompatibleOutput: {IsCompatiblePort(packet, storageCapacityBuffer[i], debug)} && {HasEnoughRoom(packet, storageCapacityBuffer[i],debug)}");
                }
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

        public float GetCapacityAvailable(Packet packet, DynamicBuffer<StorageCapacity> storageCapacityBuffer)
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

        public bool MatchesRequirement(Packet packet, Packet requirement, bool debug = false)
        { if (debug)
            {
                Debug.Log($"MatchesRequirement: {requirement.Type} == 0 && ({requirement.ItemProperties} & {packet.ItemProperties}) == {requirement.ItemProperties} || {requirement.Type} != 0 && {requirement.Type} == {packet.Type} && {packet.Quantity} >= {requirement.Quantity}");
            }
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

        public bool HasPort(Entity target, SimpleGuid toPortID, DynamicBuffer<MachinePort> targetPortBuffer)
        {
            bool found = false;
            for (int t = 0; t < targetPortBuffer.Length; t++)
            {
                if (targetPortBuffer[t].PortID.Equals(toPortID))
                {
                    return true;
                }
            }
            return found;
        }

        //public MachinePort GetTargetPort(DynamicBuffer<MachinePort> machinePortBuffer, SimpleGuid toPortID)
        //{
        //    MachinePort targetPort = default;

        //    for (int t = 0; t < machinePortBuffer.Length; t++)
        //    {
        //        if (machinePortBuffer[t].PortID.Equals(toPortID))
        //        {
        //            targetPort = machinePortBuffer[t];
        //            return targetPort;
        //        }
        //    }

        //    return new MachinePort { StorageCapacity = -1 };
        //}

        public RecipeOutputElement GetPortRecipe(DynamicBuffer<RecipeOutputElement> recipeBuffer, MachinePort fromPort)
        {
            foreach (var recipe in recipeBuffer)
            {
                if (MatchesRequirement(recipe.Packet.ItemProperties,fromPort.PortProperty))
                {
                    return recipe;
                }
            }

            return default;
        }

        public RecipeOutputElement GetPortRecipe(DynamicBuffer<RecipeOutputElement> recipeBuffer, ItemProperty requirement)
        {
            foreach (var recipe in recipeBuffer)
            {
                if (MatchesRequirement(recipe.Packet.ItemProperties, requirement))
                {
                    return recipe;
                }
            }

            return default;
        }

        //public MachinePort GetLocalPort(DynamicBuffer<MachinePort> portsBuffer, Connection connection)
        //{
        //    foreach (var port in portsBuffer)
        //    {
        //        if (port.PortID.Equals(connection.FromPortID))
        //        {
        //            return port;
        //        }
        //    }

        //    return default;
        //}



        public bool AreEqualPorts(MachinePort port1, MachinePort port2)
        {
            return port1.PortProperty == port2.PortProperty &&
                   port1.PortID.Equals(port2.PortID) &&
                   port1.PortDirection == port2.PortDirection;
        }

        public bool AreCompatiblePorts(MachinePort port1, MachinePort port2)
        {
            return port1.PortProperty == port2.PortProperty && //same configuration
                   !port1.PortID.Equals(port2.PortID) && //prevent self-analysis
                   port1.PortDirection != port2.PortDirection; //prevent same-direction connections
        }

        public void SortPacketsByElapsedTimeDesc(ref DynamicBuffer<StorageBufferElement> buffer)
        {
            for (int i = 1; i < buffer.Length; i++)
            {
                StorageBufferElement key = buffer[i];
                int j = i - 1;

                // Move elements of buffer[0..i-1], that are greater than key, to one position ahead of their current position
                while (j >= 0 && buffer[j].Packet.ElapsedTime < key.Packet.ElapsedTime)
                {
                    buffer[j + 1] = buffer[j];
                    j = j - 1;
                }
                buffer[j + 1] = key;
            }
        }
    }
}