using UnityEngine;
using System.Collections.Generic;
using DMDungeonGenerator;
using System;

namespace DMDungeonGenerator {
    [ExecuteInEditMode]
    public class RoomData:MonoBehaviour {

        public List<DMDungeonGenerator.Voxel> LocalVoxels = new List<Voxel>();
        public List<DMDungeonGenerator.Door> Doors = new List<DMDungeonGenerator.Door>();
        private Bounds bounds = new Bounds();
        public static bool DrawRoomBounds = false;
        public static bool DrawVolumes = true;


        [HideInInspector]
        public float rotation = 0f; //used to store the rotation value once this room is committed

        /// <summary>
        /// This runs both in editor and in playmode as per ExecuteInEditMode.  
        /// This just ensures we always have at least one voxel in the list to build off of using the editor tools
        /// </summary>
        private void Awake() {
            //In the editor, when we add this script to a room, we always want at least one voxel in the list by default.  
            //So we add that here. 
            if(LocalVoxels.Count == 0) {
                LocalVoxels.Add(new Voxel(Vector3.zero));
            }
        }

        /// <summary>
        /// Called in edit mode from the RoomDataEditor class, adds the voxel to the list by using the editor tools
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="dir"></param>
        public void AddVoxel(Vector3 pos, Vector3 dir) {
            LocalVoxels.Add(new Voxel(pos + dir));
            RecalculateBounds();
            //check all doors to make sure the
            //door voxel neighbour (the voxel the door LEADS into)
            //is not the voxel we are adding,
            //if it is, we need to remove the door
            //OR we could just not, and remove any intersecting doors manually
            //it's really easy to just click on them
        }

        /// <summary>
        /// Called in edit mode from the RoomDataEditor class.  
        /// </summary>
        /// <param name="pos"></param>
        public void RemoveVoxel(Vector3 pos) {
            for(int i = 0; i < LocalVoxels.Count; i++) {
                if(LocalVoxels[i].position == pos) {
                    LocalVoxels.RemoveAt(i);
                    //also need to check all the doors
                    //so that if any of the doors are owned by the 
                    //voxel we just removed, we need to remove that 
                    //door too

                    // OR we could just not, and remove any intersecting doors manually
                    //it's really easy to just click on them
                    RecalculateBounds();
                    return;
                }
            }
        }

        /// <summary>
        /// Called in edit mode from the RoomDataEditor class.  
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="dir"></param>
        public void AddDoor(Vector3 pos, Vector3 dir) {
            DMDungeonGenerator.Door door = new DMDungeonGenerator.Door(pos, dir, this);
            Doors.Add(door);
        }

        /// <summary>
        /// Called in edit mode from the RoomDataEditor class.  
        /// </summary>
        /// <param name="index"></param>
        public void RemoveDoor(int index) {
            Doors.RemoveAt(index);
        }

        /// <summary>
        /// Used at generation time, when we spawn in a room for real, we need to update the doors parents to use the instantiated data instead of the prefab data
        /// So this just updates those references to the correct object
        /// </summary>
        public void UpdateInstantiatedData() {
            //UnityEditor.Selection.objects = new UnityEngine.Object[] { this.gameObject };
            //Debug.Log("Updated Door parent data to " + this.gameObject.name);
            for(int i = 0; i < Doors.Count; i++) {
                Doors[i].parent = this;
            }
        }

        /// <summary>
        /// Bounds are not used for anything but a visual representation; probably is not needed tbh.
        /// </summary>
        [ContextMenu("Recalculate Bounds")]
        public void RecalculateBounds() {
            Vector3 min = new Vector3(LocalVoxels[0].position.x,
                                      LocalVoxels[0].position.y,
                                      LocalVoxels[0].position.z);

            Vector3 max = new Vector3(LocalVoxels[0].position.x,
                                      LocalVoxels[0].position.y,
                                      LocalVoxels[0].position.z);

            for(int i = 0; i < LocalVoxels.Count; i++) {
                Vector3 pos = LocalVoxels[i].position;

                if(pos.x < min.x) min.x = pos.x;
                if(pos.y < min.y) min.y = pos.y;
                if(pos.z < min.z) min.z = pos.z;

                if(pos.x > max.x) max.x = pos.x;
                if(pos.y > max.y) max.y = pos.y;
                if(pos.z > max.z) max.z = pos.z;
            }

            //Debug.Log("Voxel::RecalculateBounds() | " + min + " : " + max);
            float voxelSize = DungeonGenerator.voxelScale;
            Vector3 size = new Vector3(voxelSize, voxelSize, voxelSize);
            bounds = new Bounds(((min + max) / 2f) * voxelSize, ((max * voxelSize + size / 2f) - (min * voxelSize - size / 2f)));
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnDrawGizmos() {
            if(DrawRoomBounds) {
                Gizmos.matrix = this.transform.localToWorldMatrix;
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }

        //this doesn't seem to save changes to the value properly in editor.
        [ContextMenu("Toggle Volume Visibility")]
        public void ToggleVolumes() {
            DrawVolumes = !DrawVolumes;
        }

        [ContextMenu("Toggle Bounds Visibility")]
        public void ToggleBounds() {
            DrawRoomBounds = !DrawRoomBounds;
        }
    }
}

