using System.Collections.Generic;
using UnityEngine;


namespace LogiSim
{
    /// <summary>
    /// Defines a Recipe asset that can be used in the game.
    /// </summary>
    //[CreateAssetMenu(fileName = "MachineData", menuName = "LogiSim/SimpleRecipe", order = 1)]
    [System.Serializable]
    public class SimpleRecipe 
    {
        public string name = "new recipe";
        public List<IOData> inputs;
        public List<IOData> outputs;
        public float processingTime;

        /// <summary>
        /// Defines the properties of the I/O item. 
        /// For Input, if the ItemType is Any, then the ItemProperties are the defining characteristics of the requirement.
        /// For Output, the ItemProperties are added to the create Packet so that the new item has the same properties.
        /// </summary>
        
    }

}
