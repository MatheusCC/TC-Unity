using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class S_RotateTower : MonoBehaviour {
    private Transform TowerFloor;
    public float RotateSpeed = 0;
    public Vector3 towerOldRotation;
    public Quaternion RotateTo;
    public float t;
    public bool keyPressed = false;
	// Use this for initialization
	void Start () {
        TowerFloor = GameObject.Find("Floor").GetComponent<Transform>();
        towerOldRotation = TowerFloor.rotation.eulerAngles;
	}

    // Update is called once per frame
    void Update()
    {
        //If the tower's old position is equal to a 90 degree angle and left or right arrows are pressed,
        //Rotate the tower to a 90 degree angle
      //  if (Mathf.Abs(towerOldRotation.y - TowerFloor.rotation.eulerAngles.y) < 0.1f && !keyPressed)
       {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
               
                RotateTo = new Quaternion(0, TowerFloor.rotation.y - 90, 0, 0);
                towerOldRotation = RotateTo.eulerAngles;
                keyPressed = true;
                t = 0;

            }
           else if (Input.GetKeyDown(KeyCode.RightArrow))
            {

                RotateTo = new Quaternion(0, TowerFloor.rotation.y + 90, 0, 0);
                towerOldRotation = RotateTo.eulerAngles;
                keyPressed = true;
                t = 0;


            }

        }
        if (keyPressed)
        {
            
            t += RotateSpeed * Time.deltaTime;
            TowerFloor.rotation =  Quaternion.Lerp(TowerFloor.rotation, RotateTo, t);
            if(t >= 1)
            {
                keyPressed = false;
            }
        }
        Debug.Log(Mathf.Abs(towerOldRotation.y - TowerFloor.rotation.eulerAngles.y));
        Debug.Log(TowerFloor.rotation);
    }
}
