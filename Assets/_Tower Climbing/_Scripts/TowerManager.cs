using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class TowerManager : MonoBehaviour {

    [Header("Platforms")]
    [SerializeField]
    private GameObject platPrefab;
    [FormerlySerializedAs("spawnPos")]
    [SerializeField]
    private Transform[] platformSpawns;
    [SerializeField]
    private float spawnRate;
    [Space(10)]

    [SerializeField]
    private Transform towerBase = null;
    [Space(10)]

    [Header("Floor Reference")]
    public GameObject floor;

    private float timer;
    private int randomPos;
    private int[] platRotations = { 0, 90, 180, 270 };
    private int cachedRandomRotationIndex;
    private bool isTowerRotating;
    private float rotationTarget;
    private float elapsed;
    private float startRotation;
    private float endRotation;

    private void Awake () 
    {
        timer = spawnRate;		
    }
	
	// Update is called once per frame
	void Update () {

        timer -= Time.deltaTime;
        if(timer <= 0)
        {
            randomPos = UnityEngine.Random.Range(0, platformSpawns.Length);
            SpawnPlat();
            timer = spawnRate;
        }
       
        if (isTowerRotating)
        {
            elapsed += Time.deltaTime;
            float interpolation = Mathf.Clamp01(elapsed / 0.15f);

            // Interpolate only the Y angle (with wrap-around support)
            float currentY = Mathf.LerpAngle(startRotation, endRotation, interpolation);

            // Keep current X and Z unchanged
            Vector3 currentEuler = transform.eulerAngles;
            currentEuler.y = currentY;

            transform.rotation = Quaternion.Euler(currentEuler);

            if (interpolation >= 1f)
            {
                isTowerRotating = false;
                elapsed = 0;
            }
        }
    
    }

    public void RotateTower(bool isLeftRotation)
    {
        if (isLeftRotation)
        {
            isTowerRotating = true;

            startRotation = this.transform.localEulerAngles.y;
            endRotation = startRotation - 90;

            //Vector3 towerRotation = this.transform.localEulerAngles;
            //towerRotation.y -= 90;
            //rotationTarget = towerRotation.y -= 90;
            //this.transform.localEulerAngles = towerRotation;
        }
        else
        {
            isTowerRotating = true;

            startRotation = this.transform.localEulerAngles.y;
            endRotation = startRotation + 90;

            //Vector3 towerRotation = this.transform.localEulerAngles;
            //rotationTarget = towerRotation.y += 90;

            //towerRotation.y += 90;
            //this.transform.localEulerAngles = towerRotation;

        }
    }

    public bool CanRotateTower()
    {
        return isTowerRotating == false;
    }

    // Spawn a plat using one of the 4 spawn positions
    private void SpawnPlat()
    {
        const int MAX_TRIES = 99;

        int randomIndex = -1;

        for (int i = 0; i < MAX_TRIES; i++)
        {
            randomIndex = UnityEngine.Random.Range(0, platRotations.Length);

            if(randomIndex != cachedRandomRotationIndex)
            {
                cachedRandomRotationIndex = randomIndex;
                break;
            }
        }

        GameObject platform = Instantiate(platPrefab, platformSpawns[randomPos].position, Quaternion.identity) as GameObject;
        platform.GetComponent<PlatformBehaviour>().Initialize(towerParentParam: towerBase);
        platform.transform.SetParent(towerBase.transform, true);
        
        Vector3 platRotation = platform.transform.localEulerAngles;
        platRotation.y = platRotations[randomIndex];
        platform.transform.localEulerAngles = platRotation;

    }
}
