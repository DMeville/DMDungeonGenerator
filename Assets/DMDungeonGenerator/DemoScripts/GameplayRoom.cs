using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameplayRoom : MonoBehaviour
{

    public void ColorRoom(Color c) {
        List<Renderer> childMats = this.GetComponentsInChildren<Renderer>().ToList();
        for(int i = 0; i < childMats.Count; i++) {
            childMats[i].material.color = c;
        }
    }
}
