using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace LogiSim
{
    public class MachineComponent : MonoBehaviour
    {
        public List<PortComponent> inputPorts = new List<PortComponent>();
        public List<PortComponent> outputPorts = new List<PortComponent>();

        // Start is called before the first frame update
        void Awake()
        {
            // Retrieve all Port components in the children of the machine GameObject
            if (inputPorts == null)
            {
                inputPorts = new List<PortComponent>();
                outputPorts = new List<PortComponent>();
            }
            if (inputPorts.Count == 0)
            {
                PortComponent[] ports = GetComponentsInChildren<PortComponent>();
                foreach (PortComponent port in ports)
                {
                    if (port.direction == Direction.In)
                    {
                        inputPorts.Add(port);
                    }
                    else if (port.direction == Direction.Out)
                    {
                        outputPorts.Add(port);
                    }
                    else
                    {
                        Debug.LogError("Port direction not set for port " + port.portID);
                    }
                }
            }
        }

        // Update is called once per frame
        void Update()
        {

        }

        void OnDrawGizmos()
        {
            

            //// Retrieve all Port components in the children of the machine GameObject
            PortComponent[] ports = GetComponentsInChildren<PortComponent>();
            foreach (PortComponent port in ports)
            {
                DrawArrowForPort(port.transform, port.direction);
            }
            
        }

        private void DrawArrowForPort(Transform portTransform, Direction direction)
        {
            float rootDist = .25f;
            Color color = Color.white;
            Vector3 facing = portTransform.forward * 0.25f; // Adjust the length as needed
            Vector3 startAt = portTransform.position;
            if(direction == Direction.In)
            {
                //rootDist = 0.25f;
                color = Color.green;
                startAt = portTransform.position;
            }
            else if(direction == Direction.Out)
            {
                //rootDist = 0.0f;
                color = Color.blue;
                //facing = portTransform.forward * 0.25f; // Adjust the length as needed
                startAt = portTransform.position + (facing);
                //startAt = portTransform.position;
            }
            else
            {
                rootDist = 0.125f;
                color = Color.white;
            }
            Gizmos.color = color;
            //Vector3 facing = portTransform.forward * 0.25f; // Adjust the length as needed
            Gizmos.DrawRay(portTransform.position, facing);
            Gizmos.DrawSphere(startAt, 0.1f); // Draw a small sphere at the end
        }
    }
}