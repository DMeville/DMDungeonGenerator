using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DMDungeonGenerator {
    //[System.Serializable]
    public class GraphConnection {
        public bool open = true; //can this be walked over, or is it "locked" (via a key or something)
        //public List<GraphNode> connections = new List<GraphNode>();

        public GraphNode a; //the two rooms this connection connects
        public GraphNode b; //this could be a list but we will only ever have to so....

        //data
        public Door doorRef;
    }
}
