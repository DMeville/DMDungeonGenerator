using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DMDungeonGenerator;

public class CallbackExample : MonoBehaviour
{
    // Start is called before the first frame update

    public DMDungeonGenerator.DungeonGenerator generator;

    public GameObject PlayerPrefab;
    public GameObject spawnedPlayer;
    public GameObject KeyPrefab;
    public List<GameObject> keys = new List<GameObject>();

    public int keysToSpawnMin = 5;
    public int keysToSpawnMax = 10;

    void Awake()
    {
        if(generator != null) {
            Debug.Log("Registered post generation callback");
            generator.OnComplete += GeneratorComplete;
        }
    }

    /// <summary>
    /// THIS IS JUST AN EXAMPLE.  This is where you want to do your game specific dungeon stuff.  
    /// This method lets you hook into when the generator is done placeing the ROOMS and computing the DUNGEON graph and placing the (shared) DOORS between rooms (one door per two rooms, eg, one door per connection)
    /// This will allow you to, choose random doors to lock, choose random rooms to place keys in, choose random rooms to be "special" rooms, post process the rooms to init enemies, randomized props, etc
    /// You can place custom monobehaviours on the room prefabs to help with this, and then call Init()'s here or something
    /// </summary>
    /// <param name="generator"></param>
    public void GeneratorComplete(DMDungeonGenerator.DungeonGenerator generator) {
        Debug.Log("CallbackExample::Generator complete!");

        //cleanup
        //Destroy the player if one already exists from the last generation
        if(spawnedPlayer != null)  GameObject.DestroyImmediate(spawnedPlayer);
        //Destroy any keys we may have spawned (from the last run of the generator if there is a prev gen)
        for(int i = 0; i < keys.Count; i++) GameObject.DestroyImmediate(keys[i].gameObject);
        keys = new List<GameObject>(); //clear the key list
        //cleanup done

        //do some processing to choose which doors to lock, and which rooms to spawn keys in...
        int numKeys = keysToSpawnMin + generator.rand.Next((keysToSpawnMax - keysToSpawnMin));//get a random amount of keys between [keysToSpawnMin, keysToSpawnMax]
        Debug.Log("CallbackExample::Computing Locks and Keys - Total keys: " + numKeys);
        ComputeLocksAndKeys(numKeys); //compute the data needed to spawn the key gameobjects
        SpawnKeys(); //spawn the gameobjets using the computed data

        ChooseEndRoom();
        //color the spawn room green
        generator.DungeonGraph[0].data.GetComponent<GameplayRoom>().ColorRoom(Color.green);

        //iterate through all the rooms and call a "Init" method to set up random props
        for(int i = 0; i < generator.DungeonGraph.Count; i++) {
            OnInitRoomCallback(generator.DungeonGraph[i].data.gameObject, generator.rand);
        }


        //spawn the player in the first room somewhere
        Vector3 spawnRoomPos = generator.DungeonGraph[0].data.gameObject.transform.position;
        spawnedPlayer = GameObject.Instantiate(PlayerPrefab, spawnRoomPos, Quaternion.identity);

    }

