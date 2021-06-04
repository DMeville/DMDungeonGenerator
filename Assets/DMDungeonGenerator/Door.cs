using System.Collections.Generic;
using UnityEngine;

namespace DMDungeonGenerator {
    [System.Serializable]
    public class Door {

        public Vector3 position; //parent voxel position
        public Vector3 direction; //direction the door points.  position + direction gives the local voxel the door leads into.
        public RoomData parent; //the roomdata this door belongs to, we need this to compute rotations and stuff

        public GameObject spawnedDoor;

        [HideInInspector]
        public List<DoorPairData> doorPairs = new List<DoorPairData>();

        public Door(Vector3 pos, Vector3 dir, RoomData parent) {
            position = pos;
            direction = dir;
            this.parent = parent;
        }
    }

    //stores the data between two doors, this is used for "solving" the looping rooms part
    //we need some sort of data we can "check" to see if a room will fit into this spot and close a loop
    //this data is (relative to the door we are looking from) the second doors direction and distance
    public class DoorPairData {
        //just take the global data and then rotate it before we check it, not here.
        public Door door;
        public Vector3 deltaPos;
        public int openSetIndex;


        //this is kind of just used as a broadphase
        public int VoxelDistance() {
            return (int)(Mathf.Abs(deltaPos.x) + Mathf.Abs(deltaPos.y) + Mathf.Abs(deltaPos.z) + 1);
        }

        public bool CompareDeltas(Vector3 v) {
            Vector3 a = new Vector3(v.x, v.y, v.z);   //[ 1,  3] 
            Vector3 b = new Vector3(v.z, v.y, -v.x);  //[ 3, -1]  (b -> 90 clockwise)
            Vector3 c = new Vector3(-v.x, v.y, -v.z); //[-1, -3]
            Vector3 d = new Vector3(-v.z, v.y, v.x);  //[-3 , 1]

            if(deltaPos == a || deltaPos == b || deltaPos == c || deltaPos == d) {
                return true;
            } else {
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v"></param>
        /// <returns>rotation in degrees clockwise</returns>
        public int GetMatchingDeltaRotation(Vector3 v) {
            Vector3 a = new Vector3(v.x, v.y, v.z);   //[ 1,  3] 
            Vector3 b = new Vector3(v.z, v.y, -v.x);  //[ 3, -1]  (b -> 90 clockwise)
            Vector3 c = new Vector3(-v.x, v.y, -v.z); //[-1, -3]
            Vector3 d = new Vector3(-v.z, v.y, v.x);  //[-3 , 1]

            if(deltaPos == a) return 0;
            if(deltaPos == b) return 90;
            if(deltaPos == c) return 180;
            if(deltaPos == d) return 270;
            Debug.LogError("No matching rotation...this should never happen!");
            return 0;
        }

        public Vector3 GetRotatedDelta(int rotation) {
            Vector3 v = deltaPos;
            Vector3 a = new Vector3(v.x, v.y, v.z);   //[ 1,  3] 
            Vector3 b = new Vector3(v.z, v.y, -v.x);  //[ 3, -1]  (b -> 90 clockwise)
            Vector3 c = new Vector3(-v.x, v.y, -v.z); //[-1, -3]
            Vector3 d = new Vector3(-v.z, v.y, v.x);

            if(rotation == 0) return a;
            if(rotation == 90) return b;
            if(rotation == 180) return c;
            if(rotation == 270) return d;
            Debug.LogError("GetRotatedDelta failed...this should never happen!");
            return Vector3.zero;
        }

        public void LogRotations() {
            Vector3 v = deltaPos;
            Vector3 a = new Vector3(v.x, v.y, v.z);   //[ 1,  3] 
            Vector3 b = new Vector3(v.z, v.y, -v.x);  //[ 3, -1]  (b -> 90 clockwise)
            Vector3 c = new Vector3(-v.x, v.y, -v.z); //[-1, -3]
            Vector3 d = new Vector3(-v.z, v.y, v.x);  //[-3 , 1]
            Debug.Log(a.ToString());
            Debug.Log(b.ToString());
            Debug.Log(c.ToString());
            Debug.Log(d.ToString());

        }
    }

}