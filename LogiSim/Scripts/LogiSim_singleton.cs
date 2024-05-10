using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using System;
using Unity.Entities;
using static LogiSim.SimpleRecipe;
using System.Linq;
using Unity.VisualScripting;
using System.IO;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEditor.Experimental.GraphView;


namespace LogiSim
{
    /// <summary>
    /// Defines a classification of Machine for grouping and filtering.
    /// </summary>
    [Flags]
    public enum MachineClass
    {
        None = 0,
        Logistics = 1,
        PowerGeneration = 1 << 2,
        ResourceExtractor = 1 << 3,
        ResourceRefinery = 1 << 4,
        ProductAssembler = 1 << 5
    }

    /// <summary>
    /// Defines a type of object which can be transported between machines.
    /// </summary>
    public enum PacketType
    {
        Any,
        Electricity,
        Ore,
        Gravel,
        Widget,
        Water,
        Diesel,
        Gasoline,
        Methane,
        Alcohol,
    }

    

    [Flags]
    public enum ItemProperty
    {
        None = 0,
        RawMaterial = 1 << 0,
        Solid = 1 << 1,
        Liquid = 1 << 2,
        Energy = 1 << 3,
        Product = 1 << 4,
        Coolant = 1 << 5,
        Fuel= 1 << 6,
        Waste = 1 << 7,
        Gas = 1 << 8,
        Ore = 1 << 9,
    }

    
    
    

    /// <summary>
    /// A Singleton class that manages the game's simulation logic and provides helper methods for creating and connecting machines.
    /// </summary>
    public class LogiSim : MonoBehaviour
    {
        public static LogiSim Instance { get; private set; }
        [SerializeField]
        public float SimulationSpeed = 1f;
        public bool ReserializeConfigData = false;

        public Dictionary<string, SimpleRecipe> RecipeLookup { get; private set; }
        public Dictionary<string, SimpleMachine> MachineLookup { get; private set; }
        public Dictionary<string, GameObject> MachinePrefabsLookup { get; private set; }

        public List<Entity> createdEntities = new List<Entity>();

        public Dictionary<string, ItemDictionaryEntry> ItemDictionary { get; private set; }



        public void InitializePacketTypeProperties()
        {
            ItemDictionary = Serializer.LoadData<Dictionary<string, ItemDictionaryEntry>>(Application.streamingAssetsPath + "/config/meta/items.json");
        }

        public ItemProperty GetPropertiesForPacketType(string type)
        {
            if (ItemDictionary.TryGetValue(type, out ItemDictionaryEntry itemData))
            {
                return itemData.properties;
            }
            else
            {
                throw new KeyNotFoundException($"No properties found for packet type {type}");
            }
        }

        public ItemDictionaryEntry GetItemData(string type)
        {
            if (ItemDictionary.TryGetValue(type, out ItemDictionaryEntry itemData))
            {
                return itemData;
            }
            else
            {
                throw new KeyNotFoundException($"No properties found for item type {type}");
            }
        }

        // Define the event
        public event Action OnInitialized;

        /// <summary>
        /// Load the Recipe and Machine assets from the Addressables system.
        /// </summary>
        /// <returns></returns>
        public async Task LoadAssets()
        {
            // Load the recipes and machines from JSON files
            LoadRecipesFromJson("/config/meta/recipes.json");
            await LoadMachinesFromJson("/config/meta/machines.json");

            // Once the assets are loaded, raise the event to notify the subscribers
            OnInitialized?.Invoke();
        }

        /// <summary>
        /// make sure to destroy the entities when the game object is destroyed
        /// </summary>
        private void OnDestroy()
        {
            // Destroy the entities when the game object is destroyed
            foreach (Entity entity in createdEntities)
            {
                try
                {
                    if (entityManager.Exists(entity))
                    {
                        entityManager.DestroyEntity(entity);
                    }
                }
                catch (Exception)
                {
                    //stop needless exceptions
                    //Debug.LogError(e);
                }
            }
        }

