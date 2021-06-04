using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using DMUtils;

namespace DMDungeonGenerator {
    public class DungeonGenerator:MonoBehaviour {

        [Header("Generator Options")]
        public bool generateOnStart = true;
        public bool randomSeedOnStart = false;

        /// <summary>
        /// The seed to be used by the generators random number generator, the same seed will always generate the same dungeon.
        /// </summary>
        public int randomSeed = 0;
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
        public System.Random rand;

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
        [HideInInspector]
        public List<Door> AllDoorsData = new List<Door>(); //these are in local space
        //public List<GameObject> AllDoors = new List<GameObject>();

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
        private int highestDepth = 0;
        public Gradient debugGradient = new Gradient();
        /// <summary>
        /// Draws all the voxels occupied in the GlobalVoxelGrid via gizmos 
        /// </summary>
        public bool drawGlobalVoxels = false;
        public bool drawAllDoors = false;
        public bool drawGraph = false;
        public bool drawDepthLabels = false;
        public bool colourRoomsWithDepth = false;
        public bool colourLockedDoors = false;
        public Color graphConnectionColor;
        public Color globalVoxelColor;
        public bool drawKeyLocksLabels = false;


        public List<GraphNode> DungeonGraph = new List<GraphNode>();

        public void Start() {
            generationComplete = true; //we set this to true by default so that StartGenerator knows nothing is running already, and sets it to false when it starts.  
            if(randomSeedOnStart) randomSeed = UnityEngine.Random.Range(0, 9999);
            targetRooms = generatorSettings.TargetRooms;

            if(generateOnStart) {
                StartGenerator(randomSeed);
            }
        }

        public void StartGenerator(int seed) {
            if(!generationComplete) {
                Debug.Log("Dungeon Generator:: Can not start generator as previous generator is not yet complete!");
                return;
            }
            Debug.Log("Dungeon Generator:: Starting generation with seed [" + seed + "]");
            DMDebugTimer.Start();
            generationComplete = false;

            //lets check the data first really quick, to make sure we're not missing any rooms or anything...
            //spawn rooms:
            bool hasErrors = CheckGeneratorData();
            if(hasErrors) return;


            attempts = 1;
            do {
                DestroyAllGeneratedRooms();
                RunGenerator(seed);
                if(generateInUpdate) break;
            } while(regenerateWithDifferentSeed);

            if(!generateInUpdate) {
                PostGeneration();
            }
        }



        public void Update() {
            if(generateInUpdate) {
                _generationTimer -= Time.deltaTime;


                bool hasErrors = CheckGeneratorData();
                if(hasErrors) { //break out here.  Shouldn't ever really generate in update unless debugging though tbh
                    generateInUpdate = false;
                    return;
                }

                if(_generationTimer <= 0f) {
                    _generationTimer = generationTimer;

                    if(regenerateWithDifferentSeed) {
                        DestroyAllGeneratedRooms();
                        RunGenerator(randomSeed);
                    }

                    if(openSet.Count > 0) {
                        GenerateNextRoom();  //this is just isolated so we can tick this in update during testing
                    } else {
                        //generation is done, do any dungeon specific postprocessing here
                        if(AllRooms.Count < generatorSettings.minRooms) {
                            regenerateWithDifferentSeed = true;
                            this.randomSeed++;

                            Debug.Log("Dungeon Generator:: Generation failed to meet min rooms [" + AllRooms.Count + "/" + generatorSettings.minRooms + "] ... trying again with seed++ [ " + this.randomSeed + " ]");
                            return;
                        }

                        if(!generationComplete) {
                            Debug.Log("Dungeon Generator:: Generation Complete in [" + DMDebugTimer.Lap() + "ms] and [" + attempts + "] attempts");
                            generationComplete = true;
                            PostGeneration();
                        }

                    }
                }
            }


            if(Input.GetKeyUp(KeyCode.Space)) {
                //restart?
                Debug.Log("Regenerating the next dungeon");
                randomSeed++;
                //need to destroy all the rooms
                StartGenerator(randomSeed);
            }
        }

        //Call this to start the generator.  
        public void RunGenerator(int seed) {
            openSet = new List<Door>();
            GlobalVoxelGrid = new Dictionary<Vector3, bool>();
            AllRooms = new List<GameObject>();
            AllDoorsData = new List<Door>();
            DungeonGraph = new List<GraphNode>();

            if(regenerateWithDifferentSeed) {
                seed = this.randomSeed;
                Debug.Log("Dungeon Generator:: Seed changed to [" + seed + "]");
                attempts++;
                regenerateWithDifferentSeed = false;
            }

            //assign every possible room a unique template ID, used for identifying room types later
            int templateId = 0;
            for(int i = 0; i < generatorSettings.possibleRooms.Count; i++) {
                generatorSettings.possibleRooms[i].GetComponent<RoomData>().roomTemplateID = templateId;
                templateId++;
                generatorSettings.possibleRooms[i].GetComponent<RoomData>().PrecomputeDeltas();

            }
            for(int i = 0; i < generatorSettings.spawnRooms.Count; i++) {
                generatorSettings.spawnRooms[i].GetComponent<RoomData>().roomTemplateID = templateId;
                templateId++;
            }
            for(int i = 0; i < generatorSettings.deadendRooms.Count; i++) {
                generatorSettings.deadendRooms[i].GetComponent<RoomData>().roomTemplateID = templateId;
                templateId++;
            }




            rand = new System.Random(seed);

            int ri = rand.Next(0, generatorSettings.spawnRooms.Count); //get a random start room
            RoomData startRoomPrefab = generatorSettings.spawnRooms[ri].GetComponent<RoomData>();
            RoomData instantiatedDataStartRoom = AddRoom(startRoomPrefab, Vector3.zero, 0f);

            GraphNode firstNode = new GraphNode();
            firstNode.data = instantiatedDataStartRoom;
            instantiatedDataStartRoom.node = firstNode; //cyclic yay
            DungeonGraph.Add(firstNode);


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
                this.randomSeed++;
                Debug.Log("Dungeon Generator:: Generation failed to meet min rooms [" + AllRooms.Count + "/" + generatorSettings.minRooms + "] ... trying again with seed++ [ " + this.randomSeed + " ]");
                return;
            }

