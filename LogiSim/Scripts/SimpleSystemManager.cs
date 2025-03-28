using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;
using static LogiSim.LogiSim;

namespace LogiSim
{
    public class SystemManager : MonoBehaviour
    {
        private Dictionary<Entity,LogiSim.MachineInstanceData> _entities;
        private EntityManager _em;
        
        public float voxelSize = 1.0f;



        private void Awake()
        {
            _entities = new Dictionary<Entity, LogiSim.MachineInstanceData>();
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;

        }

        void Start()
        {
            LogiSim.Instance.OnInitialized += CreateSystem;
        }

        private void OnDestroy()
        {
            LogiSim.Instance.OnInitialized -= CreateSystem;
        }


        //public Entity CreateMachine(SimpleMachine machine, SimpleRecipe recipe, string prefabRef = "MachineStandIn", Vector3Int address = default)
        //{
        //    Entity newMachine = LogiSim.Instance.CreateMachine(machine, recipe);
        //    SystemEntityData data = new SystemEntityData {
        //        ConnectedEntities = new List<Entity>(),
        //        localAddress = address,
        //        yRotation = 0,
        //        prefabReference = prefabRef
        //    };

        //    // Get the prefab from the MachinePrefabsLookup dictionary
        //    GameObject prefab = LogiSim.Instance.MachinePrefabsLookup[data.prefabReference];

        //    // Instantiate the prefab and store the resulting GameObject in the data
        //    data.gameObject = GameObject.Instantiate(prefab);
        //    //parent it
        //    data.gameObject.transform.SetParent(this.transform);
        //    data.gameObject.transform.localRotation = Quaternion.Euler(0, data.yRotation, 0);
        //    data.gameObject.transform.localPosition = new Vector3(data.localAddress.x * voxelSize, data.localAddress.y * voxelSize, data.localAddress.z * voxelSize);

        //    _entities.Add(newMachine, data);

        //    return newMachine;
        //}

        //public void ConnectMachines(Entity machine1, Entity machine2, IOData output)
        //{
        //    LogiSim.Instance.ConnectMachines(machine1, machine2, output);
        //}

        //public void ConnectMachines(Entity machine1, Entity machine2, string outputname)
        //{
        //    LogiSim.Instance.ConnectMachines(machine1, machine2, outputname);
        //}

        public void ConnectMachines(ref MachineInstanceData source, ref MachineInstanceData target, string type)
        {
            LogiSim.Instance.ConnectMachines(ref source, ref target, type);
        }

        public void AddRecipe(ref MachineInstanceData machineInstanceData, SimpleRecipe recipe)
        {
            LogiSim.Instance.AddRecipe(ref machineInstanceData, recipe);
        }

        public LogiSim.MachineInstanceData CreateMachine(SimpleMachine config)
        {
            var data = LogiSim.Instance.CreateMachine(config);
            
            data.prototype = config;
            // Get the prefab from the MachinePrefabsLookup dictionary
            GameObject prefab = LogiSim.Instance.MachinePrefabsLookup[data.PrefabRef];

            // Instantiate the prefab and store the resulting GameObject in the data
            data.GameObject = GameObject.Instantiate(prefab);
            //parent it
            data.GameObject.transform.SetParent(this.transform);
            data.GameObject.transform.localRotation = Quaternion.Euler(0, data.yRotation, 0);
            data.GameObject.transform.localPosition = new Vector3(data.localAddress.x * voxelSize, data.localAddress.y * voxelSize, data.localAddress.z * voxelSize);


            _entities.Add(data.entity, data);
            return data;
        }
       

        //TODO: MODIFY THIS TO USE THE NEW CREATEMACHINE FUNCTION
        public void CreateSystem()
        {
            // Load the recipes
            SimpleMachine conduitMachine = LogiSim.Instance.GetMachine("conduit_1");
            SimpleMachine generatorMachine = LogiSim.Instance.GetMachine("generator_1");
            SimpleMachine minerMachine = LogiSim.Instance.GetMachine("miner_1");

            SimpleRecipe conduitRecipe = LogiSim.Instance.GetRecipe("conduit");
            SimpleRecipe generatorRecipe = LogiSim.Instance.GetRecipe("generator");
            SimpleRecipe minerRecipe = LogiSim.Instance.GetRecipe("miner");


            var generator = CreateMachine(generatorMachine);
            var conduit1 = CreateMachine(conduitMachine);
            var miner = CreateMachine(minerMachine);


            AddRecipe(ref generator, generatorRecipe);
            AddRecipe(ref conduit1, conduitRecipe);
            AddRecipe(ref miner, minerRecipe);

            ConnectMachines(ref generator, ref conduit1, "electricity");
            ConnectMachines(ref conduit1, ref miner, "electricity");

            //// Create the machines
            //Entity generator = CreateMachine(generatorMachine, generatorRecipe, "prefab_Generator", new Vector3Int(0,0,0));
            //Entity conduit1 = CreateMachine(conduitMachine, conduitRecipe, "prefab_ConduitBasic", new Vector3Int(2, 0, 0));
            ////Entity conduit2 = CreateMachine(conduitMachine, conduitRecipe, "prefab_ConduitBasic", new Vector3Int(3, 0, 0));
            //Entity miner = CreateMachine(minerMachine, minerRecipe, "prefab_Generator", new Vector3Int(4, 0, 0));
            ////Entity conveyor = LogiSim.Instance.CreateMachine(conveyorMachine, conveyorRecipe);


            //// Set up the connections between the machines
            //ConnectMachines(generator, conduit1, "electricity");
            ////ConnectMachines(conduit1, conduit2, conduitRecipe.outputs[0]);
            //ConnectMachines(conduit1, miner, "electricity");
        }


        //public void ConnectMachines(Entity machine1, Entity machine2, Output output)
        //{
        //    // Connect the machines here
        //}

        //public void PositionMachine(Entity machine, Vector3 position)
        //{
        //    // Position the machine here
        //}

        //public void RotateMachine(Entity machine, Quaternion rotation)
        //{
        //    // Rotate the machine here
        //}

        //public string SerializeSystem()
        //{
        //    // Serialize the system here
        //}

        //public void DeserializeSystem(string serializedSystem)
        //{
        //    // Deserialize the system here
        //}

        //public void Cleanup()
        //{
        //    // Cleanup the entities here
        //}
    }
}