    /// <summary>
    /// THIS IS JUST AN EXAMPLE THAT CHOOSES ROOMS/DOORS AT RANDOM AND LOCKS THEM.  
    /// Hpw you want to compute this is up to you, right now it just chooses random rooms with nothing else.  
    /// You could write logic to make it so rooms have to be a certain distance apart to be locked (Every 3 rooms at least, etc)
    /// </summary>
    public void ComputeLocksAndKeys(int totalKeys, int randomStepsMax = 25) {
        int totalKeysGenerated = 0;
        
        for(int i = 0; i < totalKeys; i++) {
            //For every key we want to spawn; Look on the dungeon graph, grab a random room, then a random connection (door) in that room.
            //We then set this door to open=false, and assign it a keyID.  This sets up the DATA we need later..
            int randomRoomIndex = generator.rand.Next(generator.DungeonGraph.Count);
            GraphNode randomRoom = generator.DungeonGraph[randomRoomIndex];
            int randomDoorIndex = generator.rand.Next(randomRoom.connections.Count);

            GraphConnection randomDoor = generator.DungeonGraph[randomRoomIndex].connections[randomDoorIndex];
            if(!randomDoor.open) continue; //if we've already locked this door, we can't lock it with a SECOND key (I mean, we could, but not for this demo)
            randomDoor.open = false;
            randomDoor.keyID = totalKeysGenerated;
            //callback to the spawned gameplay door to update it's colour, set up lock stuff on the door...
            OnLockDoorCallback(randomDoor.doorRef.spawnedDoor); //(this could be nicer, not sure how tho)
            //we now have the door data setup, we need to now select a room that will be marked with "spawn key with this id" _somewhere_ in this room
            //for this example, we just spawn it on the ground; but you could put it in a chest, you could mark your room monster spawners to give the key to an enemy to drop on death, etc.


            GraphNode keyRoom; //By default, we place the key for the door, in the room that has the locked door.  
            //A GraphConnection stores the connection between two nodes (rooms), so in order to place it on the CORRECT side of the door (so it is accessable) we 
            //find which of the two rooms in the connection has the lowest depth.  If we put it in the room with the higher depth, the key would be on the other side...and you'd need the key to open the door to then get to the key!
            if(randomDoor.a != null && randomDoor.b != null) { //if both nodes in the connection are not null...
                if(randomDoor.a.depth < randomDoor.b.depth) keyRoom = randomDoor.a;
                else keyRoom = randomDoor.b;
            } else { //one node is null, so just assign the keyroom to whichever is not
                if(randomDoor.a == null) keyRoom = randomDoor.b;
                else keyRoom = randomDoor.a;
            }
            //we now can "walk" the key along the dungeon graph, a random number of steps.  This moves the key room to a different (nearby, usually) room so keys are a bit harder to find. 
            //a few rules about this.  A key can go to any room via the key room's connections
            //1) It CAN go through locked doors only if it's going through that locked door in the "right" way, eg, key Room with depth 1 -> Locked door -> room with depth 0.  Locked doors are like one-way platforms, we can walk throgh if we're walking to a room with lower depth.
            //2) Otherwise it can NOT go through locked doors, as we don't solve if "you would have this key so you could pass this door"

            //choose a random number of steps to walk the key around
            int randomSteps = generator.rand.Next(randomStepsMax);
            for(int s = 0; s < randomSteps; s++) {
                //lets get a list of all rooms we could step towards, starting from the room we are currently in.
                List<GraphConnection> possibleRooms = new List<GraphConnection>();
                for(int c = 0; c < keyRoom.connections.Count; c++) {
                    GraphConnection gc = keyRoom.connections[c];
                    if(gc.a == null || gc.b == null) { //if both nodes in the connection are not null...just do nothing
                    } else { //otherwise...
                        if(gc.open) { //if the door to this possible room is open, add it to the list of places we can end up this step
                            possibleRooms.Add(gc);
                        } else {
                            //If the door to this possible room is locked, we might still be able to move through it so long as we are moving towards a lower depth
                            GraphNode other; //the connection holds a ref to the two rooms it connects, since it is in our current room's connection list, we know one of the rooms is us, but we don't know whih one...
                                             //so figure that out so we can grab the other room
                            if(gc.a == keyRoom) other = gc.b;
                            else other = gc.a;

                            //then if the other rom has a lower depth, we can move through it's locked door so add that to the list of places we can end 
                            if(other.depth < keyRoom.depth) {
                                possibleRooms.Add(gc);
                            }
                        }
                    }
                }

                //We now have a list of all the rooms we can move to, choose one of them at random
                int conIndex = generator.rand.Next(possibleRooms.Count);
                GraphConnection selected = possibleRooms[conIndex];


                //Again, as a connection holds two rooms, and one of them is our current room, find the room of the two that isn't us,
                //and assign that as the key room
                if(selected.a == keyRoom)  keyRoom = selected.b;
                else  keyRoom = selected.a;
            }

            //once we're done random stepping, add the KeyID to that rooms key list.  Rooms can have multiple keys!
            keyRoom.keyIDs.Add(totalKeysGenerated);
            totalKeysGenerated++; 
        }
    }

