using UnityEditor;
using UnityEngine;
using LogiSim;

//namespace LogiSim.Editor
//{
//    [CustomEditor(typeof(LogisticsManager))]
//    public class LogisticsEditor : UnityEditor.Editor
//    {
//        public override void OnInspectorGUI()
//        {
//            if (EditorApplication.isPlaying)
//            {
//                base.OnInspectorGUI();

//                LogisticsManager logisticsManager = (LogisticsManager)target;

//                EditorGUILayout.Space();
//                EditorGUILayout.LabelField("Debug Information", EditorStyles.boldLabel);
//                EditorGUILayout.LabelField("Number of Recipes Loaded: " + logisticsManager.RecipeCache.Count);
//                EditorGUILayout.LabelField("Number of Recipe Entities: " + logisticsManager.RecipeLookup.Count);
//            }
//        }
//    }
//}