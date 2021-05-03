using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class GameplayDoor : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject doorMesh;
    public GameObject doorTrigger;

    public DMDungeonGenerator.GeneratorDoor genDoor;

    //wait for the player to enter/exit the trigger zone and show/hide the door accordingly.
    //you could also trigger an animation opening the door, show some UI that says "Press <space> to Open door" etc etc
    //this is normal gameplay stuff

    private void Start() {
        genDoor = this.GetComponent<DMDungeonGenerator.GeneratorDoor>();
    }

    private void OnTriggerEnter(Collider other) {

        bool needsKey = !genDoor.data.open; //a door is either locked or open (eg, unlocked)
        int neededKey = neededKey = genDoor.data.keyID;
    

        if((needsKey == false) ||  (needsKey && DemoPlayer.HasKey(neededKey))) {

            if(other.gameObject.tag == "Player") {
                doorMesh.gameObject.SetActive(false);
            }
        }
    }

    private void OnTriggerExit(Collider other) {
        if(other.gameObject.tag == "Player") {
            doorMesh.gameObject.SetActive(true);
        }
    }

    public void LockDoor() {
        List<Renderer> childMats = this.GetComponentsInChildren<Renderer>().ToList();
        for(int i = 0; i < childMats.Count; i++) {
            childMats[i].material.color = DMDungeonGenerator.DungeonGenerator.GetKeyColor(genDoor.data.keyID);
        }
    }
}
