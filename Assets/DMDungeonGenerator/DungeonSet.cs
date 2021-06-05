using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace DMDungeonGenerator {
    [CreateAssetMenu(fileName = "DungeonSet", menuName = "DMDungeonGenerator/Create Dungeon Set", order = 1)]

    public class DungeonSet:ScriptableObject {
        public new string name = "";
        public string description = "Enter any text here";

        public int TargetRooms = 100;
        public int minRooms = 25; //if the generator finishes with fewer than this many rooms, it tries again.  This prevents dungeons being very very small due to super bad rng
        
        [Tooltip("Enable this if you want the dungeon to spawn 1x1x1 voxel rooms as deadends (from the deadendRooms list) when the generator is finishing and capping off rooms.  Otherwise, if this is not enabled, it will just spawn 'fake walls' as doors (from the deadenddoors List) to cap off open connections")]
        public bool useDeadendRooms = true; //if true, the generator will spawn a single voxel room to "cap off" the generation when it's complete.  
                                            //If false, it will spawn a door instead on the "open connection" of the last room that can not be passed.

        [Header("The first room to spawn will be chosen from this list")]
        public List<GameObject> spawnRooms = new List<GameObject>(); //also bosses, doors, whatever


        [Header("Generator will use these rooms randomly")]
        public List<GameObject> possibleRooms = new List<GameObject>();

        [Header("Generator will add these to room list when trying to make a loop. These will not be spawned otherwise")]
        public List<GameObject> possibleLoopRooms = new List<GameObject>();

        [Header("You must have at least one dead end room (which is only a single voxel in volume)!")]
        public List<GameObject> deadendRooms = new List<GameObject>();
        public List<GameObject> deadendDoors = new List<GameObject>();

        [Header("Objects")]
        public List<GameObject> doors = new List<GameObject>(); //graphical representation of the doors. Will be spawned post generation
    }
}