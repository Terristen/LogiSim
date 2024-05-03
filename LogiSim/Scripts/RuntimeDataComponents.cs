using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace LogiSim
{

    /// <summary>
    /// Provides the structure for serialization/deserialization of Item type information
    /// code is the numeric representation of the item used to identify it in the ECS system
    /// </summary>
    [System.Serializable]
    public struct ItemDictionaryEntry
    {
        public string name;
        public int code;
        public ItemProperty properties;
        public string icon;
        public string prefab;
        public string description;
    }

    /// <summary>
    /// FOR REVIEW: holds game-world information about a machine including its position and rotation in the world.
    /// </summary>
    public struct SystemEntityData
    {
        public Vector3Int localAddress;
        public float yRotation;
        public GameObject gameObject;
        public string prefabReference; //to decide whether or not to use addressables for the prefab or just use the prefab reference

        public List<Entity> ConnectedEntities;

    }

    

    /// <summary>
    /// A class that holds the Input or Ouput data for a Recipe
    /// </summary>
    [System.Serializable]
    public class IOData
    {
        public ItemProperty ItemProperties;
        public string type;
        public int quantity;
    }

    /// <summary>
    /// Makes packets-types usable as a key in a NativeHashMap.
    /// </summary>
    public struct PacketKey : IEquatable<PacketKey>
    {
        public int Type;
        public ItemProperty Properties;

        public PacketKey(int type, ItemProperty properties)
        {
            Type = type;
            Properties = properties;
        }

        public bool Equals(PacketKey other)
        {
            return Type == other.Type && Properties == other.Properties;
        }

        public override int GetHashCode()
        {
            // Use XOR to combine the hash codes of the Type and Properties
            return ((int)Type) ^ ((int)Properties);
        }
    }

    [BurstCompile]
    public struct SimpleGuid : IEquatable<SimpleGuid>
    {
        public long Part1;
        public long Part2;

        public static SimpleGuid Create()
        {
            var guid = Guid.NewGuid();
            byte[] bytes = guid.ToByteArray();
            return new SimpleGuid
            {
                Part1 = BitConverter.ToInt64(bytes, 0),
                Part2 = BitConverter.ToInt64(bytes, 8)
            };
        }

        public static SimpleGuid Create(Guid guid)
        {
            if (guid == Guid.Empty)
            {
                guid = Guid.NewGuid();
            }

            byte[] bytes = guid.ToByteArray();
            return new SimpleGuid
            {
                Part1 = BitConverter.ToInt64(bytes, 0),
                Part2 = BitConverter.ToInt64(bytes, 8)
            };
        }

        public bool Equals(SimpleGuid other)
        {
            return Part1 == other.Part1 && Part2 == other.Part2;
        }

        public override bool Equals(object obj)
        {
            if (obj is SimpleGuid other)
            {
                return Equals(other);
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash = hash * 23 + Part1.GetHashCode();
                hash = hash * 23 + Part2.GetHashCode();
                return hash;
            }
        }

        public Guid ToGuid()
        {
            byte[] bytes = new byte[16];
            BitConverter.GetBytes(Part1).CopyTo(bytes, 0);
            BitConverter.GetBytes(Part2).CopyTo(bytes, 8);
            return new Guid(bytes);
        }
    }

}