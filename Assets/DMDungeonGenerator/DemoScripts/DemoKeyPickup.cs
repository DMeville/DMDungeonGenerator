using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemoKeyPickup : MonoBehaviour
{

    public int keyID = 0;

    public float currentRotation = 0f;
    public float rotationSpeed = 10f;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        currentRotation += rotationSpeed * Time.deltaTime;
        this.transform.rotation = Quaternion.AngleAxis(currentRotation, Vector3.up);
    }

    private void OnTriggerEnter(Collider other) {
        Debug.Log("Added key: " + keyID);
        if(other.gameObject.tag == "Player") {
            if(!DemoPlayer.HasKey(keyID)) {
                DemoPlayer.AddKey(keyID);


                GameObject.Destroy(this.gameObject);
            }
        }
    }
}