            Debug.Log("Dungeon Generator:: Generation Complete in [" + DMDebugTimer.Lap() + "ms] and [" + attempts + "] attempts");
            generationComplete = true;

        }

        //Takes the target door, and creates a door pair between target door and every other door
        //This also computes their distances relative to eachother, and other info we need to figure out
        //looping rooms.  
        private List<DoorPairData> ComputeDoorPairs(Door target) {
            List<DoorPairData> dpd = new List<DoorPairData>();
            for(int i = 0; i < openSet.Count; i++) {
                Door a = target;
                Door b = openSet[i];
                if(a.parent == b.parent) continue;
                Vector3 doorAWorldVoxPos = GetVoxelWorldPos(a.position + a.direction, a.parent.rotation) + a.parent.transform.position;
                Vector3 doorBWorldVoxPos = GetVoxelWorldPos(b.position + b.direction, b.parent.rotation) + b.parent.transform.position;
                Vector3 dist = new Vector3(Mathf.Abs(doorAWorldVoxPos.x - doorBWorldVoxPos.x), Mathf.Abs(doorAWorldVoxPos.y - doorBWorldVoxPos.y), Mathf.Abs(doorAWorldVoxPos.z - doorBWorldVoxPos.z));
                float vD = dist.x + dist.y + dist.z - 1f;
                //need to find a matching doorpair that fits these critera in order to spawn it in and close it up...

                if(Mathf.RoundToInt(vD) <= 10) { //if the doors are close enough we might be able to connect to (as most rooms I think would be relatively small idk this number is straight outta thin air
                    DoorPairData dd = new DoorPairData();
                    dd.door = b;
                    dd.openSetIndex = i;
                    dd.deltaPos = doorBWorldVoxPos - doorAWorldVoxPos;
                    dpd.Add(dd);
                    //Debug.Log("New door pair!");
                    //Debug.Log("Voxel distance between doors is: " + vD);
                    //Debug.Log("LDelta: " + dd.deltaPos.ToString());
                    //Debug.Log("WDelta: " + (doorBWorldVoxPos - doorAWorldVoxPos).ToString());
                    //Debug.Log("Distance: " + dd.VoxelDistance());
                    //Debug.Log("Door directions: " + GetVoxelWorldDir(a.direction, a.parent.rotation).ToString() + " : " + GetVoxelWorldDir(b.direction, b.parent.rotation).ToString());
                    //Debug.DrawLine(doorAWorldVoxPos, doorBWorldVoxPos, Color.red);
                    Debug.DrawRay(doorAWorldVoxPos, GetVoxelWorldDir(a.direction, a.parent.rotation) * 0.5f, Color.green);
                    Debug.DrawRay(doorBWorldVoxPos, GetVoxelWorldDir(b.direction, b.parent.rotation) * 0.5f, Color.green);
                }
            }
            return dpd;
        }

