using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace DMDungeonGenerator {
    [CreateAssetMenu(fileName = "DungeonSet", menuName = "DMDungeonGenerator/Create Dungeon Set", order = 1)]

    public class DungeonSet:ScriptableObject {
        public new string name = "";
        public List<RoomData> spawns = new List<RoomData>(); //also bosses, doors, whatever
    }
}