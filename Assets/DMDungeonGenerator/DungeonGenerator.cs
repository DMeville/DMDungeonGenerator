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

            StartGenerator(randomSeed);
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
                Debug.Log("Dungeon Generator:: Generation failed to meet min rooms [" + AllRooms.Count + "/"+generatorSettings.minRooms +"] ... trying again with seed++ [ " + this.randomSeed +" ]");
                return;
            }

            Debug.Log("Dungeon Generator:: Generation Complete in [" + DMDebugTimer.Lap() + "ms] and [" + attempts + "] attempts");
            generationComplete = true;

        }

        public void GenerateNextRoom() {
            Door targetDoor = openSet[0]; //grab the first door in the openset to process
            openSet.RemoveAt(0);

            Vector3 targetVoxel = targetDoor.position + targetDoor.direction; //offset one voxel in door dir so we work on the unoccupied voxel the door leads to
            Vector3 targetWorldVoxPos = GetVoxelWorldPos(targetVoxel, targetDoor.parent.rotation) + targetDoor.parent.transform.position; //need this for offset
            Vector3 targetWorldDoorDir = GetVoxelWorldDir(targetDoor.direction, targetDoor.parent.rotation); //the target voxel we're going to align to

            Door doorForProcessing = new Door(targetWorldVoxPos, targetWorldDoorDir, targetDoor.parent); //why do I do this instead of using targetDoor directly...?
            //AllDoorsData.Add(doorForProcessing);

            List<GameObject> roomsToTry = new List<GameObject>(generatorSettings.possibleRooms);
            //create a copy of the "all possible rooms list" so we can pick and remove from this list as we try different rooms
            //this ensures we don't try the same room over and over, and so we know when we have exhausted all the possiblities and just have to cap it off with a 1x1x1 vox room
            
            for(int i =0; i < roomsToTry.Count; i++) { //find the room template of the room we are trying to connect to, and remove that room template from the list of possible rooms to spawn
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



            //spawn in door geometry
            int di = rand.Next(0, generatorSettings.doors.Count); //get a random door from the list
            GameObject doorToSpawn = generatorSettings.doors[di];
            Vector3 doorOffset = new Vector3(0f, 0.5f, 0f); //to offset it so the gameobject pivot is on the bottom edge of the voxel
            GameObject spawnedDoor = GameObject.Instantiate(doorToSpawn, doorForProcessing.position - (doorForProcessing.direction * 0.5f) - doorOffset , Quaternion.LookRotation(doorForProcessing.direction), this.transform);
            doorForProcessing.spawnedDoor = spawnedDoor; 

            //need to link up the doors to the roomData's too?
            //AllDoors.Add(spawnedDoor);

            //instantiatedNewRoom.Doors[doorIndex].spawnedDoor = spawnedDoor;


            //build graph.. we know...
            //instantiatedNewRoom and doorForProcessing.parent are the only two rooms that share the connection we just made so...
            //we also know instantiedNewRoom is brand new and has no other connections, we we can use that directly,
            //however we need to search for doorForProcessing.parent in the roomsList first as it could have more connections already, if it does, we need to add the connection
            //betwen it and

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
            con.doorRef = doorForProcessing; //this needs a reference to the door geometry, as we need to be able to...unlock it visually? We could hook it up to a data ref, which then has a ref to geometry too but that seems painfully overcomplicated

            lastNode.connections.Add(con); //store the connections both ways
            newNode.connections.Add(con);
            spawnedDoor.GetComponent<GeneratorDoor>().data = con;
            DungeonGraph.Add(newNode);
        }

        //Wrapping the interal post step, just generator doors for now (eg, taking each door pair and spawning a gameplay door in it's place)
        private void PostGeneration() {

            //Debug.Log("We need to lock all the doors before placing ANY keys!");
            //because we can place a key in a valid location, then a lock a door infront of it that might cause issues, maybe?
            //actually it might be ok...

            //example showing how lock random doors
            //Locking random doors
            


            ////Example showing how to choose two random rooms, and see if a path exsists between the two; this is used for checking if the dungeon is solvable, does not actually return any path
            ////get two random rooms, try and pathfind between them
            //int ra = rand.Next(DungeonGraph.Count);
            //int rb = rand.Next(DungeonGraph.Count);
            //ra = 0;

            //bool hasPath = HasPath(DungeonGraph[0], DungeonGraph[rb]);
            ////ColorChildren(DungeonGraph[ra].data.gameObject.transform, Color.magenta);
            ////ColorChildren(DungeonGraph[rb].data.gameObject.transform, Color.black);
            //Debug.Log("Found path: " + hasPath);


            ////Debug.Log("Walked DungeonGraph, max depth was: " + maxDepth);


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
                Debug.LogError("Dungeon Generator:: Data Issues found on dungeon set: [" + generatorSettings.name +"], fix these errors before proceeding!");
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

            if(drawGraph ) {

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

                    Color roomCol = debugGradient.Evaluate(((float)DungeonGraph[i].depth)/ highestDepth);
                    if(i == 0) roomCol = Color.blue;
                    if(colourRoomsWithDepth) ColorChildren(DungeonGraph[i].data.gameObject.transform, roomCol);

                    if(drawKeyLocksLabels) {
                        if(DungeonGraph[i].keyIDs.Count != 0) {
                            string keyLabel = "keys: ";
                            for(int k = 0; k < DungeonGraph[i].keyIDs.Count; k++) {
                                keyLabel += DungeonGraph[i].keyIDs[k].ToString() + ", ";
                            }
                            GUIStyle style = new GUIStyle();
                            style.fontSize = 25;
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

                        if(!c.open) {
                            Gizmos.DrawWireCube(dPos, new Vector3(s, s, s));
                            if(drawKeyLocksLabels) {
                                GUIStyle style = new GUIStyle();
                                style.fontSize = 25;
                                style.normal.textColor = Color.black;
                                UnityEditor.Handles.Label(dPos + new Vector3(0f, 0.4f, 0f), "Lock: " + c.keyID.ToString(), style);
                            }
                        }


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