        public List<RoomSpawnTemplate> ComputeLoopRooms(Door targetDoor, List<DoorPairData> dpd) {
            List<RoomSpawnTemplate> loopRooms = new List<RoomSpawnTemplate>();
            //Debug.Log("--------------------------- Door Pairs: " + dpd.Count);
            //each dpd is a pair with currentDoor and some other door, that is close ish
            //so we really should loop through each pair (although we usually only have like one valid pair to close... if any)

            Vector3 targetVoxel = targetDoor.position + targetDoor.direction; //offset one voxel in door dir so we work on the unoccupied voxel the door leads to
            Vector3 targetWorldVoxPos = GetVoxelWorldPos(targetVoxel, targetDoor.parent.rotation) + targetDoor.parent.transform.position; //need this for offset


            for(int i = 0; i < dpd.Count; i++) {
                Door a = dpd[i].door;
                Door b = targetDoor;
                Vector3 doorAWorldVoxPos = GetVoxelWorldPos(a.position + a.direction, a.parent.rotation) + a.parent.transform.position + (Vector3.up * 0.1f);
                Vector3 doorBWorldVoxPos = GetVoxelWorldPos(b.position + b.direction, b.parent.rotation) + b.parent.transform.position + (Vector3.up * 0.1f);
                Debug.DrawLine(doorAWorldVoxPos, doorBWorldVoxPos, DMDungeonGenerator.DungeonGenerator.GetKeyColor(i));
            }


            if(dpd.Count > 0) {
                DoorPairData spawnedPairData = dpd[0];
                Door b = spawnedPairData.door;
                Vector3 doorBWorldVoxPos = GetVoxelWorldPos(b.position + b.direction, b.parent.rotation) + b.parent.transform.position;
                Debug.DrawRay(doorBWorldVoxPos, Vector3.up, Color.black);
                Debug.DrawRay(targetWorldVoxPos, Vector3.up, Color.white);
                Debug.DrawLine(doorBWorldVoxPos, targetWorldVoxPos, Color.white);

                for(int i = 0; i < generatorSettings.possibleRooms.Count; i++) {
                    RoomData possibleRoom = generatorSettings.possibleRooms[i].gameObject.GetComponent<RoomData>();
                    if(possibleRoom.Doors.Count < 2) continue;
                    //Debug.Log("Checking possible room: " + possibleRoom.gameObject.name);

                    //we need to check every door pair in this possible room, to see if the computed STUFF matches the spawened door pair we already have.
                    for(int j = 0; j < possibleRoom.Doors.Count; j++) {
                        Door possibleDoor = possibleRoom.Doors[j];
                        for(int k = 0; k < possibleDoor.doorPairs.Count; k++) {
                            DoorPairData possiblePairData = possibleDoor.doorPairs[k];

                            int pairIndex = -1;
                            for(int dpi = 0; dpi < possibleRoom.Doors.Count; dpi++) {
                                if(possiblePairData.door == possibleRoom.Doors[dpi]) {
                                    pairIndex = dpi;
                                }
                            }

                            //Debug.Log("------------------------------------ Checking pair with doors: " + j + " : " + pairIndex);


                            if(possiblePairData.VoxelDistance() == spawnedPairData.VoxelDistance()) {
                                //Debug.Log("Matching voxel dist with room: " + possibleRoom.gameObject.name + " : " + spawnedPairData.VoxelDistance());
                                //check what rotation we'd need to match this...
                                if(possiblePairData.CompareDeltas(spawnedPairData.deltaPos)) {
                                    //Debug.Log("Matching Deltas: " + spawnedPairData.deltaPos.ToString() + " (unrotate) possible: " + possiblePairData.deltaPos.ToString());
                                    int neededRotation = possiblePairData.GetMatchingDeltaRotation(spawnedPairData.deltaPos);
                                    //Debug.Log("Needs rotation of: " + neededRotation);

                                    //check if doors line up with either of these rotations?
                                    Vector3 sDoorA = GetVoxelWorldDir(targetDoor.direction, targetDoor.parent.rotation);
                                    Vector3 sDoorB = GetVoxelWorldDir(spawnedPairData.door.direction, spawnedPairData.door.parent.rotation);

                                    Vector3 pDoorA = GetVoxelWorldDir(possibleDoor.direction, neededRotation);
                                    Vector3 pDoorB = GetVoxelWorldDir(possiblePairData.door.direction, neededRotation);

                                    //Debug.Log("SDoor directions: " + sDoorA.ToString() + " : " + sDoorB.ToString());
                                    // Debug.Log("pDoor directions: " + pDoorA.ToString() + " : " + pDoorB.ToString());

                                    //do these doors face eachother? (in any order?)
                                    //it says we are facing eachother...but the data doesn't seem right??
                                    bool facingEachother = false;
                                    if(sDoorA == -pDoorA && sDoorB == -pDoorB) {
                                        facingEachother = true;
                                        //Debug.Log("FacingA");
                                    } else if(sDoorA == -pDoorB && sDoorB == -pDoorA) {
                                        facingEachother = true;
                                        //Debug.Log("FacingB");
                                    }
                                    if(facingEachother) {
                                        //Debug.Log("--------- NEW LOOP ROOM PASSED: Doors facing eachother check passed: " + possibleRoom.gameObject.name + " :r " + neededRotation);
                                        int doorIndexToConnect = 0;
                                        //what door index from loop room are we using as the position/alignment target?
                                        //we know we are processing targetDoor, so we need to know which door in possibleRoom we should sync up to
                                        //then we also need to close/connect up the other two doors in the other two doorpairs
                                        //we know the targetDoors worldPosition and world direction.  

                                        //we can just test it?
                                        //Align doors A->A with rotation, and see if B->B also then land on the matching voxels?
                                        //if not Align doors A->B and see if B->A land on the matching voxels?
                                        //then we can return door index too to the loopRooms list?

                                        //we can also check the deltaPos, if they match exactly it should mean they are ordered, if they are flipped, it means we need to reorder the doorpairs?

                                        //spawnedPairData.deltaPos; //compare against
                                        int otherIndex = -1;
                                        int indexA = -1;
                                        int indexB = -1;
                                        Vector3 delta = possiblePairData.GetRotatedDelta(neededRotation);
                                        bool matchingDeltas = (delta == spawnedPairData.deltaPos);
                                        bool inverted = false;
                                        if(!matchingDeltas) {
                                            if(delta == -spawnedPairData.deltaPos) {
                                                matchingDeltas = true;
                                                inverted = true;
                                            }
                                        }

                                        //Debug.Log("Do the deltas match?: " + matchingDeltas + ": inverted? " + inverted);
                                        if(matchingDeltas) {
                                            for(int dd = 0; dd < possibleRoom.Doors.Count; dd++) {
                                                if(possibleRoom.Doors[dd] == possiblePairData.door) {
                                                    otherIndex = dd;
                                                }
                                            }

                                            if(!inverted) {
                                                indexA = j;
                                                indexB = otherIndex;
                                            } else {
                                                indexA = otherIndex;
                                                indexB = j;
                                            }

                                            //Debug.Log("Connecting doors: targetDoor -> possibleRoom.Doors[" + indexA + "] and spawnedPair -> possibleRoom.Doors[" + indexB + "]");

                                            RoomSpawnTemplate t = new RoomSpawnTemplate();
                                            t.isLoopRoom = true;
                                            t.roomToSpawn = possibleRoom.gameObject;
                                            t.neededRotation = neededRotation;
                                            t.otherSpawnedDoor = spawnedPairData.openSetIndex;
                                            t.possibleDoorAIndex = indexA;
                                            t.possibleDoorBIndex = indexB;
                                            loopRooms.Add(t);
                                        }   
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return loopRooms;

            //if(loopRooms.Count > 0) Debug.Log("We have loopRooms: " + loopRooms.Count);
            //for(int i = 0; i < loopRooms.Count; i++) {
            //    Debug.Log("Loop piece: " + loopRooms[i].Item1.gameObject.name + " with a needed rotation of: " + loopRooms[i].Item2);
            //    Debug.Log("Spawned room doors are targetDoor and openSet[" + loopRooms[i].Item3 + "] and other possRoomDoors at index: " + loopRooms[i].Item4 + " and " + loopRooms[i].Item5);
            //}
        }

        /// <summary>
        /// This takes all the rooms in loopRooms and checks them against the world voxels for overlaps.  If we find a valid room, we assign it to nextSpawn
        /// </summary>
        /// <param name="nextSpawn"></param>
        /// <param name="loopRooms"></param>
        /// <param name="targetWorldVoxPos"></param>
        /// <param name="targetWorldDoorDir"></param>
        public void CheckLoopRoomsValid(RoomSpawnTemplate nextSpawn, List<RoomSpawnTemplate> loopRooms, Vector3 targetWorldVoxPos, Vector3 targetWorldDoorDir) {
            bool anyOverlaps = false;
         
            for(int lr = 0; lr < loopRooms.Count; lr++) {
                RoomSpawnTemplate t = loopRooms[lr];
                GameObject roomPrefab = t.roomToSpawn;
                int rotationNeeded = t.neededRotation;
                //Spawned Door A = targetDoor
                int spawnedDoorBIndex = t.otherSpawnedDoor;
                int possibleDoorAIndex = t.possibleDoorAIndex;
                int possibleDoorBIndex = t.possibleDoorBIndex;

                //--
                RoomData newRoom = roomPrefab.GetComponent<RoomData>();
                Debug.Log("Door index: " + possibleDoorAIndex);
                Door newDoor = newRoom.Doors[possibleDoorAIndex];
                Vector3 targetDoorDir = targetWorldDoorDir;
                Vector3 sDLocalWithRotation = GetVoxelWorldPos(newDoor.position, rotationNeeded);
                Vector3 computedRoomOffset = targetWorldVoxPos - sDLocalWithRotation;

                //AddRoom(newRoom, computedRoomOffset, computedRoomRotation); WORKS! need to make sure it passes the overlap tests first though...!

                List<Vector3> worldVoxels = new List<Vector3>(); //check for overlaps with all of these. MUST BE Mathf.RoundToInt so that the vectors are not like 0.999999999 due to precision issues
                for(int i = 0; i < newRoom.LocalVoxels.Count; i++) {
                    Vector3 v = GetVoxelWorldPos(newRoom.LocalVoxels[i].position, rotationNeeded) + computedRoomOffset; //all the room voxels
                    worldVoxels.Add(v);
                }

                for(int i = 0; i < newRoom.Doors.Count; i++) {
                    if(i == possibleDoorAIndex || i == possibleDoorBIndex) { //only add the neighbours to the doors we DONT care about
                    } else {
                        Vector3 v = GetVoxelWorldPos((newRoom.Doors[i].position + newRoom.Doors[i].direction), rotationNeeded) + computedRoomOffset;
                        worldVoxels.Add(v);
                    }
                }

                //all room voxels addd. Get all open door voxels now.. as we don't want to block the exits to any doors not yet connected on both sides.
                List<Vector3> doorNeighbours = new List<Vector3>();
                for(int i = 0; i < openSet.Count; i++) {
                    //we also need to ignore the second spawned door neighbour here.
                    if(i != spawnedDoorBIndex) {
                        Vector3 v = GetVoxelWorldPos((openSet[i].position + openSet[i].direction), openSet[i].parent.rotation) + openSet[i].parent.transform.position;
                        doorNeighbours.Add(v);
                    }
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
                //overlap tests

                if(!anyOverlaps) {
                    nextSpawn.isLoopRoom = true;
                    nextSpawn.roomToSpawn = t.roomToSpawn;
                    nextSpawn.roomOffset = computedRoomOffset;
                    nextSpawn.neededRotation = t.neededRotation;
                    nextSpawn.otherSpawnedDoor = t.otherSpawnedDoor;
                    nextSpawn.possibleDoorAIndex = t.possibleDoorAIndex;
                    nextSpawn.possibleDoorBIndex = t.possibleDoorBIndex;
                    nextSpawn.useMe = true;
                    return; //break here because we found a room that fits, and we're going to use this
                }
            }
        }

        /// <summary>
        /// Uses the "normal" generation logic to find the next room that we want to spawn.  
        /// </summary>
        /// <param name="nextSpawn"></param>
        /// <param name="roomsToTry"></param>
        /// <param name="targetWorldVoxPos"></param>
        /// <param name="targetWorldDoorDir"></param>
        /// <returns>False if it fails to find a valid room. This means we have NO room to spawn</returns>
        public bool ComputeNextRoomToGenerate(RoomSpawnTemplate nextSpawn, List<GameObject> roomsToTry, Vector3 targetWorldVoxPos, Vector3 targetWorldDoorDir) {
            bool anyOverlaps = false;
            do {
                //it's possible that we try every room and none fit, especially if we don't have any deaded rooms that _should_ fit in any situation.
                //so if the list is empty, just break out
                if(roomsToTry.Count == 0) {
                    return false;
                }

                RoomData newRoom = roomsToTry[0].GetComponent<RoomData>();
                roomsToTry.RemoveAt(0);

                List<int> doorsToTry = new List<int>();
                for(int i = 0; i < newRoom.Doors.Count; i++) {
                    doorsToTry.Add(i);
                }
                doorsToTry.Shuffle(rand); //same thing here with the doorors as with the rooms. Copy the list so we can exaust our options, shuffle it so we never try in the same order

                do { //try all the different doors in different orientations
                    int doorIndex = doorsToTry[0]; //get first doorIndex (has been shuffled)
                    doorsToTry.RemoveAt(0);

                    Door newDoor = newRoom.Doors[doorIndex];
                    Vector3 targetDoorDir = targetWorldDoorDir;
                    float computedRoomRotation = GetRoomRotation(targetDoorDir, newDoor); //computes the rotation of the room so that the door we've selected to try lines up properly to the door we are wanting to connect to
                    Vector3 sDLocalWithRotation = GetVoxelWorldPos(newDoor.position, computedRoomRotation);
                    Vector3 computedRoomOffset = targetWorldVoxPos - sDLocalWithRotation; //the computed offset we need to apply to the room gameobject so that the doors align


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

                    if(!anyOverlaps) {
                        //we made it to the end with no overlaps. Means this is the room we want?
                        nextSpawn.isLoopRoom = false;
                        nextSpawn.roomToSpawn = newRoom.gameObject;
                        nextSpawn.roomOffset = computedRoomOffset;
                        nextSpawn.neededRotation = (int)computedRoomRotation;
                        nextSpawn.possibleDoorAIndex = doorIndex;
                        nextSpawn.useMe = true;

                    }

                } while(doorsToTry.Count > 0 && anyOverlaps);

            } while(roomsToTry.Count > 0 && anyOverlaps);

            //we now have a spawn template for either a loop room, or a normal room
            if(anyOverlaps) { //if we made it here, we either have a newRoom assigned and ready to spawn, or we have no rooms left to as they all didn't fit
                return false;
            }
            return true;
        }

        public void GenerateNextRoom() {
            Door targetDoor = openSet[0]; //grab the first door in the openset to process
            openSet.RemoveAt(0);


            Vector3 targetVoxel = targetDoor.position + targetDoor.direction; //offset one voxel in door dir so we work on the unoccupied voxel the door leads to
            Vector3 targetWorldVoxPos = GetVoxelWorldPos(targetVoxel, targetDoor.parent.rotation) + targetDoor.parent.transform.position; //need this for offset
            Vector3 targetWorldDoorDir = GetVoxelWorldDir(targetDoor.direction, targetDoor.parent.rotation); //the target voxel we're going to align to

            List<DoorPairData> dpd = ComputeDoorPairs(targetDoor);
            List<RoomSpawnTemplate> loopRooms = ComputeLoopRooms(targetDoor, dpd); //find any possible spawn possibilities that could give us looping rooms
            RoomSpawnTemplate nextSpawn = new RoomSpawnTemplate(); //this is the next room we're going to spawn. We compute this here, (which can happen in a few different ways) then spawn it

            Door doorForProcessing = new Door(targetWorldVoxPos, targetWorldDoorDir, targetDoor.parent); //why do I do this instead of using targetDoor directly...? BECAUSE targetDoor is in ROOM space, doorForProcessing is in WORLD space (pos/directions)
            Door loopDoorForProcessing = null; //the door pair that we might connect a loop up to (newly spawned room will connect two of it's doors to doorFOrProcessing and loopDoorForProcessing)
            Door loopTargetDoor = null; //this is grabbed later from openset[doorindex] 

            List<GameObject> roomsToTry = new List<GameObject>(generatorSettings.possibleRooms);
            //create a copy of the "all possible rooms list" so we can pick and remove from this list as we try different rooms
            //this ensures we don't try the same room over and over, and so we know when we have exhausted all the possiblities and just have to cap it off with a 1x1x1 vox room

            for(int i = 0; i < roomsToTry.Count; i++) { //find the room template of the room we are trying to connect to, and remove that room template from the list of possible rooms to spawn
                if(roomsToTry[i].GetComponent<RoomData>().roomTemplateID == targetDoor.parent.roomTemplateID) { //this is to make it so we are less likely to spawn the same room type twice in a row
                    roomsToTry.RemoveAt(i); break;
                }
            }
            roomsToTry.Shuffle(rand); //shuffle this list so we dont always try the rooms in the same order.

            for(int i = 0; i < generatorSettings.possibleRooms.Count; i++) { //add back the room type we removed to the end of the (now shuffled) list, so that we try every other room first and only use this room as a last choice
                if(generatorSettings.possibleRooms[i].GetComponent<RoomData>().roomTemplateID == targetDoor.parent.roomTemplateID) {
                    roomsToTry.Add(generatorSettings.possibleRooms[i]);
                }
            }

            RoomData instantiatedNewRoom = null;
            if(generatorSettings.useDeadendRooms) {
                if(AllRooms.Count + openSet.Count >= targetRooms) {
                    roomsToTry.Clear();
                    //Debug.Log("Ending Gen, Target rooms hit");
                }
            } else {
                if(AllRooms.Count >= targetRooms) {
                    roomsToTry.Clear();
                }
            }

            if(generatorSettings.useDeadendRooms) {//append this to the end of the roomsToTryList
                int ri = rand.Next(0, generatorSettings.deadendRooms.Count); //get a random deadend room
                roomsToTry.Add(generatorSettings.deadendRooms[ri]); //append the "singleVoxelRoom" as a last resort, this room will fit in ALL cases
            }

            //roomsToTry list MIGHT be empty, if so we just don't spawn a room, spawn a door and a halfempty connection
            bool makeRoomConnectionSingleSided = false; //single voxel rooms are added to the possRooms list, this toggle forces us to just close off a room with a wall "door" that can't be passed through
            if(roomsToTry.Count == 0) makeRoomConnectionSingleSided = true; //so if have no rooms to try (which would inlcude single vox) we know we just wall off.

            //check if loop fits...
            //---------------------------------------------------------------
            //data we will have once we find a room, used for spawning the room into the world

            //after we run this, we MAY have a valid spawn in nextSpawn
            CheckLoopRoomsValid(nextSpawn, loopRooms, targetWorldVoxPos, targetWorldDoorDir);
            //if(nextSpawn.useMe) Debug.Log("<color=red>We found a valid loop room to spawn!</color>");

            bool computeNextRoom = true; 
            if(nextSpawn.useMe) computeNextRoom = false; 
            if(makeRoomConnectionSingleSided) computeNextRoom = false;
            bool instantiateRoom = true;
            if(makeRoomConnectionSingleSided) instantiateRoom = false; //these are just some bools to make things easier to read the flow

            //compute the next room if we 1) Don't already have one ready to instantiate, or 2) Don't need to spawn a room because it's going to be a single sided connection (this door is going to be wallled off instead of going into another room)

            if(computeNextRoom) {//This is the "compute room as normal", only want to do if ----^
                //start normal spawning. Compute room as normal
                bool success = ComputeNextRoomToGenerate(nextSpawn, roomsToTry, targetWorldVoxPos, targetWorldDoorDir);

                //we now have a spawn template for either a loop room, or a normal room
                if(!success) { //if we made it here, we either have a newRoom assigned and ready to spawn, or we have no rooms left to as they all didn't fit
                    makeRoomConnectionSingleSided = true;
                }
            }

            //Instantiate
            if(instantiateRoom) { //if it's not a single sided room, we have to spawn the connecting room from the spawn we decided on. Otherwise we just skip it!
                instantiatedNewRoom = AddRoom(nextSpawn.roomToSpawn.GetComponent<RoomData>(), nextSpawn.roomOffset, nextSpawn.neededRotation, nextSpawn.isLoopRoom);

                if(!nextSpawn.isLoopRoom) {
                    for(int i = 0; i < instantiatedNewRoom.Doors.Count; i++) {
                        if(i != nextSpawn.possibleDoorAIndex) {
                            openSet.Add(instantiatedNewRoom.Doors[i]);
                        }
                    }
                } else {
                    for(int i = 0; i < instantiatedNewRoom.Doors.Count; i++) {
                        if(i == nextSpawn.possibleDoorAIndex || i == nextSpawn.possibleDoorBIndex) {//only add the doors that are not one of the two loop doors we are about to close
                        } else {
                            openSet.Add(instantiatedNewRoom.Doors[i]);
                        }
                    }
                    //we also need to remove the second door from openset.
                    loopTargetDoor = openSet[nextSpawn.otherSpawnedDoor];
                    openSet.RemoveAt(nextSpawn.otherSpawnedDoor);
                }
            }
            


            //spawn in door geometry ----
            GameObject doorToSpawn = null;
            if(instantiateRoom) {  //select the door type to spawn (from random list)
                doorToSpawn = generatorSettings.doors[rand.Next(0, generatorSettings.doors.Count)];
            } else {
                doorToSpawn = generatorSettings.deadendDoors[rand.Next(0, generatorSettings.deadendDoors.Count)];
            }

            //instantiate the doors
            //first door, we will always spawn
            Vector3 doorOffset = new Vector3(0f, 0.5f, 0f); //to offset it so the gameobject pivot is on the bottom edge of the voxel
            GameObject spawnedDoor = GameObject.Instantiate(doorToSpawn, doorForProcessing.position - (doorForProcessing.direction * 0.5f) - doorOffset, Quaternion.LookRotation(doorForProcessing.direction), this.transform);
            doorForProcessing.spawnedDoor = spawnedDoor;

            //if it's a loop room, we have to spawn in a second door as well
            GameObject loopSpawnedDoor = null;
            if(nextSpawn.isLoopRoom) {
                doorToSpawn = generatorSettings.doors[rand.Next(0, generatorSettings.doors.Count)]; //will never be a deadend door, so grab one from the normal doors list.

                Vector3 loopTargetVoxel = loopTargetDoor.position + loopTargetDoor.direction; //offset one voxel in door dir so we work on the unoccupied voxel the door leads to
                Vector3 loopTargetWorldVoxPos = GetVoxelWorldPos(loopTargetVoxel, loopTargetDoor.parent.rotation) + loopTargetDoor.parent.transform.position; //need this for offset
                Vector3 loopTargetWorldDoorDir = GetVoxelWorldDir(loopTargetDoor.direction, loopTargetDoor.parent.rotation); //the target voxel we're going to align to

                loopDoorForProcessing = new Door(loopTargetWorldVoxPos, loopTargetWorldDoorDir, loopTargetDoor.parent);

                loopSpawnedDoor = GameObject.Instantiate(doorToSpawn, loopDoorForProcessing.position - (loopDoorForProcessing.direction * 0.5f) - doorOffset, Quaternion.LookRotation(loopDoorForProcessing.direction), this.transform);
                loopDoorForProcessing.spawnedDoor = loopSpawnedDoor;
            }
            //-----doors complete

            //build graph.---------------------------------
            //instantiatedNewRoom and doorForProcessing.parent are the only two rooms that share the connection we just made so...
            //we also know instantiedNewRoom is brand new and has no other connections, we we can use that directly,
            //however we need to search for doorForProcessing.parent in the roomsList first as it could have more connections already, if it does, we need to add the connection
            //betwen it and
            if(!makeRoomConnectionSingleSided) {
                GraphNode newNode = new GraphNode();
                newNode.data = instantiatedNewRoom; //connect it both ways, so we access the data from the node, and the node from the data...
                instantiatedNewRoom.node = newNode;
                //grab the node of the room we are connecting to
                GraphNode lastNode = doorForProcessing.parent.node;
                newNode.depth = lastNode.depth + 1;
                if(newNode.depth > highestDepth) highestDepth = newNode.depth; //store the highest depth, used for debugging

                //make a connection for the two of them
                GraphConnection con = new GraphConnection();
                con.a = lastNode; //store the connections to the rooms
                con.b = newNode;
                con.open = true;
                con.doorRef = doorForProcessing; 

                lastNode.connections.Add(con); //store the connections both ways
                newNode.connections.Add(con);
                spawnedDoor.GetComponent<GeneratorDoor>().data = con;
                DungeonGraph.Add(newNode);

                if(nextSpawn.isLoopRoom) {
                    //also need to add the second door into the graph!
                    GraphNode otherLastNode = loopDoorForProcessing.parent.node;
                    GraphConnection otherCon = new GraphConnection();
                    otherCon.a = otherLastNode;
                    otherCon.b = newNode;
                    otherCon.open = true;
                    otherCon.doorRef = loopDoorForProcessing;

                    otherLastNode.connections.Add(otherCon);
                    newNode.connections.Add(otherCon);
                    loopSpawnedDoor.GetComponent<GeneratorDoor>().data = otherCon;
                    //Debug.Log("<color=red>LOOOOP</color>");
                }
            } else {
                //want to add just a new door as we did not spawn a room;
                GraphNode lastNode = doorForProcessing.parent.node;
             
                //make a connection for the two of them
                GraphConnection con = new GraphConnection();
                con.a = lastNode; //store the connections to the rooms
                con.b = null; //b is null, because this door is a "wall" that can not be passed through
                con.open = false;
                con.keyID = -1;
                con.doorRef = doorForProcessing; 
                lastNode.connections.Add(con); //store the connections both ways
                spawnedDoor.GetComponent<GeneratorDoor>().data = con;
            }
        }

        //Wrapping the interal post step, just generate doors and keys for now (eg, taking each door pair and spawning a gameplay door in it's place)
        private void PostGeneration() {
            Debug.Log("Dungeon Generator:: Post Generation Starting. ");

            //let the user hook in here once it's all done
            if(OnComplete != null) OnComplete(this);
        }

        public void DestroyAllGeneratedRooms() {
            while(transform.childCount > 0) {
                DestroyImmediate(transform.GetChild(0).gameObject);
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
        public RoomData AddRoom(RoomData prefabData, Vector3 pos, float rotation, bool b = false) {
            GameObject roomObj = GameObject.Instantiate(prefabData.gameObject, pos, Quaternion.AngleAxis(rotation, Vector3.up), this.transform);
            RoomData data = roomObj.GetComponent<RoomData>();
            data.rotation = rotation; //set the instantiated roomData's rotation to be used for the voxels transformation into worldspace later
            data.UpdateInstantiatedData();

            AllRooms.Add(roomObj);
            AddGlobalVoxels(data, pos, rotation);

            if(b) {
                roomObj.GetComponent<GameplayRoom>().ColorRoom(Color.black);
            }
            return data;
        }

        public void AddGlobalVoxels(RoomData d, Vector3 offset, float rotation = 0f) {
            for(int i = 0; i < d.LocalVoxels.Count; i++) {
                Voxel v = d.LocalVoxels[i];
                Vector3 r = GetVoxelWorldPos(v.position, rotation) + offset;
                Vector3 iV = new Vector3(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), Mathf.RoundToInt(r.z));
                GlobalVoxelGrid.Add(iV, true);
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

        private void ColorChildren(Transform t, Color c) {
            List<Renderer> childMats = t.GetComponentsInChildren<Renderer>().ToList();
            for(int i = 0; i < childMats.Count; i++) {
                childMats[i].material.color = c;
            }
        }

        /// <summary>
        /// Finds a path between the start and end nodes.  Interally, we don't need to use the path for anything, but we just need to make sure a path exsists, otherwise the graph is not solveable
        /// This is used for keys and locked doors and stuff like that
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public bool HasPath(GraphNode start, GraphNode end) {
            //iterate through the graph to zero it out
            for(int i = 0; i < DungeonGraph.Count; i++) {
                DungeonGraph[i].pathfindingWeight = -1f; //-1f means "not processed"
                DungeonGraph[i].pathfindingProcessed = false;
            }

            List<GraphNode> nodesToProcess = new List<GraphNode>();
            nodesToProcess.Add(start);
            start.pathfindingWeight = 0f;
            start.pathfindingProcessed = true;


            //do a flood fill starting from start room, increasing the depth every step
            //for(int i = 0; i < nodesToProcess.Count; i++) {
            //ColorChildren(start.data.gameObject.transform, Color.cyan);
            //this is infinite looping
            while(nodesToProcess.Count > 0) {
                GraphNode nodeToProcess = nodesToProcess[0];
                nodesToProcess.RemoveAt(0);
                nodeToProcess.pathfindingProcessed = true;


                for(int j = 0; j < nodeToProcess.connections.Count; j++) {

                    if(nodeToProcess.connections[j].open) {

                        GraphConnection c = nodeToProcess.connections[j];
                        if(!c.a.pathfindingProcessed) {
                            //a has not been processed, so add it to the "rooms to process" and set it's weight accordingly
                            c.a.pathfindingWeight = nodeToProcess.pathfindingWeight + 1f;
                            //c.a.pathfindingProcessed = true;
                            nodesToProcess.Add(c.a);
                            //ColorChildren(c.a.data.transform, Color.red);
                        }

                        if(!c.b.pathfindingProcessed) {
                            //a has not been processed, so add it to the "rooms to process" and set it's weight accordingly
                            c.b.pathfindingWeight = nodeToProcess.pathfindingWeight + 1f;
                            //c.a.pathfindingProcessed = true;
                            nodesToProcess.Add(c.b);
                            //ColorChildren(c.b.data.transform, Color.green);

                        }
                    } else {
                        //can't add this one as the way is closed!
                    }
                }
            }

            bool foundPath = false;
            if(end.pathfindingWeight != -1) {
                foundPath = true;
            }
            return foundPath;
        }

        private bool CheckGeneratorData() {
            bool hasError = false;
            if(generatorSettings.spawnRooms.Count == 0) {
                hasError = true;
                Debug.LogError("Dungeon Generator:: Data Issue! Spawn rooms list is empty (need at least one)!");
            }
            if(generatorSettings.possibleRooms.Count == 0) {
                hasError = true;
                Debug.LogError("Dungeon Generator:: Data Issue! Possible rooms list is empty (need at least one)!");
            }
            if(generatorSettings.deadendRooms.Count == 0) {
                hasError = true;
                Debug.LogError("Dungeon Generator:: Data Issue! Deadend rooms list is empty (need at least one)!");
            }
            if(generatorSettings.doors.Count == 0) {
                hasError = true;
                Debug.LogError("Dungeon Generator:: Data Issue! Doors list is empty (need at least one)!");
            }
            for(int i = 0; i < generatorSettings.spawnRooms.Count; i++) {
                if(generatorSettings.spawnRooms[i] == null) {
                    hasError = true;
                    Debug.LogError("Dungeon Generator:: Data Issue! Spawm rooms list has a missing/null entry at index: " + i);
                }
            }
            for(int i = 0; i < generatorSettings.possibleRooms.Count; i++) {
                if(generatorSettings.possibleRooms[i] == null) {
                    hasError = true;
                    Debug.LogError("Dungeon Generator:: Data Issue! Possible rooms list has a missing/null entry at index: " + i);
                }
            }
            for(int i = 0; i < generatorSettings.deadendRooms.Count; i++) {
                if(generatorSettings.deadendRooms[i] == null) {
                    hasError = true;
                    Debug.LogError("Dungeon Generator:: Data Issue! Deadend rooms list has a missing/null entry at index: " + i);
                }
            }
            for(int i = 0; i < generatorSettings.doors.Count; i++) {
                if(generatorSettings.doors[i] == null) {
                    hasError = true;
                    Debug.LogError("Dungeon Generator:: Data Issue! Deadend rooms list has a missing/null entry at index: " + i);
                }
            }

            if(hasError) {
                Debug.LogError("Dungeon Generator:: Data Issues found on dungeon set: [" + generatorSettings.name + "], fix these errors before proceeding!");
            }
            return hasError;
        }

        public static Color GetKeyColor(int keyID) {
            switch(keyID % 10) {
                case 0: return new Color(0.1f, 1f, 0.1f);
                case 1: return Color.red;
                case 2: return Color.blue;
                case 3: return Color.cyan;
                case 4: return Color.magenta;
                case 5: return Color.yellow;
                case 6: return Color.black;
                case 7: return Color.white;
                case 8: return new Color(1f, 0.5f, 0f); //orange
                case 9: return new Color(1f, 0.5f, 1f); //purple-y pink
            }
            return Color.green;
        }

        private void OnDrawGizmos() {

            Gizmos.color = Color.blue;
            for(int i = 0; i < openSet.Count; i++) {
                Vector3 v = GetVoxelWorldPos((openSet[i].position + openSet[i].direction), openSet[i].parent.rotation) + openSet[i].parent.transform.position;
                Gizmos.DrawWireCube(v, Vector3.one);
            }

            Gizmos.color = Color.green;
            if(drawGlobalVoxels) {
                Gizmos.color = globalVoxelColor;
                foreach(var i in GlobalVoxelGrid) {
                    Gizmos.DrawWireCube(i.Key, Vector3.one);
                }
            }

            if(drawAllDoors) {
                Gizmos.color = Color.cyan;
                for(int i = 0; i < AllDoorsData.Count; i++) {
                    Gizmos.DrawWireCube(AllDoorsData[i].position, Vector3.one);
                }
            }

#if UNITY_EDITOR
            if(drawDepthLabels) {
                GUIStyle style = new GUIStyle();
                style.fontSize = 20;
                style.normal.textColor = Color.red;

                for(int i = 0; i < DungeonGraph.Count; i++) {
                    Vector3 offset = new Vector3(0f, 0f, 0f);
                    Vector3 pos = DungeonGraph[i].data.transform.position + offset;

                    string label = DungeonGraph[i].depth.ToString();
                    //label = DungeonGraph[i].pathfindingWeight.ToString();

                    UnityEditor.Handles.Label(pos + new Vector3(0f, 0.1f, 0f), label, style);
                }
            }
#endif

            if(drawGraph) {

                for(int i = 0; i < DungeonGraph.Count; i++) {

                    Vector3 offset = new Vector3(0f, 0f, 0f);
                    Vector3 pos = DungeonGraph[i].data.transform.position + offset;
                    float s = 0.4f;
                    if(i == 0) {
                        Gizmos.color = Color.blue;
                        s = 1f;
                    } else {
                        Gizmos.color = Color.green;
                        s = 0.4f;
                    }

                    //Gizmos.DrawWireSphere(pos, s / 2f);

                    Color roomCol = debugGradient.Evaluate(((float)DungeonGraph[i].depth) / highestDepth);
                    if(i == 0) roomCol = Color.blue;
                    if(colourRoomsWithDepth) ColorChildren(DungeonGraph[i].data.gameObject.transform, roomCol);

                    if(drawKeyLocksLabels) {
                        if(DungeonGraph[i].keyIDs.Count != 0) {
                            string keyLabel = "keys: ";
                            for(int k = 0; k < DungeonGraph[i].keyIDs.Count; k++) {
                                keyLabel += DungeonGraph[i].keyIDs[k].ToString() + ", ";
                            }
                            GUIStyle style = new GUIStyle();
                            style.fontSize = 15;
                            style.normal.textColor = Color.red;
                            UnityEditor.Handles.Label(pos + new Vector3(0f, 0.4f, 0f), keyLabel, style);
                        }
                    }

                    s = 0.4f;
                    //since we have cyclic references, this will be drawn twice...
                    for(int j = 0; j < DungeonGraph[i].connections.Count; j++) {
                        GraphConnection c = DungeonGraph[i].connections[j];
                        if(c.open) Gizmos.color = Color.green;
                        else Gizmos.color = Color.red;


                        if(colourLockedDoors) {
                            ColorChildren(c.doorRef.spawnedDoor.transform, GetKeyColor(c.keyID));
                        }
                        Door doorRef = DungeonGraph[i].connections[j].doorRef;
                        Vector3 dPos = doorRef.spawnedDoor.transform.position + offset;

                        Gizmos.DrawWireCube(dPos, new Vector3(s, s, s));
                        if(!c.open) {
                            if(drawKeyLocksLabels) {
                                GUIStyle style = new GUIStyle();
                                style.fontSize = 15;
                                style.normal.textColor = Color.black;
                                if(c.keyID != -1) {
                                    UnityEditor.Handles.Label(dPos + new Vector3(0f, 0.4f, 0f), "Lock: " + c.keyID.ToString(), style);
                                }
                            }
                        }

                        if(c.b != null) {
                            Vector3 posStart = c.a.data.transform.position;
                            Vector3 posEnd = c.b.data.transform.position;

                            float l = ((float)DungeonGraph[i].depth) / highestDepth;
                            Color col = debugGradient.Evaluate(l);
                            col = graphConnectionColor;
                            Gizmos.color = col;
                            for(int thicc = 0; thicc < 5; thicc++) {
                                float thic = 0.01f * thicc;
                                Vector3 lineThicc = new Vector3(thic, thic, thic);
                                Gizmos.DrawLine(posStart + offset + lineThicc, dPos + lineThicc);
                                Gizmos.DrawLine(posEnd + offset + lineThicc, dPos + lineThicc);
                            }
                        }

                    }
                }
            }
        }
    }

    /// <summary>
    /// This is an internal class, used to "force" check a spawn template that we've already computed.
    /// We create one of these, and add it to a list that is checked first before the generator tries every other room like normal
    /// This is used specifically for looping rooms, as we precompute everything and only want to check it has no voxel overlaps BEFORe the normal spawning would happen
    /// Really, it's just a bunch of info about the room/orientation/how doors should connect up
    /// </summary>
    public class RoomSpawnTemplate {
        public bool isLoopRoom = false;

        public bool useMe = false; // owo
        public GameObject roomToSpawn;
        public Vector3 roomOffset;
        public int neededRotation;

        public int otherSpawnedDoor; 

        public int possibleDoorAIndex; //connects up to targetDoor (eg, the door we are processing currently
        public int possibleDoorBIndex; //connects up to otherSpawnDoor

    }

}
