using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Linq;
using DMDungeonGenerator;

[RequireComponent(typeof(DungeonGenerator))]
[CustomEditor(typeof(DungeonGenerator))]
public class GeneratorEditor:Editor {


    public override void OnInspectorGUI() {

        //Generator g = (Generator)target;
        //Generator.voxelScale = EditorGUILayout.FloatField("VoxelScale: ", Generator.voxelScale);

        base.OnInspectorGUI();
    }

}
  