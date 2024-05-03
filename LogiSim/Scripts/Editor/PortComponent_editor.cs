using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;



//[CustomEditor(typeof(PortComponent))]
//public class PortComponent_editor : Editor
//{
//    void OnSceneGUI()
//    {
//        Transform portTransform = ((PortComponent)target).transform;

//        if (portTransform != null)
//        {
//            // Draw an arrow representing the forward direction of the port
//            Vector3 startPosition = portTransform.position;  // This is the world position of the port
//            Vector3 endPosition = startPosition + portTransform.forward * 0.5f;  // Length of the arrow

//            // Draw line
//            Handles.color = Color.magenta;
//            Handles.DrawLine(startPosition, endPosition);

//            Handles.DrawWireCube(startPosition, new Vector3(0.2f, 0.2f, 0.2f));
//            // Draw arrow cap
//            Handles.ConeHandleCap(
//                0,
//                endPosition,
//                Quaternion.LookRotation(portTransform.forward),
//                0.1f, // Size of the cap, adjust as needed
//                EventType.Repaint
//            );
//        }

//    }
//}