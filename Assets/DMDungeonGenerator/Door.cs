using UnityEngine;

namespace DMDungeonGenerator {
    [System.Serializable]
    public class Door {

        public Vector3 position; //parent voxel position
        public Vector3 direction; //direction the door points.  position + direction gives the local voxel the door leads into.
        public RoomData parent; //the roomdata this door belongs to, we need this to compute rotations and stuff

        public Door(Vector3 pos, Vector3 dir, RoomData parent) {
            position = pos;
            direction = dir;
            this.parent = parent;
        }
    }
}