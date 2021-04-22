using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using DMUtils;

namespace DMDungeonGenerator {
    public class DungeonGenerator:MonoBehaviour { 

        [Header("Generator Options")]
        public bool randomSeedOnStart = false;

        /// <summary>
        /// The seed to be used by the generators random number generator, the same seed will always generate the same dungeon.
        /// </summary>
        public int seed = 0;
        /// <summary>
        /// The total number of rooms you want the dungeon to have.  This is a "soft" limit, target of 100 often generates 103 rooms etc
        /// </summary>
        private int targetRooms = 100;
        public bool generationComplete = false;
        private bool regenerateWithDifferentSeed = false;
        private int attempts = 0;
        /// <summary>
        /// Hook into this to do any dungeon postprocessing, like grabbing the list of all the rooms that were generated and choosing one as a boss room, etc
        /// </summary>
        public Action<DungeonGenerator> OnComplete;
        private System.Random rand;

        /// <summary>
        /// WARNING!!! voxelScale can not be changed at runtime, and changing this after you have already laid out your room geometry will break everything, as room geometry 
        /// is laid out to match the voxels, changing voxelScale literally makes the voxels bigger so that the room geometry will no longer link up properly. The generator should still *work*
        /// but visually your rooms will not connect at all!. THis really only affects rendering, as the generator uses unscaled voxels for everything except for rendering.  
        /// </summary>
        public static float voxelScale = 1f; //see note above about changing this.

        [Header("Room Prefabs")]
        public DungeonSet generatorSettings;
        //room prefabs, should be extracted into a SO so we can swap them easy
        //public GameObject spawnFirstRoom;
        //public List<GameObject> roomPrefabs = new List<GameObject>();
        //public GameObject singleVoxelRoom; //need this for the edge cases where nothing else fits.
        //---

        /// <summary>
        /// A list of all the rooms that have been generated. Use this after generation is complete to randomly choose an exit room, etc
        /// </summary>
        [HideInInspector]
        public List<GameObject> AllRooms = new List<GameObject>();


        /// <summary>
        /// The voxel grid we use for computing overlaps. Access it with a vector3 pos (must be RoundedToInt) via IsVoxelOccupied instead of directly
        /// As IsVoxelOccupied does some error checking. 
        /// </summary>
        private Dictionary<Vector3, bool> GlobalVoxelGrid = new Dictionary<Vector3, bool>();
        /// <summary>
        /// The current "open" set of doors that have not yet been processed or connected up to anything.  
        /// </summary>
        private List<Door> openSet = new List<Door>();


        [Header("Debug Generator Options")]
        public bool generateInUpdate = false;  
        public float generationTimer = 1f; //how long to wait before generating the next room when generating in the editor
        private float _generationTimer = 0f;
        
        /// <summary>
        /// Draws all the voxels occupied in the GlobalVoxelGrid via gizmos 
        /// </summary>
        public bool drawGlobalVoxels = false;


        public void Start() {
            if(randomSeedOnStart) seed = UnityEngine.Random.Range(0, 9999);
            targetRooms = generatorSettings.TargetRooms;

            StartGenerator(seed);
        }

        public void StartGenerator(int seed) {
            Debug.Log("Dungeon Generator:: Starting generation with seed [" + seed + "]");
            DMDebugTimer.Start();

            attempts = 1;
            do {
                DestroyAllGeneratedRooms();
                RunGenerator(seed);
                if(generateInUpdate) break; 
            } while(regenerateWithDifferentSeed);

        }

        public void DestroyAllGeneratedRooms() {
            while(transform.childCount > 0) {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }
        }

        public void Update() {
            if(generateInUpdate) {
                _generationTimer -= Time.deltaTime;
                if(_generationTimer <= 0f) {
                    _generationTimer = generationTimer;

                    if(regenerateWithDifferentSeed) {
                        DestroyAllGeneratedRooms();
                        RunGenerator(seed);
                    }

                    if(openSet.Count > 0) {
                        GenerateNextRoom();  //this is just isolated so we can tick this in update during testing
                    } else { 
                        //generation is done, do any dungeon specific postprocessing here
                        if(AllRooms.Count < generatorSettings.minRooms) {
                            regenerateWithDifferentSeed = true;
                            Debug.Log("Dungeon Generator:: Generation failed to meet min rooms [" + AllRooms.Count + "/" + generatorSettings.minRooms + "] ... trying again with seed++");
                            return;
                        }

                        if(!generationComplete) {
                            Debug.Log("Dungeon Generator:: Generation Complete in [" + DMDebugTimer.Lap() + "ms] and [" + attempts + "] attempts");
                            generationComplete = true;
                            if(OnComplete != null) OnComplete(this);
                        }
                    }


                }
            }


            //if(generationComplete && Input.GetKeyUp(KeyCode.Space)) {
            //    //restart?
            //    Debug.Log("Regenerating the next dungeon");
            //    seed++;
            //    generationComplete = false;
            //    //need to destroy all the rooms
            //    while(transform.childCount > 0) {
            //        DestroyImmediate(transform.GetChild(0).gameObject);
            //    }
            //    StartGenerator(seed);
            //}
        }

