using LogiSim;
using System.Collections;
using System.Collections.Generic;
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
        public List<MachinePortConfig> ValidInputs;
        public List<MachinePortConfig> ValidOutputs;
        public bool IsTransporter;
        public float OutputRefractoryTime;
        public ItemProperty PowerType; //the type of power the machine uses; should match an input port type
        public float PowerConsumption; //units per second
    }

    [System.Serializable]
    public struct MachinePortConfig
    {
        public ItemProperty PortProperty;
        public int StorageCapacity;
    }
}
//Todo: add information for the I/O positions on the machine