    /// <summary>
    /// THIS IS JUST AN EXAMPLE THAT TAKES THE COMPUTED LOCK/KEY DATA AND SPAWNS ACTUALL GAMEOBECTS FOR PICKUPS  
    /// </summary>
    public void SpawnKeys() {
        //iterate through the dungeon graph, find the rooms that need keys to be spawned (these can be keys or switches, they work the same)
        for(int i = 0; i < generator.DungeonGraph.Count; i++) {
            GraphNode room = generator.DungeonGraph[i];

            for(int j = 0; j < room.keyIDs.Count; j++) {
                //we are just spawning simple gameobjects as pickups at the room position (plus a random offset so we don't get overlapping keys in rooms with more than 1 key)
                Vector3 keyOffset = new Vector3(Random.Range(-0.1f, 0.1f), 0f, Random.Range(-0.1f, 0.1f)); 
                Vector3 keyPos = room.data.transform.position; //just using the default room position for the spawn location 
                //generally you want to choose a spawning postion in a smarter way.  Perhaps having null locators in the room data or something

                GameObject spawnedKey = GameObject.Instantiate(KeyPrefab, keyPos + keyOffset, Quaternion.identity);
                spawnedKey.GetComponent<DemoKeyPickup>().keyID = room.keyIDs[j]; //set the keyID so when the player picks up the key, he knows what ID the key is for
                keys.Add(spawnedKey); //add it to a list for safekeeping (and for cleanup purposes)

                //setting the colour of the key (and key light) for demo purposes based on the keyID
                spawnedKey.GetComponentInChildren<Renderer>().material.color = DMDungeonGenerator.DungeonGenerator.GetKeyColor(room.keyIDs[j]);
                spawnedKey.GetComponentInChildren<Light>().color = DMDungeonGenerator.DungeonGenerator.GetKeyColor(room.keyIDs[j]);
            }
        }
    }

    /// <summary>
    /// THIS IS JUST AN EXAMPLE That chooses a random room (with one connection) and marks it as special, maybe this is the room with the dungeon exit or something.
    /// </summary>
    public void ChooseEndRoom() {
        List<GraphNode> possibleRooms = new List<GraphNode>();
        for(int i = 0; i < generator.DungeonGraph.Count; i++) {
            if(generator.DungeonGraph[i].connections.Count == 3) {
                possibleRooms.Add(generator.DungeonGraph[i]);
            }
        }
        if(possibleRooms.Count > 0) {
            int randomSelected = generator.rand.Next(possibleRooms.Count);
            GameObject selectedRoom = possibleRooms[randomSelected].data.gameObject; //here we can get the gameobject of the room and any associated scripts on it...for this demo we're not doing anything, just gunna colour the room
            if(selectedRoom.GetComponent<GameplayRoom>() != null) {
                selectedRoom.GetComponent<GameplayRoom>().ColorRoom(Color.red);
            }
        }
    }

    /// <summary>
    /// EXAMPLE. 
    /// This is called whenever we lock a door. Use the door gameobject to get any custom component on your object and hook into animation, or chaning the look of your door depending on it's state, etc.
    ///  In this example we just change the colour on via our example gameplay script on the gameobject
    /// </summary>
    /// <param name="door"></param>
    private void OnLockDoorCallback(GameObject door) {
        GameplayDoor d = door.GetComponent<GameplayDoor>();
        if(d != null) d.LockDoor();
    }

    /// <summary>
    /// Example.  Calling a method on every room to initialize it post generation.  Gunna use this to randomize some room props. Passing in the generator so we can grab it's System.random 
    /// so props randomize the same for every generation that uses this seed
    /// </summary>
    /// <param name="room"></param>
    /// <param name="generator"></param>
    private void OnInitRoomCallback(GameObject room, System.Random rand) {
        GameplayRoom r = room.GetComponent<GameplayRoom>();
        if(r != null) r.Init(rand);       
    }
}