        //Call this to start the generator.  
        public void RunGenerator(int seed) {
            openSet = new List<Door>();
            GlobalVoxelGrid = new Dictionary<Vector3, bool>();
            AllRooms = new List<GameObject>();
            if(regenerateWithDifferentSeed) {
                seed++;
                attempts++;
                regenerateWithDifferentSeed = false;
            }

            rand = new System.Random(seed);

            int ri = rand.Next(0, generatorSettings.spawnRooms.Count); //get a random start room
            RoomData startRoomPrefab = generatorSettings.spawnRooms[ri].GetComponent<RoomData>();
            RoomData instantiatedDataStartRoom = AddRoom(startRoomPrefab, Vector3.zero, 0f);

            for(int i = 0; i < instantiatedDataStartRoom.Doors.Count; i++) {
                openSet.Add(instantiatedDataStartRoom.Doors[i]);
            }
            if(generateInUpdate) return;

            while(openSet.Count > 0) {
                GenerateNextRoom();  //this is just isolated so we can tick this in update during testing
            }

            //generation is done, do any dungeon specific postprocessing here
            if(AllRooms.Count < generatorSettings.minRooms) {
                regenerateWithDifferentSeed = true;
                Debug.Log("Dungeon Generator:: Generation failed to meet min rooms [" + AllRooms.Count + "/"+generatorSettings.minRooms +"] ... trying again with seed++");
                return;
            }

            Debug.Log("Dungeon Generator:: Generation Complete in [" + DMDebugTimer.Lap() + "ms] and [" + attempts + "] attempts");
            generationComplete = true;
            if(OnComplete != null) OnComplete(this);

        }

