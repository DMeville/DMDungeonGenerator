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

    void Start()
    {
        if(generator != null) {
            generator.OnComplete += GeneratorComplete;
        }
    }

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
        int numKeys = 3 + generator.rand.Next(12);// [3-15] keys
        Debug.Log("Num keys: " + numKeys);
        ComputeLocksAndKeys(numKeys);
        SpawnKeys();



        //spawn the player in the first room somewhere
        Vector3 spawnRoomPos = generator.DungeonGraph[0].data.gameObject.transform.position;
        spawnedPlayer = GameObject.Instantiate(PlayerPrefab, spawnRoomPos, Quaternion.identity);

    }

    /// <summary>
    /// HOw you want to compute this is up to you, right now it just chooses random rooms with nothing else.  
    /// You could write logic to make it so rooms have to be a certain distance apart to be locked (Every 3 rooms at least, etc)
    /// </summary>
    public void ComputeLocksAndKeys(int totalKeys, int randomStepsMax = 25) {
        int totalKeysGenerated = 0;
        int r = totalKeys;

        //r = 1;
        for(int i = 0; i < r; i++) {
            //get a random door
            int rr = generator.rand.Next(generator.DungeonGraph.Count);
            GraphNode n = generator.DungeonGraph[rr];
            int rc = generator.rand.Next(n.connections.Count);
            if(!generator.DungeonGraph[rr].connections[rc].open) continue;
            generator.DungeonGraph[rr].connections[rc].open = false;
            generator.DungeonGraph[rr].connections[rc].keyID = totalKeys;

            //also need to choose a room in which the key is available
            //lets just place it in the room with the lowest depth that is connected to the locked door

            GraphConnection con = generator.DungeonGraph[rr].connections[rc];
            GraphNode keyRoom;
            if(con.a.depth < con.b.depth) {
                //place key in ROOM A, as it is on the close side towards spawn
                keyRoom = con.a;
            } else {
                keyRoom = con.b;
            }

            //we now can "walk" the key around a random number of steps
            //a few rules about this.
            //1) A key can go to any room via the key room's connections
            //2) It can NOT go through locked doors, we don't solve if "you would have this key so you could pass this door"
            //2) It CAN go through locked doors only if it's going through that locked door in the "right" way, eg, key Room with depth 1 -> Locked door -> room with depth 0

            //this works, it seems that a good chunk of the keys are staying in the same room they spawn in though...
            //might be stepping back and forth?
            int randomSteps = generator.rand.Next(randomStepsMax);
            for(int s = 0; s < randomSteps; s++) {
                //lets get a list of all the random steps we can take, then choose one at random
                List<GraphConnection> pos = new List<GraphConnection>();
                for(int c = 0; c < keyRoom.connections.Count; c++) {
                    GraphConnection gc = keyRoom.connections[c];
                    if(gc.open) {
                        pos.Add(gc); //add this connection as it is not locked
                    } else {
                        //room is locked, can we still move down it? 
                        //eg, is the room in the connection that is NOT us, at a lower depth?
                        GraphNode other;
                        if(gc.a == keyRoom) {
                            other = gc.b;
                        } else {
                            other = gc.a;
                        }
                        if(other.depth < keyRoom.depth) {
                            pos.Add(gc);
                        }
                    }
                }
                int conIndex = generator.rand.Next(pos.Count);
                GraphConnection selected = pos[conIndex];


                //key room is now whatever room in selected that is not keyroom
                if(selected.a == keyRoom) {
                    keyRoom = selected.b;
                } else {
                    keyRoom = selected.a;
                }

            }

            keyRoom.keyIDs.Add(totalKeys);
            totalKeysGenerated++;
        }
    }

    public void SpawnKeys() {
        //iterate through the dungeon graph, find the rooms that need keys to be spawned (these can be keys or switches, they work the same)
        for(int i = 0; i < generator.DungeonGraph.Count; i++) {
            DMDungeonGenerator.GraphNode room = generator.DungeonGraph[i];
            for(int j = 0; j < room.keyIDs.Count; j++) {
                Vector3 keyPos = room.data.transform.position; //just using the default room position for the spawn location 
                Vector3 keyOffset = new Vector3(Random.Range(-0.1f, 0.1f), 0f, Random.Range(-0.1f, 0.1f)); //plus a random small offset because we only have one spawn position, so if a room has two keys to spawn, they are not overlapping
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

}
