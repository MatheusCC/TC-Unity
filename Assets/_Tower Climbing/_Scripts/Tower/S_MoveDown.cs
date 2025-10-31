using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TowerBehaviour : MonoBehaviour {

    // Use this for initialization

    public float speed;

	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {


        transform.position += (transform.up * -1) * speed * Time.deltaTime; 
		
	}
}