        /// <summary>
        /// Register an entity to be destroyed when the game object is destroyed
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public Entity RegisterEntity(Entity entity)
        {
            createdEntities.Add(entity);
            return entity;
        }

        EntityManager entityManager;

        /// <summary>
        /// Initialize the singleton instance and load the Recipe and Machine assets.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                Time.timeScale = SimulationSpeed;

                RecipeLookup = new Dictionary<string, SimpleRecipe>();
                MachineLookup = new Dictionary<string, SimpleMachine>();
                MachinePrefabsLookup = new Dictionary<string, GameObject>();


                InitializePacketTypeProperties();

                OnInitialized += () =>
                {
                    if(ReserializeConfigData)
                    {
                        Serializer.SaveData(MachineLookup, Application.streamingAssetsPath + "/config/meta/machines.json");
                        Serializer.SaveData(RecipeLookup, Application.streamingAssetsPath + "/config/meta/recipes.json");
                        Serializer.SaveData(ItemDictionary, Application.streamingAssetsPath + "/config/meta/items.json");
                    }

                    Debug.Log("Loaded config data for Machines and Recipes Items from " + Application.streamingAssetsPath);
                };

                LoadAssets().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        // Handle exception
                        Debug.LogError(t.Exception);
                        Debug.Log(MachinePrefabsLookup.Keys.ToSeparatedString(","));
                    }
                });

                
            }

            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        private void LoadRecipesFromJson(string path)
        {
            path = Application.streamingAssetsPath + path;
            if (File.Exists(path))
            {
                //string json = File.ReadAllText(path);
                RecipeLookup = Serializer.LoadData<Dictionary<string, SimpleRecipe>>(path);
            }
            else
            {
                Debug.LogError("Recipes JSON file not found at " + path);
            }
        }

        private async Task LoadMachinesFromJson(string path)
        {
            path = Application.streamingAssetsPath + path;
            if (File.Exists(path))
            {
                //string json = File.ReadAllText(path);
                MachineLookup = Serializer.LoadData<Dictionary<string, SimpleMachine>>(path);

                // Load the corresponding prefab for each machine
                foreach (var machine in MachineLookup.Values)
                {
                    var prefabLoadOperation = Addressables.LoadAssetAsync<GameObject>(machine.prefabRef);
                    await prefabLoadOperation.Task;

                    if (prefabLoadOperation.Status == AsyncOperationStatus.Succeeded)
                    {
                        MachinePrefabsLookup[machine.prefabRef] = prefabLoadOperation.Result;
                    }
                    else
                    {
                        Debug.LogError("Failed to load prefab for machine " + machine.name);
                    }
                }
            }
            else
            {
                Debug.LogError("Machines JSON file not found at " + path);
            }
        }

        /// <summary>
        /// Load the Recipe assets from the Addressables system.
        /// </summary>
        /// <returns></returns>
        private async Task LoadRecipesAsync()
        {
            RecipeLookup.Clear();

            var locations = await Addressables.LoadResourceLocationsAsync(new List<string> { "Recipes" }, Addressables.MergeMode.Union).Task;

            foreach (var location in locations)
            {
                var recipe = await Addressables.LoadAssetAsync<SimpleRecipe>(location).Task;
                RecipeLookup.Add(recipe.name, recipe);
            }
        }

        /// <summary>
        /// Load the Machine assets from the Addressables system.
        /// </summary>
        /// <returns></returns>
        private async Task LoadMachinesAsync()
        {
            MachineLookup.Clear();
            
            var locations = await Addressables.LoadResourceLocationsAsync(new List<string> { "Machines" },Addressables.MergeMode.Union).Task;
            
            foreach (var location in locations)
            {
                var machine = await Addressables.LoadAssetAsync<SimpleMachine>(location).Task;
                MachineLookup.Add(machine.name, machine);
            }

            
        }

        /// <summary>
        /// Load the Machine assets from the Addressables system.
        /// </summary>
        /// <returns></returns>
        private async Task LoadMachinePrefabsAsync()
        {
            MachinePrefabsLookup.Clear();

            var locations = await Addressables.LoadResourceLocationsAsync(new List<string> { "MachinePrefabs" }, Addressables.MergeMode.Union).Task;

            foreach (var location in locations)
            {
                var machine = await Addressables.LoadAssetAsync<GameObject>(location).Task;
                MachinePrefabsLookup.Add(location.PrimaryKey, machine);
                Debug.Log($"Loaded Machine Prefab: {location.PrimaryKey}");
            }


        }

        /// <summary>
        /// Get a Recipe by name. (filename without extension)
        /// </summary>
        /// <param name="recipeName"></param>
        /// <returns></returns>
        public SimpleRecipe GetRecipe(string recipeName)
        {
            if (RecipeLookup.TryGetValue(recipeName, out var recipe))
            {
                return recipe;
            }

            Debug.LogWarning($"Recipe '{recipeName}' not found.");
            return null;
        }

        /// <summary>
        /// Get a Machine by name. (filename without extension)
        /// </summary>
        /// <param name="machineName"></param>
        /// <returns></returns>
        public SimpleMachine GetMachine(string machineName)
        {
            if (MachineLookup.TryGetValue(machineName, out var machine))
            {
                return machine;
            }

            Debug.LogWarning($"Machine '{machineName}' not found.");
            return null;
        }


        public struct MachineInstanceData
        {
            public SimpleMachine prototype;
            public Entity entity;
            public string Name;
            public string PrefabRef;
            public MachineClass MachineClass;
            public float Efficiency;
            public int Level;
            public float Quality;
            public bool IsTransporter;
            public float TransporterLength;
            public float OutputRefractoryTime;
            public ItemProperty PowerType;
            public float PowerConsumption;
            public float PowerStorage;
            public SimpleRecipe CurrentRecipe;
            public GameObject GameObject;
            public List<Entity> ConnectedEntities;
            public Vector3 localAddress;
            public float yRotation;
            public string prefabReference;
            public List<MachineCapacity> capacities;
        }

        public MachineInstanceData CreateMachine(SimpleMachine machine)
        {
            MachineInstanceData newMachine = new MachineInstanceData
            {
                MachineClass = machine.MachineClass,
                Efficiency = machine.Efficiency,
                Level = machine.Level,
                Quality = machine.Quality,
                PowerType = machine.PowerType,
                PowerConsumption = machine.PowerConsumption,
                IsTransporter = machine.IsTransporter,
                Name = machine.name,
                PrefabRef = machine.prefabRef,
                PowerStorage = machine.PowerStorage,
                capacities = machine.Capacities,
            };

            // Create the entity
            newMachine.entity = entityManager.CreateEntity();
            RegisterEntity(newMachine.entity);

            // Add the Machine component
            entityManager.AddComponentData(newMachine.entity, new Machine
            {
                ProcessTimer = 0f,
                Processing = false,
                Disabled = false,
                MachineClass = newMachine.MachineClass,
                Efficiency = newMachine.Efficiency,
                Level = newMachine.Level,
                Quality = newMachine.Quality,
                PowerType = newMachine.PowerType,
                PowerConsumption = newMachine.PowerConsumption,
                PowerStorage = newMachine.PowerStorage,
            });

            

            if (machine.IsTransporter)
            {
                entityManager.AddComponent<IsTransporter>(newMachine.entity);
            }

            // Add the StorageBuffer and TransferBuffer components
            entityManager.AddBuffer<StorageBufferElement>(newMachine.entity);
            entityManager.AddBuffer<TransferBufferElement>(newMachine.entity);

            // create and load the storage capacity buffer and port buffer
            entityManager.AddBuffer<StorageCapacity>(newMachine.entity);
            DynamicBuffer<StorageCapacity> storageBuffer = entityManager.GetBuffer<StorageCapacity>(newMachine.entity);

            foreach (var cap in machine.Capacities)
            {
                storageBuffer.Add(new StorageCapacity
                {
                    BinType = cap.CapacityType,
                    Capacity = cap.Capacity,
                    CurrentQuantity = 0
                });
            }

            //.designer should add the capacity for the power storage
            //storageBuffer.Add(new StorageCapacity
            //{
            //    BinType = newMachine.PowerType,
            //    Capacity = newMachine.PowerStorage,
            //    CurrentQuantity = 0
            //});

            entityManager.AddBuffer<MachinePort>(newMachine.entity);
            DynamicBuffer<MachinePort> portBuffer = entityManager.GetBuffer<MachinePort>(newMachine.entity);

            for (var p = 0; p < machine.Ports.Count; p++)
            {
                var port = machine.Ports[p];
                
                portBuffer.Add(new MachinePort
                {
                    PortProperty = port.PortProperty,
                    PortID = port.PortID,
                    PortDirection = port.PortDirection,
                    RefractoryTime = port.RefractoryTime,
                    AssignedPacketType = -1
                });

                machine.Ports[p] = port;
            }

            //for (var p = 0; p < machine.ValidOutputs.Count; p++)
            //{
            //    var port = machine.ValidOutputs[p];
            //    portBuffer.Add(new MachinePort
            //    {
            //        PortProperty = port.PortProperty,
            //        PortID = port.PortID,
            //        PortDirection = Direction.Out,
            //        RefractoryTime = port.RefractoryTime,
            //        AssignedPacketType = -1
            //    });

            //    machine.ValidOutputs[p] = port;

            //}

            entityManager.AddBuffer<RecipeInputElement>(newMachine.entity);
            entityManager.AddBuffer<RecipeOutputElement>(newMachine.entity);

            // Add the Connections component
            //entityManager.AddBuffer<ConnectionBufferElement>(newMachine.entity);

            return newMachine;
        }

        public void AddRecipe(ref MachineInstanceData machineInstanceData, SimpleRecipe recipe)
        {
            bool isValid = IsRecipeCompatibleWithMachine(recipe, machineInstanceData.prototype);
            if (!isValid)
            {
                Debug.LogError($"Invalid recipe({recipe.name}) for this machine({machineInstanceData.Name}).");
                return;
            }

            if(!entityManager.Exists(machineInstanceData.entity))
            {
                Debug.LogError($"Entity does not exist for machine {machineInstanceData.Name}");
                return;
            }

            if (!entityManager.HasComponent<RecipeData>(machineInstanceData.entity))
            {
                // Add the RecipeData component
                entityManager.AddComponentData(machineInstanceData.entity, new RecipeData
                {
                    ProcessingTime = recipe.processingTime
                    // Add more fields here if needed
                });
            } else
            {
                entityManager.SetComponentData(machineInstanceData.entity, new RecipeData
                {
                    ProcessingTime = recipe.processingTime
                });
            }

            //if the machine is a transporter, set the length
            if(machineInstanceData.IsTransporter)
            {
                entityManager.SetComponentData(machineInstanceData.entity, new IsTransporter
                {
                    Length = (machineInstanceData.TransporterLength == 0) ? 1 : machineInstanceData.TransporterLength

                    //TODO: modify the capacity of the capacity buffer to reflect the base capacity * length
                });
            }

            // Check if the RecipeInputElement buffer exists and clear it
            if (entityManager.HasComponent<RecipeInputElement>(machineInstanceData.entity))
            {
                var recipeInputBuffer = entityManager.GetBuffer<RecipeInputElement>(machineInstanceData.entity);
                recipeInputBuffer.Clear();

            }

            // Check if the RecipeOutputElement buffer exists and clear it
            if (entityManager.HasComponent<RecipeOutputElement>(machineInstanceData.entity))
            {
                var recipeOutputBuffer = entityManager.GetBuffer<RecipeOutputElement>(machineInstanceData.entity);
                recipeOutputBuffer.Clear();
            }

            // Add the RecipeInput and RecipeOutput components
            DynamicBuffer<RecipeInputElement> inputBuffer = entityManager.AddBuffer<RecipeInputElement>(machineInstanceData.entity);
            inputBuffer.Clear();
            foreach (var input in recipe.inputs)
            {
                var inputdata = GetItemData(input.type);
                // Set the ItemProperties field
                ItemProperty properties;
                if (inputdata.code == 0)
                {
                    if (input.ItemProperties == ItemProperty.None)
                    {
                        // Scenario: input.type is PacketType.Any and input.ItemProperties is ItemProperty.None
                        properties = GetPropertiesForPacketType(input.type);
                    }
                    else
                    {
                        // Scenario: input.type is PacketType.Any and input.ItemProperties is not ItemProperty.None
                        properties = input.ItemProperties;
                    }
                }
                else
                {
                    if (input.ItemProperties == ItemProperty.None)
                    {
                        // Scenario: input.type is not PacketType.Any and input.ItemProperties is ItemProperty.None
                        properties = GetPropertiesForPacketType(input.type);
                    }
                    else
                    {
                        // Scenario: input.type is not PacketType.Any and input.ItemProperties is not ItemProperty.None
                        properties = GetPropertiesForPacketType(input.type) | input.ItemProperties;
                    }
                }

                inputBuffer.Add(new RecipeInputElement { Packet = new Packet { Type = inputdata.code, Quantity = input.quantity, ItemProperties = properties } });
            }


            DynamicBuffer<RecipeOutputElement> outputBuffer = entityManager.AddBuffer<RecipeOutputElement>(machineInstanceData.entity);
            outputBuffer.Clear();
            foreach (var output in recipe.outputs)
            {
                var outputdata = GetItemData(output.type);
                outputBuffer.Add(new RecipeOutputElement { Packet = new Packet { Type = outputdata.code, Quantity = output.quantity, ItemProperties = GetPropertiesForPacketType(output.type) } });

                //set the output port quanttity to the recipe output quantity
                var portsBuffer = entityManager.GetBuffer<MachinePort>(machineInstanceData.entity);
                for(var p=0; p < portsBuffer.Length; p++)
                {
                    var port = portsBuffer[p];
                    if(PortCanHandle(port, outputdata.properties) && port.PortDirection == Direction.Out)
                    {
                        port.RecipeQuantity = output.quantity;
                        portsBuffer[p] = port;
                        break;
                    }
                }
            }

            machineInstanceData.CurrentRecipe = recipe;
        }

        

        public bool AreEqualPorts(MachinePort port1, MachinePort port2)
        {
            return port1.PortProperty == port2.PortProperty &&
                   port1.PortID == port2.PortID &&
                   port1.PortDirection == port2.PortDirection;
        }

        public bool AreCompatiblePorts(MachinePort port1, MachinePort port2)
        {
            return port1.PortProperty == port2.PortProperty && //same configuration
                   port1.PortID != port2.PortID && //prevent self-analysis
                   port1.PortDirection != port2.PortDirection; //prevent same-direction connections
        }

        public bool PortCanHandle(MachinePort port, ItemProperty itemProperties)
        {
            return (port.PortProperty & itemProperties) == port.PortProperty;
        }


        /// <summary>
        /// This is the current method for connecting machines. It is based on the MachinePort buffer.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="type"></param>
        public void ConnectMachines(ref MachineInstanceData source, ref MachineInstanceData target, string type)
        {
            // Get the MachinePort buffers
            var sourcePortBuffer = entityManager.GetBuffer<MachinePort>(source.entity);
            var targetPortBuffer = entityManager.GetBuffer<MachinePort>(target.entity);

            // Lookup the item type code
            var itemType = ItemDictionary[type];

            
            var foundSourcePort = new MachinePort { PortID = -1 };
            int sourceId = 0;
            // Find all matching ports with AssignedPacketType of 0 and assign the item type code
            for (int i = 0; i < sourcePortBuffer.Length; i++)
            {
                if (sourcePortBuffer[i].PortDirection == Direction.Out && sourcePortBuffer[i].AssignedPacketType == -1 && PortCanHandle(sourcePortBuffer[i],itemType.properties)) //it it's output and not already assigned
                {
                    foundSourcePort = sourcePortBuffer[i];
                    sourceId = i;   
                    break;
                }
            }

            var foundTargetPort = new MachinePort { PortID = -1 };
            int targetId = 0;
            for (int i = 0; i < targetPortBuffer.Length; i++)
            {
                if (targetPortBuffer[i].PortDirection == Direction.In && targetPortBuffer[i].AssignedPacketType == -1 && PortCanHandle(targetPortBuffer[i], itemType.properties)) //it it's output and not already assigned
                {
                    foundTargetPort = targetPortBuffer[i];
                    targetId = i;
                    break;
                    
                }
            }

            if(foundSourcePort.PortID == -1 || foundTargetPort.PortID == -1)
            {
                Debug.LogError("No suitable ports found for connection");
                return;
            }

            foundSourcePort.AssignedPacketType = itemType.code;
            foundSourcePort.ToPortID = foundTargetPort.PortID;
            foundSourcePort.ConnectedEntity = target.entity;

            foundTargetPort.AssignedPacketType = itemType.code;
            foundTargetPort.ToPortID = foundSourcePort.PortID;
            foundTargetPort.ConnectedEntity = target.entity;
            

            sourcePortBuffer[sourceId] = foundSourcePort;
            targetPortBuffer[targetId] = foundTargetPort;

        }



        public ItemDictionaryEntry GetItemDataByCode(int typeNumber)
        {
            var itemData = ItemDictionary.Values.FirstOrDefault(item => item.code == typeNumber);

            return itemData;
        }

        public ItemProperty GetPropertiesForTypeNumber(int typeNumber)
        {
            ItemDictionaryEntry itemData = GetItemDataByCode(typeNumber);

            // Return the properties of the item
            return itemData.properties;
        }


        // Overriding this method since we'll be making recipes use process information instead of port configuration for compatibility
        public bool IsRecipeCompatibleWithMachine(SimpleRecipe recipe, SimpleMachine machine)
        {
            //// Create checklists for input and output ports
            //List<bool> inputChecklist = new List<bool>(new bool[machine.Ports.Count]);
            //List<bool> outputChecklist = new List<bool>(new bool[machine.Ports.Count]);

            //// Loop through recipe inputs and claim machine input ports
            //foreach (var input in recipe.inputs)
            //{
            //    ItemProperty inputProperties = (input.type == "any")? input.ItemProperties : GetPropertiesForPacketType(input.type);
            //    bool inputClaimed = false;
            //    for (int i = 0; i < machine.Ports.Count; i++)
            //    {
            //        if (!inputChecklist[i] && machine.Ports[i].PortDirection == Direction.In && (machine.Ports[i].PortProperty & inputProperties) == machine.Ports[i].PortProperty)
            //        {
            //            inputChecklist[i] = true;
            //            inputClaimed = true;
            //            break;
            //        }
            //    }
            //    if (!inputClaimed)
            //    {
            //        return false;
            //    }
            //}

            //// Loop through recipe outputs and claim machine output ports
            //foreach (var output in recipe.outputs)
            //{
            //    ItemProperty outputProperties = (output.type == "any") ? output.ItemProperties : GetPropertiesForPacketType(output.type);
            //    bool outputClaimed = false;
            //    for (int i = 0; i < machine.Ports.Count; i++)
            //    {
            //        if (!outputChecklist[i] && machine.Ports[i].PortDirection == Direction.Out && (machine.Ports[i].PortProperty & outputProperties) == machine.Ports[i].PortProperty)
            //        {
            //            outputChecklist[i] = true;
            //            outputClaimed = true;
            //            break;
            //        }
            //    }
            //    if (!outputClaimed)
            //    {
            //        return false;
            //    }
            //}


            return true;
        }
    }
}