using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameplayRoom : MonoBehaviour {

    public bool initialized = false;
    public int roomIndex = -1;

    public List<Transform> spawnLocations = new List<Transform>();
    public List<GameObject> spawnables = new List<GameObject>();

    public List<GameObject> spawnedRandomProps = new List<GameObject>();



    public void Init(int roomIndex, System.Random rand) {

        this.initialized = true;
        this.roomIndex = roomIndex;

        //spawn props randomly in the room based off locators set when buildin the room prefab
        int numSpawn = rand.Next(spawnLocations.Count);
        List<Transform> possibleLocations = new List<Transform>(spawnLocations);
        possibleLocations.Shuffle(rand);
        for(int i =0; i < numSpawn; i++) {
            Vector3 spawnLocation = possibleLocations[i].position;
            float randomAngle = (float)rand.Next(360);
            Quaternion spawnRotation = Quaternion.AngleAxis(randomAngle, Vector3.up);

            //choose a random spawnable
            int spawnableToUse = rand.Next(spawnables.Count);
            GameObject spawned = GameObject.Instantiate(spawnables[spawnableToUse], spawnLocation, spawnRotation, this.transform);
            spawnedRandomProps.Add(spawned);
        }
    }

    public void ColorRoom(Color c) {
        List<Renderer> childMats = this.GetComponentsInChildren<Renderer>().ToList();
        for(int i = 0; i < childMats.Count; i++) {
            childMats[i].material.color = c;
        }
    }
}
