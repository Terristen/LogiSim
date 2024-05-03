using LogiSim;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Entities;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace LogiSim
{
    public class SimpleLogiSim_Manager : MonoBehaviour
    {
        EntityManager entityManager;


        private void Awake()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        }
        // Start is called before the first frame update
        void Start()
        {
            LogiSim.Instance.OnInitialized += CreateSystem;
        }

        private void OnDestroy()
        {
            LogiSim.Instance.OnInitialized -= CreateSystem;
        }
        

        // Update is called once per frame
        void Update()
        {

        }

        /// <summary>
        /// Creates a simple system with a generator, conduit, and miner for testing purposes.
        /// </summary>
        public void CreateSystem()
        {
            // Load the recipes
            SimpleMachine conduitMachine = LogiSim.Instance.GetMachine("Conduit_1");
            SimpleMachine conveyorMachine = LogiSim.Instance.GetMachine("Conveyor_1");
            SimpleMachine generatorMachine = LogiSim.Instance.GetMachine("Generator_1");
            SimpleMachine minerMachine = LogiSim.Instance.GetMachine("Miner_1");

            SimpleRecipe conduitRecipe = LogiSim.Instance.GetRecipe("Conduit");
            SimpleRecipe conveyorRecipe = LogiSim.Instance.GetRecipe("Conveyor");
            SimpleRecipe generatorRecipe = LogiSim.Instance.GetRecipe("Generator");
            SimpleRecipe minerRecipe = LogiSim.Instance.GetRecipe("Miner");


            // Create the machines
            Entity generator = LogiSim.Instance.CreateMachine(generatorMachine, generatorRecipe);
            Entity conduit = LogiSim.Instance.CreateMachine(conduitMachine, conduitRecipe);
            Entity miner = LogiSim.Instance.CreateMachine(minerMachine, minerRecipe);
            //Entity conveyor = LogiSim.Instance.CreateMachine(conveyorMachine, conveyorRecipe);


            // Set up the connections between the machines
            LogiSim.Instance.ConnectMachines(generator, conduit, generatorRecipe.outputs[0]);
            LogiSim.Instance.ConnectMachines(conduit, miner, conduitRecipe.outputs[0]);
        }

        
    }
}