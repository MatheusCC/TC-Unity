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
    private float spawnPlatRate;
    [Space(10)]

    [SerializeField]
    private Transform towerBase = null;
    [SerializeField]
    private float rotationTime = 0.15f;
    [Space(10)]

    [Header("Floor Reference")]
    public GameObject floor;

    private float platSpawnTimer;
    private int randomPos;
    private int[] platDefaultRotations = { 0, 90, 180, 270 };
    private int cachedRandomRotationIndex;
    private bool isTowerRotating;
    private float elapsed;
    private float startYRotation;
    private float endYRotation;

    private void Awake () 
    {
        platSpawnTimer = spawnPlatRate;		
    }
	
	// Update is called once per frame
	void Update () {

        platSpawnTimer -= Time.deltaTime;
        if(platSpawnTimer <= 0)
        {
            randomPos = UnityEngine.Random.Range(0, platformSpawns.Length);
            SpawnPlat();
            platSpawnTimer = spawnPlatRate;
        }
        

        // Lerp tower euler angle 
        if (isTowerRotating)
        {
            elapsed += Time.deltaTime;
            float interpolation = Mathf.Clamp01(elapsed / rotationTime);

            // Interpolate only the Y angle
            float currentY = Mathf.LerpAngle(startYRotation, endYRotation, interpolation);

            // Set new Vector3 with new euler angles
            Vector3 currentEuler = transform.eulerAngles;
            currentEuler.y = currentY;

            // Set tower with new euler angles
            transform.rotation = Quaternion.Euler(currentEuler);

            // Stop interpolation if rotation has been completed
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

            startYRotation = this.transform.localEulerAngles.y;
            endYRotation = startYRotation - 90;
        }
        else
        {
            isTowerRotating = true;

            startYRotation = this.transform.localEulerAngles.y;
            endYRotation = startYRotation + 90;
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
        int randomRotation = -1;

        // Before to spawn a new platform, it is necessary to avoid to use the same rotation,
        // as the last spawned platform, to avoid dead locks.
        for (int i = 0; i < MAX_TRIES; i++)
        {
            randomRotation = UnityEngine.Random.Range(0, platDefaultRotations.Length);

            // Use a different rotation value from last one cached
            if(randomRotation != cachedRandomRotationIndex)
            {
                cachedRandomRotationIndex = randomRotation;
                break;
            }
        }

        GameObject platform = Instantiate(platPrefab, platformSpawns[randomPos].position, Quaternion.identity) as GameObject;
        platform.GetComponent<PlatformBehaviour>().Initialize(towerParentParam: towerBase);
        platform.transform.SetParent(towerBase.transform, true);
        
        Vector3 platRotation = platform.transform.localEulerAngles;
        platRotation.y = platDefaultRotations[randomRotation];
        platform.transform.localEulerAngles = platRotation;

    }
}
