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

                RecipeLookup = new Dictionary<string, SimpleRecipe>();
                MachineLookup = new Dictionary<string, SimpleMachine>();
                MachinePrefabsLookup = new Dictionary<string, GameObject>();


                InitializePacketTypeProperties();

                OnInitialized += () =>
                {

                    //Serializer.SaveData(MachineLookup, Application.streamingAssetsPath + "/config/machines/all.json");
                    //Serializer.SaveData(RecipeLookup, Application.streamingAssetsPath + "/config/recipes/all.json");
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
            public Entity entity;
            public string Name;
            public string PrefabRef;
            public MachineClass MachineClass;
            public float Efficiency;
            public int Level;
            public float Quality;
            public bool IsTransporter;
            public float OutputRefractoryTime;
            public ItemProperty PowerType;
            public float PowerConsumption;
            public float PowerStorage;
            public SimpleRecipe CurrentRecipe;
            public GameObject GameObject;
        }

        public MachineInstanceData CreateMachine(SimpleMachine machine)
        {
            MachineInstanceData newMachine = new MachineInstanceData
            {
                MachineClass = machine.MachineClass,
                Efficiency = machine.Efficiency,
                Level = machine.Level,
                Quality = machine.Quality,
                OutputRefractoryTime = machine.OutputRefractoryTime,
                PowerType = machine.PowerType,
                PowerConsumption = machine.PowerConsumption,
                IsTransporter = machine.IsTransporter,
                Name = machine.name,
                PrefabRef = machine.prefabRef,
                PowerStorage = machine.PowerStorage
            };

            // Create the entity
            newMachine.entity = entityManager.CreateEntity();
            RegisterEntity(newMachine.entity);

            // Add the Machine component
            entityManager.AddComponentData(newMachine.entity, new Machine
            {
                ProcessTimer = 0f,
                WorkTimer = 0f,
                Processing = false,
                Disabled = false,
                MachineClass = newMachine.MachineClass,
                Efficiency = newMachine.Efficiency,
                Level = newMachine.Level,
                Quality = newMachine.Quality,
                OutputRefractory = newMachine.OutputRefractoryTime,
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

            foreach (var port in machine.ValidInputs.Concat(machine.ValidOutputs))
            {
                storageBuffer.Add(new StorageCapacity
                {
                    BinType = port.PortProperty,
                    Capacity = port.StorageCapacity,
                    CurrentQuantity = 0
                });
            }

            storageBuffer.Add(new StorageCapacity
            {
                BinType = newMachine.PowerType,
                Capacity = newMachine.PowerStorage,
                CurrentQuantity = 0
            });

            entityManager.AddBuffer<MachinePort>(newMachine.entity);
            DynamicBuffer<MachinePort> portBuffer = entityManager.GetBuffer<MachinePort>(newMachine.entity);

            for (var p = 0; p < machine.ValidInputs.Count; p++)
            {
                var port = machine.ValidInputs[p];
                var newGuid = SimpleGuid.Create(port.PortID);
                portBuffer.Add(new MachinePort
                {
                    PortProperty = port.PortProperty,
                    StorageCapacity = port.StorageCapacity,
                    PortID = newGuid,
                    PortDirection = Direction.In
                });

                port.PortID = newGuid.ToGuid();
                machine.ValidInputs[p] = port;
            }

            for (var p = 0; p < machine.ValidOutputs.Count; p++)
            {
                var port = machine.ValidOutputs[p];
                var newGuid = SimpleGuid.Create(port.PortID);
                portBuffer.Add(new MachinePort
                {
                    PortProperty = port.PortProperty,
                    StorageCapacity = port.StorageCapacity,
                    PortID = newGuid,
                    PortDirection = Direction.Out
                });

                port.PortID = newGuid.ToGuid();
                machine.ValidOutputs[p] = port;
            }

            // Add the Connections component
            entityManager.AddBuffer<ConnectionBufferElement>(newMachine.entity);

            return newMachine;
        }

        public void AddRecipe(ref MachineInstanceData machineInstanceData, SimpleRecipe recipe)
        {
            bool isValid = IsRecipeCompatibleWithMachine(recipe, GetMachine(machineInstanceData.Name));
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

            // Check if the RecipeInputElement buffer exists and clear it
            if (!entityManager.HasComponent<RecipeInputElement>(machineInstanceData.entity))
            {
                entityManager.AddBuffer<RecipeInputElement>(machineInstanceData.entity);
                
            }

            // Check if the RecipeOutputElement buffer exists and clear it
            if (!entityManager.HasComponent<RecipeOutputElement>(machineInstanceData.entity))
            {
                entityManager.AddBuffer<RecipeOutputElement>(machineInstanceData.entity);
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
            }

            machineInstanceData.CurrentRecipe = recipe;
        }

        /// <summary>
        /// Create a Machine entity with the specified Machine and Recipe assets.
        /// </summary>
        /// <param name="machine"></param>
        /// <param name="recipe"></param>
        /// <returns>Entity reference for new Entity</returns>
        public Entity CreateMachine(SimpleMachine machine, SimpleRecipe recipe)
        {
            MachineInstanceData newMachine = new MachineInstanceData();

            bool isValid = IsRecipeCompatibleWithMachine(recipe, machine);
            if (!isValid)
            {
                Debug.LogError($"Invalid recipe({recipe.name}) for this machine({machine.name}).");
                return Entity.Null;
            }
            

            // Create the entity
            Entity machineEntity = entityManager.CreateEntity();
            LogiSim.Instance.RegisterEntity(machineEntity);

            // Add the Machine component
            entityManager.AddComponentData(machineEntity, new Machine
            {
                ProcessTimer = 0f,
                WorkTimer = 0f,
                Processing = false,
                Disabled = false,
                MachineClass = machine.MachineClass,
                Efficiency = machine.Efficiency,
                Level = machine.Level,
                Quality = machine.Quality,
                OutputRefractory = machine.OutputRefractoryTime,
                PowerType = machine.PowerType,
                PowerConsumption = machine.PowerConsumption
            });

            entityManager.AddComponentData(machineEntity, new RecipeData
            {
                ProcessingTime = recipe.processingTime
                // Add more fields here if needed
            });

            if(machine.IsTransporter)
            {
                entityManager.AddComponent<IsTransporter>(machineEntity);
            }

            // Add the StorageBuffer and TransferBuffer components
            entityManager.AddBuffer<StorageBufferElement>(machineEntity);
            entityManager.AddBuffer<TransferBufferElement>(machineEntity);

            // create and load the storage capacity buffer and port buffer
            entityManager.AddBuffer<StorageCapacity>(machineEntity);
            
            DynamicBuffer<StorageCapacity> storageBuffer = entityManager.GetBuffer<StorageCapacity>(machineEntity);
            
            foreach (var port in machine.ValidInputs.Concat(machine.ValidOutputs))
            {
                storageBuffer.Add(new StorageCapacity
                {
                    BinType = port.PortProperty,
                    Capacity = port.StorageCapacity,
                    CurrentQuantity = 0
                });
            }

            entityManager.AddBuffer<MachinePort>(machineEntity);
            DynamicBuffer<MachinePort> portBuffer = entityManager.GetBuffer<MachinePort>(machineEntity);

            for(var p=0; p < machine.ValidInputs.Count; p++)
            {
                var port = machine.ValidInputs[p];
                var newGuid = SimpleGuid.Create(port.PortID);
                portBuffer.Add(new MachinePort
                {
                    PortProperty = port.PortProperty,
                    StorageCapacity = port.StorageCapacity,
                    PortID = newGuid,
                    PortDirection = Direction.In
                });

                port.PortID = newGuid.ToGuid();
                machine.ValidInputs[p] = port;
            }

            for (var p = 0; p < machine.ValidOutputs.Count; p++)
            {
                var port = machine.ValidOutputs[p];
                var newGuid = SimpleGuid.Create(port.PortID);
                portBuffer.Add(new MachinePort
                {
                    PortProperty = port.PortProperty,
                    StorageCapacity = port.StorageCapacity,
                    PortID = newGuid,
                    PortDirection = Direction.Out
                });

                port.PortID = newGuid.ToGuid();
                machine.ValidOutputs[p] = port;
            }


            // Add the Connections component
            entityManager.AddBuffer<ConnectionBufferElement>(machineEntity);

            // Add the RecipeInput and RecipeOutput components
            DynamicBuffer<RecipeInputElement> inputBuffer = entityManager.AddBuffer<RecipeInputElement>(machineEntity);
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


            DynamicBuffer<RecipeOutputElement> outputBuffer = entityManager.AddBuffer<RecipeOutputElement>(machineEntity);
            foreach (var output in recipe.outputs)
            {
                var outputdata = GetItemData(output.type);
                outputBuffer.Add(new RecipeOutputElement { Packet = new Packet { Type = outputdata.code, Quantity = output.quantity, ItemProperties = GetPropertiesForPacketType(output.type) } });
            }

            return machineEntity;
        }

        

        /// <summary>
        /// Connect two machines together with a specified Packet.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="packet"></param>
        /// 
        public void ConnectMachines(Entity source, Entity target, Packet packet)
        {
            // Get the RecipeOutput component of the source machine
            DynamicBuffer<RecipeOutputElement> sourceOutputs = entityManager.GetBuffer<RecipeOutputElement>(source);

            // Check if the source machine can output the specified packet
            bool sourceCanOutput = false;
            foreach (var output in sourceOutputs)
            {
                if (output.Packet.Type == packet.Type || output.Packet.Type == 0)
                {
                    if (packet.Type == 0)
                    {
                        if ((output.Packet.ItemProperties & packet.ItemProperties) == packet.ItemProperties)
                        {
                            sourceCanOutput = true;
                            break;
                        }
                    }
                    else
                    {
                        sourceCanOutput = true;
                        break;
                    }
                }
            }

            if (!sourceCanOutput)
            {
                Debug.LogError("Source machine cannot output the specified packet type");
                return;
            }

            // Get the RecipeInput component of the target machine
            DynamicBuffer<RecipeInputElement> targetInputs = entityManager.GetBuffer<RecipeInputElement>(target);

            // Check if the target machine can input the specified packet
            bool targetCanInput = false;
            foreach (var input in targetInputs)
            {
                //Debug.Log($"");
                if (input.Packet.Type == packet.Type || input.Packet.Type == 0)
                {
                    Debug.Log($"Type == type or 0");
                    if (packet.Type == 0)
                    {
                        Debug.Log($"Type == 0");
                        if ((input.Packet.ItemProperties & packet.ItemProperties) == packet.ItemProperties)
                        {
                            Debug.Log($"Item properties pass");
                            targetCanInput = true;
                            break;
                        }
                    }
                    else
                    {
                        Debug.Log($"{input.Packet.ItemProperties} & {packet.ItemProperties} == {input.Packet.ItemProperties & packet.ItemProperties}");
                        targetCanInput = true;
                        break;
                    }
                }
            }

            if (!targetCanInput)
            {
                Debug.LogError($"Target machine (Entity ID: {target.Index}) cannot input the specified packet type: {packet.Type}");
                return;
            }

            // Get the Connections component of the source machine
            DynamicBuffer<ConnectionBufferElement> sourceConnections = entityManager.GetBuffer<ConnectionBufferElement>(source);

            // Add a connection to the target machine
            sourceConnections.Add(new ConnectionBufferElement { connection = new Connection { Type = packet.Type, ConnectedEntity = target, ItemProperties = packet.ItemProperties } });
        }



        public void ConnectMachines(Entity source, Entity target, IOData outputDefinition)
        {
            var outputdata = GetItemData(outputDefinition.type);
            // Create a Packet from the output definition
            Packet packet = new Packet { Type = outputdata.code, ItemProperties = outputDefinition.ItemProperties };

            // Call the original ConnectMachines method with the created Packet
            ConnectMachines(source, target, packet);
        }

        public void ConnectMachines(Entity source, Entity target, SimpleGuid sourcePort, SimpleGuid targetPort)
        {
            var src_machine = entityManager.GetComponentData<Machine>(source);
            var tgt_machine = entityManager.GetComponentData<Machine>(target);

            var srcPorts = entityManager.GetBuffer<MachinePort>(source);
            var tgtPorts = entityManager.GetBuffer<MachinePort>(target);

            DynamicBuffer<ConnectionBufferElement> sourceConnections = entityManager.GetBuffer<ConnectionBufferElement>(source);
            MachinePort port_from = default;
            MachinePort port_to = default;
            bool sourcePortFound = false;
            bool targetPortFound = false;

            foreach (MachinePort outPort in srcPorts)
            {
                if (outPort.PortID.Equals(sourcePort))
                {
                    port_from = outPort;
                    sourcePortFound = true;
                    break;
                }
            }

            foreach (MachinePort inPort in tgtPorts)
            {
                if (inPort.PortID.Equals(targetPort))
                {
                    port_to = inPort;
                    targetPortFound = true;
                    break;
                }
            }

            if (!sourcePortFound || !targetPortFound)
            {
                Debug.LogError("Source or target port not found");
                return;
            }

            if (port_from.PortDirection == port_to.PortDirection )
            {
                Debug.LogError("You must connect ports of opposite directions. Out -> In");
                return;
            }

            if ((port_from.PortProperty & port_to.PortProperty) == port_to.PortProperty)
            {
                sourceConnections.Add(new ConnectionBufferElement { connection = new Connection { Type = 0, ConnectedEntity = target, ItemProperties = 0, FromPortID = sourcePort, ToPortID = targetPort, ConnectionDirection = Direction.Out } });
            }
            
        }



        public void ConnectMachines(Entity source, Entity target, string type)
        {
            ConnectMachines(source, target, ItemDictionary[type].code);
        }

        public void ConnectMachines(Entity source, Entity target, int type)
        {
            ItemProperty properties = GetPropertiesForTypeNumber(type);

            var targ_machine = entityManager.GetComponentData<Machine>(target);
            //var src_machine = entityManager.GetComponentData<Machine>(source);

            //var targInputs = entityManager.GetBuffer<RecipeInputElement>(target);
            var srcOutputs = entityManager.GetBuffer<RecipeOutputElement>(source);

            var targCapacity = entityManager.GetBuffer<StorageCapacity>(target);
            var srcCapacity = entityManager.GetBuffer<StorageCapacity>(source);
            bool pass = false;
            ItemProperty outBinProps= 0;
            foreach(var output in srcCapacity)
            {
                if((output.BinType & properties) == properties)
                {
                    foreach(var input in targCapacity)
                    {
                        if((input.BinType & properties) == properties || (targ_machine.PowerType & properties) == properties)
                        {
                            outBinProps = output.BinType;
                            pass = true;
                            break;
                        }
                    }
                }
            }

            if (pass)
            {
                ItemProperty outProps = 0;
                foreach(var recipeOut in srcOutputs)
                {
                    if(recipeOut.Packet.Type == type)
                    {
                        outProps = recipeOut.Packet.ItemProperties;
                    }
                }

                // Get the Connections component of the source machine
                DynamicBuffer<ConnectionBufferElement> sourceConnections = entityManager.GetBuffer<ConnectionBufferElement>(source);

                // Add a connection to the target machine
                sourceConnections.Add(new ConnectionBufferElement { connection = new Connection { Type = type, ConnectedEntity = target, ItemProperties = outProps } });
            }
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

        public bool IsRecipeCompatibleWithMachine(SimpleRecipe recipe, SimpleMachine machine)
        {
            // Create checklists for input and output ports
            List<bool> inputChecklist = new List<bool>(new bool[machine.ValidInputs.Count]);
            List<bool> outputChecklist = new List<bool>(new bool[machine.ValidOutputs.Count]);

            // Loop through recipe inputs and claim machine input ports
            foreach (var input in recipe.inputs)
            {
                ItemProperty inputProperties = GetPropertiesForPacketType(input.type);
                bool inputClaimed = false;
                for (int i = 0; i < machine.ValidInputs.Count; i++)
                {
                    if (!inputChecklist[i] && (machine.ValidInputs[i].PortProperty & inputProperties) == machine.ValidInputs[i].PortProperty)
                    {
                        inputChecklist[i] = true;
                        inputClaimed = true;
                        break;
                    }
                }
                if (!inputClaimed)
                {
                    return false;
                }
            }

            // Loop through recipe outputs and claim machine output ports
            foreach (var output in recipe.outputs)
            {
                ItemProperty outputProperties = GetPropertiesForPacketType(output.type);
                bool outputClaimed = false;
                for (int i = 0; i < machine.ValidOutputs.Count; i++)
                {
                    if (!outputChecklist[i] && (machine.ValidOutputs[i].PortProperty & outputProperties) == machine.ValidOutputs[i].PortProperty)
                    {
                        outputChecklist[i] = true;
                        outputClaimed = true;
                        break;
                    }
                }
                if (!outputClaimed)
                {
                    return false;
                }
            }


            return true;
        }
    }
}