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
    /// The fundamental wrapper for all item movement and storage in the system.
    /// </summary>
    public struct Packet : IBufferElementData
    {
        public ItemProperty ItemProperties;
        public int Type;
        public float Quantity;
        public float ElapsedTime;
    }

    /// <summary>
    /// Defines the storage capacity of a machine for a specific item type.
    /// </summary>
    public struct StorageCapacity : IBufferElementData
    {
        public ItemProperty BinType;
        public float Capacity;
        public float CurrentQuantity;
    }


    /// <summary>
    /// A Buffer element that defines the recipe requirements for a machine.
    /// </summary>
    public struct RecipeInputElement : IBufferElementData
    {
        public Packet Packet;
    }

    /// <summary>
    /// A Buffer element that defines the recipe output for a machine.
    /// </summary>
    public struct RecipeOutputElement : IBufferElementData
    {
        public Packet Packet;
    }


    /// <summary>
    /// A component that defines recipe-specific data for a machine.
    /// </summary>
    public struct RecipeData : IComponentData
    {
        public float ProcessingTime;
        // Add more fields as needed
    }

    public struct IsTransporter : IComponentData
    {
        public int Length; //meters - Items travel at a speed of recipe speed m/s
    }

    /// <summary>
    /// A component that defines the machine class and its properties. Holds status information and quality/efficiency/level data.
    /// </summary>
    public struct Machine : IComponentData
    {
        public float ProcessTimer;
        public bool Processing;
        public bool Disabled;
        public MachineClass MachineClass;
        public float Efficiency;
        public int Level;
        public float Quality;
        //public float OutputRefractory; //time in seconds before the machine can output again
        public ItemProperty PowerType;
        public float PowerConsumption;
        public float PowerStorage;
    }

    /// <summary>
    /// A buffer that holds the storage of packets for a machine.
    /// </summary>
    public struct StorageBufferElement : IBufferElementData
    {
        public Packet Packet;
    }

    /// <summary>
    /// A buffer that holds packets transferred to this machine from another machine.
    /// </summary>
    public struct TransferBufferElement : IBufferElementData
    {
        public Packet Packet;
    }

    public struct MachinePort : IBufferElementData
    {
        public ItemProperty PortProperty;
        public int PortID;
        public Direction PortDirection;
        public int AssignedPacketType;
        public float RecipeQuantity;
        public float RefractoryTime;
        public Entity ConnectedEntity;
        public int ToPortID;
        public float RefractoryTimer;
    }

    /// <summary>
    /// A buffer that holds the connections of a machine to other machines
    /// </summary>
    //public struct ConnectionBufferElement : IBufferElementData
    //{
    //    public Connection connection;
    //    public float RefractoryTimer; //time since last transfer
    //}

    /// <summary>
    /// A tag component that indicates that the processing of a machine has finished.
    /// </summary>
    public struct ProcessingFinished : IComponentData
    {
    }

    /// <summary>
    /// Defines the possible directions of a connection in relation to a machine.
    /// </summary>
    [Serializable]
    public enum Direction { In, Out }

    /// <summary>
    /// A struct that holds the information about a connection to another machine. 
    /// It's important to note that machines do not track incoming connections, so the direction may be subject to depreciation.
    /// </summary>
    //public struct Connection : IBufferElementData
    //{
    //    public ItemProperty ItemProperties;
    //    public int Type;
    //    public Entity ConnectedEntity;
    //    public Direction ConnectionDirection;
    //    public SimpleGuid FromPortID;
    //    public SimpleGuid ToPortID;
    //}

    public struct Working : IComponentData
    {
        public float PercentComplete;
    }

    public struct InputStarved : IComponentData
    {
    }

    public struct InputBlocked : IComponentData
    {
    }

    public struct OutputBlocked : IComponentData
    {
    }

    public struct OutputBufferFull : IComponentData
    {
    }

    public struct NoRecipe : IComponentData
    {
    }

    public struct InputBufferFull : IComponentData
    {
    }

    public struct BlockTransfer : IComponentData
    {
    }

    public struct NotPowered: IComponentData
    {
    }

}
