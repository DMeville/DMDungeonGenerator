using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Linq;
using DMDungeonGenerator;

[RequireComponent(typeof(RoomData))]
[CustomEditor(typeof(RoomData))]
public class RoomDataEditor: Editor {

    public static EditingMode mode = EditingMode.None;
    private bool invert = false;
    
    public float transparency = 0.2f;
    public override void OnInspectorGUI() {
        mode = (EditingMode)EditorGUILayout.EnumPopup("EditingMode: ", mode);
        EditorGUILayout.LabelField("Transparency: ");
        transparency = EditorGUILayout.Slider(transparency, 0f, 0.25f);
        base.OnInspectorGUI();
    }

    protected virtual void OnSceneGUI() {

        if(!RoomData.DrawVolumes) return;
        RoomData data = (RoomData)target;
        List<DMDungeonGenerator.Voxel> vox = data.LocalVoxels;

        float voxelScale = DMDungeonGenerator.DungeonGenerator.voxelScale;

        for(int i = 0; i < vox.Count; i++) {
            Handles.color = new Color(1f, 1f, 1f, transparency);
            Vector3 pos = vox[i].position;
            Handles.CubeHandleCap(-1, data.transform.TransformPoint((pos*voxelScale)), data.transform.rotation, voxelScale, EventType.Repaint);
        }

        for(int i = 0; i < data.Doors.Count; i++) {
            Handles.color = new Color(1f, 0f, 0f, 0.4f);
            Vector3 pos = (data.Doors[i].position + (data.Doors[i].direction * 0.5f) + (Vector3.down * 0.25f))* voxelScale;

            if(mode != EditingMode.Doors) {
                Handles.CubeHandleCap(-1, data.transform.TransformPoint(pos), data.transform.rotation, 0.5f*voxelScale, EventType.Repaint);

            } else { 
                if(Handles.Button(data.transform.TransformPoint(pos), data.transform.rotation, 0.5f*voxelScale, 0.5f * voxelScale, Handles.CubeHandleCap)) {
                    ClickedDoorHandle(i);
                }
            }
        }

        switch(mode) {
            case EditingMode.Voxels: 
                for(int i = 0; i < vox.Count; i++) { 
                    Vector3 pos = vox[i].position;
                    float handleSize = 0.4f;

                    invert = Event.current.shift;
                    float iS = 1f ; //inverted sign for direction
                    float iO = 0f; //inverted offset for arrows
                    if(invert) {
                        iS = -1f ;
                        iO = 0.5f ;
                    }

                    Handles.color = Color.blue;
                    DrawArrowButton(pos, Vector3.forward, handleSize, iO, iS);
                    DrawArrowButton(pos, Vector3.back, handleSize, iO, iS);


                    Handles.color = Color.green;
                    DrawArrowButton(pos, Vector3.up, handleSize, iO, iS);
                    DrawArrowButton(pos, Vector3.down, handleSize, iO, iS);

                    Handles.color = Color.red;
                    DrawArrowButton(pos, Vector3.left, handleSize, iO, iS);
                    DrawArrowButton(pos, Vector3.right, handleSize, iO, iS);
                
            }
            break;
            case EditingMode.Doors:
                for(int i = 0; i < vox.Count; i++) {

                    Vector3 pos = vox[i].position;
                    float handleSize = 0.4f;

                    invert = false;
                    float iS = 1f; //inverted sign for direction
                    float iO = 0f; //inverted offset for arrows
                    if(invert) {
                        iS = -1f;
                        iO = 0.5f;
                    }

                    Handles.color = Color.cyan;
                    DrawDoorButtonArrow(pos, Vector3.forward, handleSize, iO, iS);
                    DrawDoorButtonArrow(pos, Vector3.back, handleSize, iO, iS);
                    DrawDoorButtonArrow(pos, Vector3.left, handleSize, iO, iS);
                    DrawDoorButtonArrow(pos, Vector3.right, handleSize, iO, iS);
                }
                break;
            }
    }

    public bool IsVoxelEmpty(Vector3 voxelPosition) {
        RoomData obj = (RoomData)target;

        for(int i = 0; i < obj.LocalVoxels.Count; i++) {
            if(obj.LocalVoxels[i].position == voxelPosition) {
                return false;
            }
        }

        return true;
    }
    public bool IsDoorEmpty(Vector3 pos, Vector3 dir) {
        RoomData obj = (RoomData)target;

        for(int i = 0; i < obj.Doors.Count; i++) {
            if(obj.Doors[i].position == pos && obj.Doors[i].direction == dir) {
                return false;
            }
        }

        return true;
    }

    private void ClickedArrowHandle(Vector3 pos, Vector3 dir) {
        if(invert) {
            ((RoomData)target).RemoveVoxel(pos);

        } else {
            ((RoomData)target).AddVoxel(pos, dir);
        }
        EditorUtility.SetDirty(target);
    }

    private void DrawArrowButton(Vector3 pos, Vector3 dir, float handleSize, float iO, float iS) {
        float voxelScale = DMDungeonGenerator.DungeonGenerator.voxelScale;
        if(IsVoxelEmpty(pos + dir)) {
            if(Handles.Button(((RoomData)target).transform.TransformPoint(pos * voxelScale + (dir * (iO + 0.5f) * voxelScale)), ((RoomData)target).transform.rotation*Quaternion.LookRotation(dir * iS), handleSize * voxelScale, 1f * voxelScale, Handles.ArrowHandleCap)) {
                ClickedArrowHandle(pos, dir);
            }
        }

    }

    private void DrawDoorButtonArrow(Vector3 pos, Vector3 dir, float handleSize, float iO, float iS) {
        float voxelScale = DMDungeonGenerator.DungeonGenerator.voxelScale;
        if(IsVoxelEmpty(pos + dir) && IsDoorEmpty(pos, dir)) {
            if(Handles.Button(((RoomData)target).transform.TransformPoint(pos * voxelScale + (dir * (iO + 0.5f) * voxelScale)), ((RoomData)target).transform.rotation*Quaternion.LookRotation(dir * iS), handleSize * voxelScale, 1f * voxelScale, Handles.ArrowHandleCap)) {
                ClickedArrowHandleDoor(pos, dir);
            }
        }

    }

    public void ClickedArrowHandleDoor(Vector3 pos, Vector3 dir) {
        ((RoomData)target).AddDoor(pos, dir);
        EditorUtility.SetDirty(target);
    }

    public void ClickedDoorHandle(int index) {
        ((RoomData)target).RemoveDoor(index);
        EditorUtility.SetDirty(target);
    }


    public enum EditingMode {
        None = 0,
        Voxels = 1,
        Doors = 2
    }
}



