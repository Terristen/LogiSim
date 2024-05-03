using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace LogiSim
{
    public class PortComponent : MonoBehaviour
    {
        public Guid portID;
        public ItemProperty properties;
        public Direction direction;
    }
}
