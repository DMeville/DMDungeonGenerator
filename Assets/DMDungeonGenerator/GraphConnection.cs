using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DMDungeonGenerator {
    /// <summary>
    /// Graph Connection is a "door" or connection that connects two rooms.
    /// One GraphNode CAN be null, as we can have "doors" that don't lead anywhere at the end of the generator.
    /// </summary>
    [System.Serializable]
    public class GraphConnection {
        public bool open = true; //can this be walked over, or is it "locked" (via a key or something)
        public int keyID = 0;
        //public List<GraphNode> connections = new List<GraphNode>();

        [System.NonSerialized]
        public GraphNode a; //the two rooms this connection connects
        [System.NonSerialized]
        public GraphNode b; //this could be a list but we will only ever have two so....

        //data
        public Door doorRef;
    }
}
