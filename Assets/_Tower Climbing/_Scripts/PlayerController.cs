using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private TowerManager towerManager = null;
    [SerializeField]
    private Rigidbody playerRigidbody = null;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        
    }

    // Update is called once per frame
    private void FixedUpdate()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            if(towerManager.CanRotateTower())
            {
                Jump();
                towerManager.RotateTower(isLeftRotation: true);

            }
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            if (towerManager.CanRotateTower())
            {
                Jump();
                towerManager.RotateTower(isLeftRotation: false);
            }
        }
    }

    private void Jump()
    {
        playerRigidbody.AddForce(Vector3.up * 7, ForceMode.Impulse);
    }
}
