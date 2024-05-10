using LogiSim;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;


namespace LogiSim
{
    /// <summary>
    /// Defines a Machine asset that can be used in the game.
    /// </summary>
    //[CreateAssetMenu(fileName = "NewMachine", menuName = "LogiSim/Machine", order = 2)]
    [System.Serializable]
    public class SimpleMachine
    {
        public string name = "new machine";
        public string prefabRef = "default";
        public MachineClass MachineClass;
        public float Efficiency;
        public int Level;
        public float Quality;
        public List<MachinePortConfig> Ports;
        public List<MachineCapacity> Capacities;
        public bool IsTransporter;
        public ItemProperty PowerType; //the type of power the machine uses; should match an input port type
        public float PowerConsumption; //units per second
        public float PowerStorage; //units
    }

    [System.Serializable]
    public struct MachinePortConfig
    {
        public int PortID;
        public ItemProperty PortProperty;
        public float RefractoryTime;
        public Direction PortDirection;
    }

    [System.Serializable]
    public struct MachineCapacity
    {
        public ItemProperty CapacityType;
        public float Capacity;
    }
}
//Todo: add information for the I/O positions on the machine