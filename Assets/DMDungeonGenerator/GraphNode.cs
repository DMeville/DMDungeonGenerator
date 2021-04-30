using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DMDungeonGenerator {
    [System.Serializable]
    public class GraphNode {

        public float pathfindingWeight = 0f; //we store data here when we pathfind, then we follow lowest value during the walk.
        public int depth = 0; //how many rooms (min) do you have to walk to get here
        public bool pathfindingProcessed = false;

        public RoomData data;

        public List<GraphConnection> connections = new List<GraphConnection>();
    }
}
