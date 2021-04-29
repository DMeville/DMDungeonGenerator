using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CallbackExample : MonoBehaviour
{
    // Start is called before the first frame update

    public DMDungeonGenerator.DungeonGenerator generator;

    void Start()
    {
        if(generator != null) {
            generator.OnComplete += GeneratorComplete;
        }
    }

    public void GeneratorComplete(DMDungeonGenerator.DungeonGenerator generator) {
        Debug.Log("Generator complete!");
    }


}
