# Simulation Workflow Documentation

## Table of Contents
- [Introduction](#introduction)
- [LogiSim Singleton Overview](#logisim-singleton-overview)
    - [Features](#features)
- [ECS Components](#ecs-components)
    - [IBufferElementData](#ibufferelementdata)
    - [IComponentData](#icomponentdata)
- [Systems Purpose](#systems-purpose)
- [Functional Assertions and Assumptions](#functional-assertions-and-assumptions)
- [Conclusion](#conclusion)

## Introduction
Provide a brief overview of the simulation system, including its purpose and how it fits into the larger project. Mention that it is written in C# using the Unity ECS package.

## LogiSim Singleton Overview
The LogiSim singleton is a central manager for the game's simulation logic, providing helper methods for creating and connecting machines. It's part of a Unity library and is designed to simulate machines working on items to produce other items.

### Features
1. Machine and Item Classification: The `MachineClass` and `ItemProperty` enums provide a way to categorize machines and items. This can be used for grouping and filtering in the simulation.

1. Item Property Management: The `InitializePacketTypeProperties` method loads item properties from a JSON file. The `GetPropertiesForPacketType` and `GetItemData` methods retrieve properties for a specific item type.

1. Asset Loading: The `LoadAssets` method loads `Recipe` and `Machine` assets from JSON files. The `LoadRecipesFromJson`, `LoadMachinesFromJson`, `LoadRecipesAsync`, `LoadMachinesAsync`, and `LoadMachinePrefabsAsync` methods are used to load these assets from specific paths or from the Addressables system.

1. Entity Management: The `RegisterEntity` method registers an entity to be destroyed when the game object is destroyed. The `OnDestroy` method ensures that all registered entities are destroyed when the game object is destroyed.

1. Singleton Initialization: The `Awake` method initializes the singleton instance and loads the `Recipe` and `Machine` assets. If another instance of the singleton already exists, it is destroyed.

1. Recipe Retrieval: The `GetRecipe` method retrieves a `Recipe` by name.

This singleton is a crucial part of the game's simulation logic, managing the loading, categorization, and retrieval of game assets, and the registration and destruction of entities.


## ECS Components
IBufferElementData
------------------

- `public struct Packet : IBufferElementData`
    
    The fundamental wrapper for all item movement and storage in the system. [TODO: remove Interface since Packets are not directly added to entities anymore]
    
- `public struct StorageCapacity : IBufferElementData`
    
    Defines the storage capacity of a machine for a specific item type.
    
- `public struct RecipeRequirement : IBufferElementData`
    
    A Buffer element that defines the recipe requirements for a machine.

- `public struct RecipeOutputElement : IBufferElementData`
    
    A Buffer element that defines the recipe output for a machine.

- `public struct StorageBufferElement : IBufferElementData`
    
    A buffer that holds the storage of packets for a machine.

- `public struct TransferBufferElement : IBufferElementData`
    
    A buffer that holds packets transferred to this machine from another machine.

- `public struct MachinePort : IBufferElementData`
    
    A struct that holds the information about a machine's port.

IComponentData
------------------

- `public struct RecipeData : IComponentData`
    
    A component that defines recipe-specific data for a machine.

- `public struct IsTransporter : IComponentData`
    
    A component that indicates if the entity is a transporter.

- `public struct Machine : IComponentData`
    
    A component that defines the machine class and its properties. Holds status information and quality/efficiency/level data.

- `public struct Working : IComponentData`
    
    A component that indicates the working status of a machine.

- `public struct ProcessingFinished : IComponentData`
    
    A tag component that indicates that the processing of a machine has finished.

- `public struct InputStarved : IComponentData`
    
    A component that indicates if the machine is starved for input.

- `public struct InputBlocked : IComponentData`
    
    A component that indicates if the machine's input is blocked.

- `public struct OutputBlocked : IComponentData`
    
    A component that indicates if the machine's output is blocked.

- `public struct OutputBufferFull : IComponentData`
    
    A component that indicates if the machine's output buffer is full.

- `public struct NoRecipe : IComponentData`
    
    A component that indicates if the machine has no recipe.

- `public struct InputBufferFull : IComponentData`
    
    A component that indicates if the machine's input buffer is full.

- `public struct BlockTransfer : IComponentData`
    
    A component that indicates if the machine's transfer is blocked.

- `public struct NotPowered: IComponentData`
    
    A component that indicates if the machine is not powered.

## Systems Definitions
---
### Packet Transfer
---

The System Packet Transfer is a part of the LogiSim namespace and is defined in the System\_PacketTransfer.cs file.

#### Major Function
The major function of this system is to transfer packets from one machine to another based on the outgoing connections between them. An item is only transferred if the connection type matches the packet type and the target machine has the required input and is ready to receive. This system does not handle transfers from transporters.

#### Assumptions

*   The connection type must match the packet type for a transfer to occur.
*   The target machine must have the required input and be ready to receive.
*   Transfers from transporters are not handled by this system.

#### Dependencies

This system depends on several components and systems:

*   `SystemBase`: The PacketTransferSystem class extends the SystemBase class.
*   `EndSimulationEntityCommandBufferSystem`: This system is used to create a command buffer.
*   `StorageBufferElement`, `TransferBufferElement`, `StorageCapacity`, `MachinePort`: These components are used to get buffer lookups.
*   `HelperFunctions`: This class is used to perform various operations like getting capacity data and available capacity.

---
---

### Resolve Transfers
---

The ResolveTransfersSystem is a part of the LogiSim namespace and is defined in the System_ResolveTransfers.cs file. It is an ECS (Entity Component System) system in Unity that is responsible for resolving the transfers of packets between machines.

#### Major Function

The major function of this system is to move packets from the transfer buffer to the storage buffer of each machine. This is done by iterating over all entities with a Machine and Connections component, and for each entity, iterating over all packets in the transfer buffer. Each packet in the transfer buffer is then added to the storage buffer and the transfer buffer is cleared.

#### Assumptions

*   The system assumes that all entities it operates on have a Machine and Connections component.
*   The system assumes that the transfer buffer contains packets that are ready to be moved to the storage buffer.

#### Dependencies

This system depends on several components and systems:

- `SystemBase`: The ResolveTransfersSystem class extends the SystemBase class.
- `EndSimulationEntityCommandBufferSystem`: This system is used to create a command buffer.
- `StorageBufferElement`, `TransferBufferElement`: These components are used to get buffer lookups.
- `Unity.Entities`, `Unity.Collections`: These namespaces are used for ECS and native collections functionality.

---
---
### Aggregate Storage
---
The AggregateStorageSystem is a part of the LogiSim namespace and is defined in the System_AggregateStorage.cs file. It is an ECS (Entity Component System) system in Unity that is responsible for aggregating packets in the storage buffer of a machine.

#### Major Function
The major function of this system is to aggregate packets in the storage buffer of a machine. The system does not want hundreds of single packets in the buffer, so it aggregates them. It also modifies the system to create a new storageCapacityBuffer if the item type and properties are not in the buffer.

#### Assumptions
*   The system assumes that all entities it operates on have a Machine and Connections component.
*   The system assumes that the storage buffer contains packets that are ready to be aggregated.

#### Dependencies
This system depends on several components and systems:
*   `SystemBase`: The AggregateStorageSystem class extends the SystemBase class.
*   `StorageBufferElement`, `TransferBufferElement`: These components are used to get buffer lookups.
*   `Unity.Entities`, `Unity.Collections`: These namespaces are used for ECS and native collections functionality.

---
---

### Start Processing

---

The StartProcessingSystem is a part of the LogiSim namespace and is defined in the System_StartProcessing.cs file. It is an ECS (Entity Component System) system in Unity that checks if a machine has the required input packets to start processing.

#### Major Function
The major function of this system is to check if a machine has the required input packets to start processing. If it does, it subtracts the required quantities from the storage buffer and sets the Processing flag to true. This is done by iterating over all entities with a Machine component and without a ProcessingFinished component.

#### Assumptions
*   The system assumes that all entities it operates on have a Machine component.
*   The system assumes that the storage buffer contains packets that are ready to be processed.

#### Dependencies
This system depends on several components and systems:
*   `SystemBase`: The StartProcessingSystem class extends the SystemBase class.
*   `EndSimulationEntityCommandBufferSystem`: This system is used to create a command buffer.
*   `RecipeInputElement`, `StorageBufferElement`: These components are used to get buffer lookups.
*   `Unity.Entities`, `Unity.Collections`: These namespaces are used for ECS and native collections functionality.

---
---

### Process Machine
---

The ProcessMachineSystem is a part of the LogiSim namespace and is defined in the System_ProcessMachine.cs file. It is an ECS (Entity Component System) system in Unity that processes the packets in the storage buffer of a machine according to the recipe data.

#### Major Function
The major function of this system is to process the packets in the storage buffer of a machine according to the recipe data. It is also responsible for updating the ProcessTimer. For regular machines, it is a simple timer, for transporters, it is a distance-based timer for each packet.

#### Assumptions
*   The system assumes that all entities it operates on have a Machine component and a RecipeData component.
*   The system assumes that the storage buffer contains packets that are ready to be processed.

#### Dependencies
This system depends on several components and systems:
*   `SystemBase`: The ProcessMachineSystem class extends the SystemBase class.
*   `EndSimulationEntityCommandBufferSystem`: This system is used to create a command buffer.
*   `StorageBufferElement`, `MachinePort`, `StorageCapacity`: These components are used to get buffer lookups.
*   `Unity.Entities`, `Unity.Collections`: These namespaces are used for ECS and native collections functionality.

---
---

### Process Finished

---

The ProcessFinishedSystem is a part of the LogiSim namespace and is defined in the System_ProcessFinished.cs file. It is an ECS (Entity Component System) system in Unity that generates the output of the recipe and stores it in the storage buffer of the machine.

#### Major Function
The major function of this system is to generate the output of the recipe and store it in the storage buffer of the machine. It is also responsible for updating the ProcessingFinished tag and turning off the Processing flag. This is done by iterating over all entities with a Machine component and a ProcessingFinished component.

#### Assumptions
*   The system assumes that all entities it operates on have a Machine component and a ProcessingFinished component.
*   The system assumes that the storage buffer has enough capacity to store the output packets.

#### Dependencies
This system depends on several components and systems:

SystemBase: The ProcessFinishedSystem class extends the SystemBase class.
*   `EndSimulationEntityCommandBufferSystem`: This system is used to create a command buffer.
*   `RecipeOutputElement`, `StorageBufferElement`, `StorageCapacity`: These components are used to get buffer lookups.
*   `Unity.Entities`, `Unity.Collections`: These namespaces are used for ECS and native collections functionality.