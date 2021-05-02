using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        for(int i = 0; i < keys.Count; i++) {
            GameObject.DestroyImmediate(keys[i].gameObject);
        }
        keys = new List<GameObject>();

        //iterate through the dungeon graph, find the rooms that need keys to be spawned (these can be keys or switches, they work the same)
        for(int i = 0; i < generator.DungeonGraph.Count; i++) {
            DMDungeonGenerator.GraphNode room = generator.DungeonGraph[i];
            for(int j = 0; j < room.keyIDs.Count; j++) {
                GameObject k = GameObject.Instantiate(KeyPrefab, room.data.transform.position, Quaternion.identity);
                k.GetComponent<DemoKeyPickup>().keyID = room.keyIDs[j];
                keys.Add(k);
            }
        }


        //spawn the player in the first room somewhere
        if(spawnedPlayer != null) {
            GameObject.DestroyImmediate(spawnedPlayer);
        }
        Vector3 spawnRoomPos = generator.DungeonGraph[0].data.gameObject.transform.position;
        spawnedPlayer = GameObject.Instantiate(PlayerPrefab, spawnRoomPos, Quaternion.identity);

    }

}
