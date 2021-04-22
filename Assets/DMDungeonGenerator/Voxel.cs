using UnityEngine;

namespace DMDungeonGenerator {
    [System.Serializable]
    public class Voxel {
        public Vector3 position; // Voxel position, should always be a whole number as we use this to index the voxel grid
       public Voxel(Vector3 pos) {
            position = pos;
        }
    }
}