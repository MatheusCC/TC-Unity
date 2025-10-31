using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformBehaviour : MonoBehaviour {

    [SerializeField]
    private float speed = 1.0f;
    

    private Transform towerParent;
    private bool isAttached;

    public void Initialize(Transform towerParentParam)
    {
        towerParent = towerParentParam;
    }

    // Update is called once per frame
    void Update () {

        if(!isAttached)
        {
            MoveDown();
        }
	}

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Plat")
        {
            //AttachToTower();
            isAttached = true;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Plat")
        {
            //AttachToTower();
            isAttached = true;
        }
    }

    private void MoveDown()
    {
        transform.position -= transform.up * speed * Time.deltaTime;
    }

    private void AttachToTower()
    {
        this.transform.SetParent(towerParent.transform, false);
        isAttached = true;
    }
}
