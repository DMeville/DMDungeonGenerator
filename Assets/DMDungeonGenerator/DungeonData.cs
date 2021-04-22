using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DMDungeonGenerator;

namespace DMDungeonGenerator {
    [CreateAssetMenu(fileName = "DungeonData", menuName = "DMDungeonGenerator/Create Dungeon Data", order = 1)]

    public class DungeonData : ScriptableObject {

        public List<DungeonSet> sets = new List<DungeonSet>();
    }
}