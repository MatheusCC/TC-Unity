using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private enum JumpDirection
    {
        NONE,
        LEFT,
        RIGHT,
        FORWARD,
        BACKWARDS
    }

    [System.Serializable]
    private struct JumpSpots
    {
        [SerializeField]
        private Transform topSpot;
        [SerializeField]
        private Transform leftSpot;
        [SerializeField]
        private Transform rightSpot;
        [SerializeField]
        private Transform bottomSpot;

        public Vector3 GetTopSpot 
        {  
            get { return topSpot.position; }
        }
        public Vector3 GetLeftSpot 
        { 
            get { return leftSpot.position; } 
        }
        public Vector3 GetRightSpot 
        { 
            get { return rightSpot.position; } 
        }
        public Vector3 GetBottomSpot 
        { 
            get { return bottomSpot.position; } 
        }
    }

    [SerializeField]
    private TowerManager towerManager = null;
    [SerializeField]
    private Rigidbody playerRigidbody = null;
    [SerializeField]
    private float jumpTime = 0.25f;
    [SerializeField]
    private float jumpHeight = 5.0f;
    [SerializeField]
    private float rotationAngleSpeed = 30.0f;

    [SerializeField]
    private JumpSpots jumpSpots;


    private JumpDirection currentJumpDirection;
    private float rotationAngle;

    private Vector3 cachedSpotTargetPos;
    private bool isPlayerJumping;

    private float horizontalElapsed;
    private float verticalElapsed;

    private float startXPos;
    private float endXPos;
    private float startZPos;
    private float endZPos;
    private float startYPos;
    private float endYPos;
    private float rotationXAngle;
    private float rotationZAngle;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        currentJumpDirection = JumpDirection.NONE;
    }

    private void Update()
    {
        if(!isPlayerJumping)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                // LEFT
                SetJumpRotationDirection(JumpDirection.LEFT);
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                // BOTTOM
                SetJumpRotationDirection(JumpDirection.BACKWARDS);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                // RIGHT
                SetJumpRotationDirection(JumpDirection.RIGHT);
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                // TOP
                SetJumpRotationDirection(JumpDirection.FORWARD);
            }
        }

        if (isPlayerJumping)
        {
            horizontalElapsed += Time.deltaTime;
            verticalElapsed += Time.deltaTime;

            float horizontalInterpolation = Mathf.Clamp01(horizontalElapsed / jumpTime);
            float verticalInterpolation = Mathf.Clamp01(verticalElapsed / (jumpTime / 2) );

            // Interpolate current axis values
            float currentX = Mathf.Lerp(startXPos, endXPos, horizontalInterpolation);
            float currentZ = Mathf.Lerp(startZPos, endZPos, horizontalInterpolation);
            float currentY; 

            if (horizontalInterpolation <= 0.5f)
            {
                // Lerp while moving up
                currentY = Mathf.Lerp(startYPos, endYPos, verticalInterpolation);
            }
            else
            {
                // Lerp while moving down
                currentY = Mathf.Lerp(startYPos, endYPos, verticalInterpolation);
            }

            // Set new Vector3 with new axis values
            Vector3 currentPosition = transform.position;
            currentPosition.x = currentX;
            currentPosition.z = currentZ;
            currentPosition.y = currentY;


            // Set current player position
            transform.position = currentPosition;

            RotatePlayer();

            // Stop interpolation if rotation has been completed
            if (horizontalInterpolation >= 1f)
            {
                CompletePlayerJump();
            }

            if(verticalInterpolation >= 1f)
            {
                endYPos = startYPos;
                startYPos = this.transform.position.y;
                verticalElapsed = 0;
            }
        }
    }

    /*
    // Update is called once per frame
    private void FixedUpdate()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            if(towerManager.CanRotateTower())
            {
                Jump();
                //towerManager.RotateTower(isLeftRotation: true);

            }
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            if (towerManager.CanRotateTower())
            {
                Jump();
                //towerManager.RotateTower(isLeftRotation: false);
            }
        }
    }
    */

    private void JumpToSpot(Vector3 spotPosition)
    {
        //playerRigidbody.AddForce(Vector3.up * 7, ForceMode.Impulse);
        startXPos = this.transform.position.x;
        startZPos = this.transform.position.z;

        endXPos = spotPosition.x;
        endZPos = spotPosition.z;

        startYPos = this.transform.position.y;
        endYPos = startYPos + jumpHeight;

        cachedSpotTargetPos = spotPosition;

        isPlayerJumping = true;
    }

    private void CompletePlayerJump()
    {
        isPlayerJumping = false;
        this.transform.localPosition = cachedSpotTargetPos;
        this.transform.localEulerAngles = Vector3.zero;

        horizontalElapsed = 0;
        verticalElapsed = 0;
    }
    
    private void SetJumpRotationDirection(JumpDirection jumpDirection)
    {
        currentJumpDirection = jumpDirection;
        
        switch (jumpDirection)
        {
            case JumpDirection.LEFT:
                rotationAngle = rotationAngleSpeed;
                rotationZAngle = 0;
                JumpToSpot(jumpSpots.GetLeftSpot);
                break;

            case JumpDirection.RIGHT:
                rotationAngle = -rotationAngleSpeed;
                rotationZAngle = 0;
                JumpToSpot(jumpSpots.GetRightSpot);
                break;

            case JumpDirection.FORWARD:
                rotationAngle = rotationAngleSpeed;
                rotationXAngle = 0;
                JumpToSpot(jumpSpots.GetTopSpot);
                break;

            case JumpDirection.BACKWARDS:
                rotationAngle = -rotationAngleSpeed;
                rotationXAngle = 0;
                JumpToSpot(jumpSpots.GetBottomSpot);
                break;

            default:
                Debug.LogError("[PlayerController] Invalid Jump Direction enum!!", this);
                break;

        }      
    }

    private void RotatePlayer()
    {
        Vector3 currentEuler = transform.eulerAngles;

        if (currentJumpDirection == JumpDirection.LEFT || currentJumpDirection == JumpDirection.RIGHT)
        {
            rotationZAngle += rotationAngle;
            //currentEuler.z += rotationAngle;
            transform.localRotation = Quaternion.Euler(0.0f, 0.0f, rotationZAngle);
        }
        else if (currentJumpDirection == JumpDirection.FORWARD || currentJumpDirection == JumpDirection.BACKWARDS)
        {
            rotationXAngle += rotationAngle;
            //currentEuler.x += rotationAngle;
            transform.localRotation = Quaternion.Euler(rotationXAngle, 0.0f, 0.0f);
        }
    }
}