        public void GenerateNextRoom() {
            Door targetDoor = openSet[0]; //grab the first door in the openset to process
            openSet.RemoveAt(0);

            Vector3 targetVoxel = targetDoor.position + targetDoor.direction; //offset one voxel in door dir so we work on the unoccupied voxel the door leads to
            Vector3 targetWorldVoxPos = GetVoxelWorldPos(targetVoxel, targetDoor.parent.rotation) + targetDoor.parent.transform.position; //need this for offset
            Vector3 targetWorldDoorDir = GetVoxelWorldDir(targetDoor.direction, targetDoor.parent.rotation); //the target voxel we're going to align to

            List<GameObject> roomsToTry = new List<GameObject>(generatorSettings.possibleRooms);
            //create a copy of the "all possible rooms list" so we can pick and remove from this list as we try different rooms
            //this ensures we don't try the same room over and over, and so we know when we have exhausted all the possiblities and just have to cap it off with a 1x1x1 vox room
            roomsToTry.Shuffle(rand); //shuffle this list so we dont always try the rooms in the same order.

            if(AllRooms.Count + openSet.Count > targetRooms) {
                roomsToTry.Clear();
                //Debug.Log("Ending Gen, Target rooms hit");
            }

            int ri = rand.Next(0, generatorSettings.deadendRooms.Count); //get a random deadend room
            roomsToTry.Add(generatorSettings.deadendRooms[ri]); //append the "singleVoxelRoom" as a last resort, this room will fit in ALL cases

            //data we will have once we find a room, used for spawning the room into the world
            RoomData newRoom = null;
            Vector3 computedRoomOffset = Vector3.zero;
            int doorIndex = 0;
            float computedRoomRotation = 0f;
            bool anyOverlaps = false;

            //start
            do {
                newRoom = roomsToTry[0].GetComponent<RoomData>();
                roomsToTry.RemoveAt(0);

                List<int> doorsToTry = new List<int>();
                for(int i = 0; i < newRoom.Doors.Count; i++) {
                    doorsToTry.Add(i);
                }
                doorsToTry.Shuffle(rand); //same thing here with the doorors as with the rooms. Copy the list so we can exaust our options, shuffle it so we never try in the same order

                do { //try all the different doors in different orientations
                    doorIndex = doorsToTry[0]; //get first doorIndex (has been shuffled)
                    doorsToTry.RemoveAt(0); 

                    Door newDoor = newRoom.Doors[doorIndex]; 
                    Vector3 targetDoorDir = targetWorldDoorDir; 
                    computedRoomRotation = GetRoomRotation(targetDoorDir, newDoor); //computes the rotation of the room so that the door we've selected to try lines up properly to the door we are wanting to connect to
                    Vector3 sDLocalWithRotation = GetVoxelWorldPos(newDoor.position, computedRoomRotation);
                    computedRoomOffset = targetWorldVoxPos - sDLocalWithRotation; //the computed offset we need to apply to the room gameobject so that the doors align

                 
                    List<Vector3> worldVoxels = new List<Vector3>(); //check for overlaps with all of these. MUST BE Mathf.RoundToInt so that the vectors are not like 0.999999999 due to precision issues
                    for(int i = 0; i < newRoom.LocalVoxels.Count; i++) { 
                        Vector3 v = GetVoxelWorldPos(newRoom.LocalVoxels[i].position, computedRoomRotation) + computedRoomOffset; //all the room voxels
                        worldVoxels.Add(v);
                    }

                    for(int i = 0; i < newRoom.Doors.Count; i++) {
                        if(i != doorIndex) { //all the door voxels (except the one we're currently working on/linking up to another room).
                            //We need to do this to so that we don't PLACE this room in a spot where the doors of this room have no space for at least a 1x1x1 room (eg, opening a door directly into a wall)
                            Vector3 v = GetVoxelWorldPos((newRoom.Doors[i].position + newRoom.Doors[i].direction), computedRoomRotation) + computedRoomOffset;
                            worldVoxels.Add(v);
                        }
                    }

                    //all room voxels addd. Get all open door voxels now.. as we don't want to block the exits to any doors not yet connected on both sides.
                    List<Vector3> doorNeighbours = new List<Vector3>();
                    for(int i = 0; i < openSet.Count; i++) {
                        Vector3 v = GetVoxelWorldPos((openSet[i].position + openSet[i].direction), openSet[i].parent.rotation) + openSet[i].parent.transform.position;
                        doorNeighbours.Add(v);
                    }

                    anyOverlaps = false;
                    for(int i = 0; i < worldVoxels.Count; i++) {
                        Vector3 iV = new Vector3(Mathf.RoundToInt(worldVoxels[i].x), Mathf.RoundToInt(worldVoxels[i].y), Mathf.RoundToInt(worldVoxels[i].z));
                        bool result = IsVoxelOccupied(iV); //check this rooms volume (including the voxels this rooms doors lead into) against all occupied voxels to check for overlaps
                        if(result) {
                            anyOverlaps = true;
                            break;
                        } else {
                            for(int j = 0; j < doorNeighbours.Count; j++) { //also check this rooms volume against all the voxels openSet.doors lead into, prevents opening a door into a wall
                                Vector3 iD = new Vector3(Mathf.RoundToInt(doorNeighbours[j].x), Mathf.RoundToInt(doorNeighbours[j].y), Mathf.RoundToInt(doorNeighbours[j].z));
                                if(iD == iV) {
                                    anyOverlaps = true;
                                    break;
                                }
                            }
                        }
                    }


                } while(doorsToTry.Count > 0 && anyOverlaps);

            } while(roomsToTry.Count > 0 && anyOverlaps);

            //Instantiate
            RoomData instantiatedNewRoom = AddRoom(newRoom, computedRoomOffset, computedRoomRotation);
            for(int i = 0; i < instantiatedNewRoom.Doors.Count; i++) {
                if(i != doorIndex) {
                    openSet.Add(instantiatedNewRoom.Doors[i]);
                }
            }

        }

      

