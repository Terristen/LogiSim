# LogiSim: WarPass Test Project 

## Table of Contents
- [LogiSim: WarPass Test Project](#logisim-warpass-test-project)
  - [Table of Contents](#table-of-contents)
  - [Introduction](#introduction)
  - [Getting Started](#getting-started)
  - [Project Structure](#project-structure)
  - [Key Features](#key-features)
  - [Database Schema](#database-schema)
  - [API Endpoints](#api-endpoints)
  - [Testing](#testing)
  - [Deployment](#deployment)
  - [Contributing](#contributing)
  - [License](#license)

## Introduction
LogiSim is a logistics simulation game engine capable of modeling and simulating tens of thousands of complicated multi-step processes. Designed to be lightweight and fast, it utilizes Unity ECS to calculate the simulation using bare-metal (Burst Compiler) code, and runs all its calculations in parallel-processes for maximum performance.  

## Getting Started
Project is cloned into the Asset folder of your Unity project, and not the Development directory. This was done to avoid excess large files and libraries being included. By default, the project ignores the Plugins folder, in order to avoid bundling any locally downloaded licensed assets into the github repo.

## Project Structure
The majority of code resides within the LogiSim directory, the primary exception being the StreamingAssets folder which holds the serialized asset information the system uses (Machines, Recipes, Items, etc.)

## Key Features
The simulation systems are located in System_* files and should be fairly easy to find the system you might be looking for. ECS Component definitions are within the SimComponents file and Unity (or runtime) classes and structures are defined in the RuntimeComponents file. The LogiSim_singleton is the main controller for the simulation, and it is recommended that you use/write an intermediary class to handle linkages between simulation and presentation.

## Database Schema
The StreamingAssets/meta/config/ folder contains the JSON files for your primary asset definitions. These are used to make modding and iteration easier during development. In-game assets are handled using the Addressables subsystem, but this can be changed if you want.

## API Endpoints
No API endpoints are available yet. These will be a major part of the Economic Simulation more than the logistics sim.

## Testing
Test what? It all just works. :wink: 

## Deployment
Hahahaha!

## Contributing
Not open to contribution.

## License
Currently closed source. All rights reserved.
