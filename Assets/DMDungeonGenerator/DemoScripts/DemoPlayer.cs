using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemoPlayer : MonoBehaviour
{

    public float mouseSensX = 100f;
    public float mouseSensY = 100f;
    public float moveSpeed = 10f;

    public Transform camera;
    private float yRot = 0f;
    private Rigidbody rb;
    public CharacterController cc;
    private float gravity = 0f;

    public static List<int> keysFound = new List<int>();

    void Start()
    {
        yRot = 0f;
        rb = this.GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensX * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensY * Time.deltaTime;

        yRot += mouseY;
        yRot = Mathf.Clamp(yRot, -90f, 90f);

        this.transform.Rotate(Vector3.up * mouseX);
        camera.transform.localRotation = Quaternion.AngleAxis(yRot, Vector3.left);


        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = x * transform.right + z * transform.forward;
        gravity = -9.81f;
        move += new Vector3(0f, gravity, 0f);
        //rb.MovePosition(move);
        cc.Move(move * moveSpeed * Time.deltaTime) ;
        if(cc.isGrounded) gravity = 0f;

       
    }

    public static bool HasKey(int keyID) {
        if(keysFound.IndexOf(keyID) == -1) {
            return false;
        } else {
            return true;
        }
    }

    public static void AddKey(int keyID) {
        keysFound.Add(keyID);
    }
}