        /// <summary>
        /// Calculates the amount of rotation the room needs so that the new room's door will align to the target door
        /// </summary>
        /// <param name="doorToConnectToDir"></param>
        /// <param name="doorToConnectWith"></param>
        /// <returns></returns>
        public float GetRoomRotation(Vector3 doorToConnectToDir, Door doorToConnectWith) {
            //doorToConnectTo is already placed in the world, the value we're using is already in worldspace
            Vector3 door2Dir = doorToConnectWith.direction;

            Vector3 a = doorToConnectToDir;
            Vector3 b = door2Dir;

            float dota = Vector3.Dot(a, GetVoxelWorldDir(b, 0f));
            float dotb = Vector3.Dot(a, GetVoxelWorldDir(b, 90f));
            float dotc = Vector3.Dot(a, GetVoxelWorldDir(b, 180f));
            float dotd = Vector3.Dot(a, GetVoxelWorldDir(b, -90f));

            //Debug.Log("All rotations: " + dota.ToString("F4") + " : " + dotb.ToString("F8") + " : " + dotc.ToString("F4") + " : " + dotd.ToString("F4"));
            //are any of these rotations 1? One should be "exactly" 
            //Debug.Log("Match: " + (dotb == 1f));
            //Debug.Log("Approx match: " + Mathf.Approximately(dotb, 1f));
            if(Mathf.Approximately(dota, -1f)) return 0f;
            if(Mathf.Approximately(dotb, -1f)) return 90f;
            if(Mathf.Approximately(dotc, -1f)) return 180f;
            if(Mathf.Approximately(dotd, -1f)) return -90f;
            Debug.LogError("Door had no matching rotations...this is impossible so something went wrong!");
            return 0f; 
        }



        /// <summary>
        /// Commits to this room, spawns it in the world, and marks the voxels in the global map as "occupied"
        /// </summary>
        /// <param name="data"></param>
        /// <param name="pos"></param>
        /// <param name="rotation"></param>
        public RoomData AddRoom(RoomData prefabData, Vector3 pos, float rotation) {
            GameObject roomObj = GameObject.Instantiate(prefabData.gameObject, pos, Quaternion.AngleAxis(rotation, Vector3.up), this.transform);
            RoomData data = roomObj.GetComponent<RoomData>();
            data.rotation = rotation; //set the instantiated roomData's rotation to be used for the voxels transformation into worldspace later
            data.UpdateInstantiatedData();

            AllRooms.Add(roomObj);
            AddGlobalVoxels(data, pos, rotation);

            return data;
        }

        public void AddGlobalVoxels(RoomData d, Vector3 offset, float rotation = 0f) {
            for(int i = 0; i < d.LocalVoxels.Count; i++) {
                Voxel v = d.LocalVoxels[i];
                Vector3 r = GetVoxelWorldPos(v.position, rotation) + offset;
                Vector3 iV = new Vector3(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), Mathf.RoundToInt(r.z));
                GlobalVoxelGrid.Add(iV , true);
                //Debug.Log("Adding voxel to global voxel list: " + r);
            }
        }


        public bool IsVoxelOccupied(Vector3 pos) {
            bool result = false;
            bool hasValue = GlobalVoxelGrid.TryGetValue(pos, out result);

            if(hasValue) {
                return true;
            } else {
                return false;
            }
        }

        public bool IsVoxelOccupied(float x, float y, float z) {
            return IsVoxelOccupied(new Vector3(x, y, z));
        }
    
        //Rotates the voxel coords so we can stamp it into the dictionary
        public Vector3 GetVoxelWorldPos(Vector3 localPos, float rotation) {
            Quaternion r = Quaternion.Euler(new Vector3(0, rotation, 0));
            Matrix4x4 m = Matrix4x4.Rotate(r);
            return m.MultiplyPoint(localPos);
        }

        public Vector3 GetVoxelWorldDir(Vector3 localDir, float rotation) {
            Quaternion r = Quaternion.Euler(new Vector3(0, rotation, 0));
            Matrix4x4 m = Matrix4x4.Rotate(r);
            return m.MultiplyVector(localDir);
        }

        private void OnDrawGizmos() {
            
            Gizmos.color = Color.blue;
            for(int i = 0; i < openSet.Count; i++) {
                Vector3 v = GetVoxelWorldPos((openSet[i].position + openSet[i].direction), openSet[i].parent.rotation) + openSet[i].parent.transform.position;
                Gizmos.DrawWireCube(v, Vector3.one);
            }

            Gizmos.color = Color.green;
            if(drawGlobalVoxels) {
                foreach(var i in GlobalVoxelGrid) {
                    Gizmos.DrawWireCube(i.Key, Vector3.one);
                }
            }
        }
    }
}
