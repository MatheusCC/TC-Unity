using UnityEngine;
using UnityEngine.InputSystem;
using PawsAndCare.Building;
using PawsAndCare.Input;

namespace PawsAndCare.Camera
{
    /// <summary>
    /// Isometric camera controller: WASD/arrow pan, middle-mouse drag, scroll zoom,
    /// Q/E snap rotation, optional edge-pan, auto pan-bounds from GridSystem.
    /// </summary>
    // Namespace note: PawsAndCare.Camera shadows UnityEngine.Camera. Use the fully
    // qualified UnityEngine.Camera when referring to the Camera component type.
    //
    // Design: "target-and-smooth" pattern. Input updates target values
    // (targetRigPosition / targetZoom / targetYRotation); each frame the actual
    // transform eases toward them via SmoothDamp. This decouples "what the player
    // wants" from "how it animates" — input handling stays simple while motion
    // stays buttery.
    public class IsometricCameraController : MonoBehaviour
    {
        // Hierarchy references — assigned in inspector.
        // Rig holds world position (panning moves this).
        // Pivot holds the Y rotation (Q/E spins this).
        // MainCamera holds orthographic size (scrolling adjusts this) and a fixed local pitch.
        // GridSystem is queried at Start to compute auto pan bounds.
        [SerializeField]
        private Transform cameraRig = null;
        [SerializeField]
        private Transform cameraPivot = null;
        [SerializeField]
        private UnityEngine.Camera mainCamera = null;
        [SerializeField]
        private GridSystem gridSystem = null;

        // Pan tuning.
        // panSpeed is world units per second at the reference zoom (panZoomScalingRef).
        // At other zoom levels, pan speed scales linearly with orthographicSize so the
        // perceived screen-speed stays roughly constant.
        [SerializeField]
        private float panSpeed = 10.0f;
        [SerializeField]
        private float panZoomScalingRef = 10.0f;
        [SerializeField]
        private float panSmoothTime = 0.1f;

        // Zoom tuning. zoomSpeed default is small because the new Input System reports
        // raw mouse-scroll deltas (often ~120 per notch on Windows); tune in inspector.
        [SerializeField]
        private float zoomSpeed = 0.05f;
        [SerializeField]
        private float zoomMin = 3.0f;
        [SerializeField]
        private float zoomMax = 20.0f;
        [SerializeField]
        private float zoomSmoothTime = 0.1f;

        // Rotation tuning — slightly longer smooth time so a 90° turn reads visually.
        [SerializeField]
        private float rotationSmoothTime = 0.2f;

        // Edge-pan tuning. Off by default — can feel intrusive during normal UI interaction.
        // When enabled, moving the cursor within edgePanMargin pixels of any screen edge
        // pans the camera in that direction at edgePanSpeed world-units per second.
        [SerializeField]
        private bool edgePanEnabled = false;
        [SerializeField]
        private float edgePanMargin = 20.0f;
        [SerializeField]
        private float edgePanSpeed = 10.0f;

        public Transform CameraRig { get { return cameraRig; } }
        public Transform CameraPivot { get { return cameraPivot; } }
        public UnityEngine.Camera MainCamera { get { return mainCamera; } }

        // Input below this magnitude/value is treated as no input (a small deadzone that
        // ignores float noise on the analog/scroll axes).
        private const float INPUT_DEADZONE = 0.0001f;
        // Degrees rotated per Q/E press.
        private const float ROTATION_STEP_DEGREES = 90.0f;

        // The generated Input System wrapper class (from IA_CameraInput.inputactions).
        private CameraInputActions input;

        // Target state — what the player has asked for. SmoothDamp eases toward these.
        private Vector3 targetRigPosition;
        private float targetZoom;
        private float targetYRotation;

        // The current animated Y rotation (kept separately so we don't read back from
        // transform.eulerAngles, which can flip representations across the 0/360 wrap).
        private float currentYRotation;

        // SmoothDamp internal velocity buffers (it stores acceleration state in these refs).
        private Vector3 panVelocity;
        private float zoomVelocity;
        private float rotationVelocity;

        // Middle-mouse drag state.
        private bool isDragging;
        private Vector2 lastPointerPosition;

        // Auto-computed XZ bounds derived from the GridSystem in Start. Target rig
        // positions are clamped to this rect each frame so the rig cannot leave the
        // facility area regardless of which input drove the pan.
        private Rect panBounds;

        /// <summary>
        /// Instantiates the generated InputActions wrapper.
        /// </summary>
        private void Awake()
        {
            input = new CameraInputActions();
        }

        /// <summary>
        /// Activates the Camera action map and subscribes to event-driven actions.
        /// </summary>
        private void OnEnable()
        {
            input.Camera.Enable();
            // Discrete actions hook via events. Continuous actions (Pan, Zoom,
            // PointerPosition) are read each frame in Update — no hook needed.
            input.Camera.RotateLeft.performed += OnRotateLeft;
            input.Camera.RotateRight.performed += OnRotateRight;
            input.Camera.Drag.started += OnDragStarted;
            input.Camera.Drag.canceled += OnDragCanceled;
        }

        /// <summary>
        /// Unsubscribes and disables the action map (mirror of OnEnable).
        /// </summary>
        private void OnDisable()
        {
            input.Camera.RotateLeft.performed -= OnRotateLeft;
            input.Camera.RotateRight.performed -= OnRotateRight;
            input.Camera.Drag.started -= OnDragStarted;
            input.Camera.Drag.canceled -= OnDragCanceled;
            input.Camera.Disable();
        }

        /// <summary>
        /// Snapshots initial transform state into targets and computes pan bounds.
        /// </summary>
        private void Start()
        {
            // Seed targets to current values so the first frame's SmoothDamp has
            // nothing to ease toward (no startup jolt). Start runs after all Awakes,
            // so inspector references and the GridSystem grid are guaranteed ready.
            if (cameraRig != null)
            {
                targetRigPosition = cameraRig.position;
            }
            else
            {
                Debug.LogError("[IsometricCameraController] cameraRig reference is missing — assign one in the inspector.", this);
            }

            if (mainCamera != null)
            {
                targetZoom = mainCamera.orthographicSize;
            }
            else
            {
                Debug.LogError("[IsometricCameraController] mainCamera reference is missing — assign one in the inspector.", this);
            }

            if (cameraPivot != null)
            {
                currentYRotation = cameraPivot.localEulerAngles.y;
                targetYRotation = currentYRotation;
            }
            else
            {
                Debug.LogError("[IsometricCameraController] cameraPivot reference is missing — assign one in the inspector.", this);
            }

            ComputePanBounds();
        }

        /// <summary>
        /// Read input → clamp targets to bounds → smooth toward targets, once per frame.
        /// </summary>
        private void Update()
        {
            // Clamping before smoothing means the rig eases to a valid position,
            // not into the wall and back.
            HandleKeyboardPan();
            HandleMouseDrag();
            HandleEdgePan();
            HandleZoom();
            ClampTargetToBounds();
            ApplySmoothing();
        }

        /// <summary>
        /// Builds the pan-bounds rect from the GridSystem's world extent.
        /// </summary>
        private void ComputePanBounds()
        {
            if (gridSystem != null)
            {
                // Rect (x, y, width, height) where the Rect's "y" is our world Z axis.
                // Called once in Start; if grid size changes at runtime, call again.
                Vector3 gridOrigin = gridSystem.Origin;
                float worldWidth = gridSystem.Width * gridSystem.CellSize;
                float worldHeight = gridSystem.Height * gridSystem.CellSize;
                panBounds = new Rect(gridOrigin.x, gridOrigin.z, worldWidth, worldHeight);
            }
        }

        /// <summary>
        /// Reads the Pan vector and adds a camera-relative delta to targetRigPosition.
        /// </summary>
        private void HandleKeyboardPan()
        {
            Vector2 panInput = input.Camera.Pan.ReadValue<Vector2>();

            if (panInput.sqrMagnitude > INPUT_DEADZONE)
            {
                // Build a local-space direction. Input.x = right/left, input.y = forward/back.
                Vector3 localDirection = new Vector3(panInput.x, 0.0f, panInput.y);

                // Rotate by the pivot's TARGET rotation (not current) so panning direction
                // stays stable during the rotation tween. Using current would jitter.
                Quaternion pivotRotation = Quaternion.Euler(0.0f, targetYRotation, 0.0f);
                Vector3 worldDirection = pivotRotation * localDirection;

                // Scale pan speed with zoom: zoomed out = larger world steps per second.
                // Keeps "pixels moved per second" roughly constant on-screen.
                float zoomScale = mainCamera.orthographicSize / panZoomScalingRef;
                targetRigPosition += worldDirection * panSpeed * zoomScale * Time.deltaTime;
            }
        }

        /// <summary>
        /// While middle-mouse is held, drags the rig so the world follows the cursor.
        /// </summary>
        private void HandleMouseDrag()
        {
            if (isDragging)
            {
                Vector2 pointerPos = input.Camera.PointerPosition.ReadValue<Vector2>();
                Vector2 screenDelta = pointerPos - lastPointerPosition;
                lastPointerPosition = pointerPos;

                if (screenDelta.sqrMagnitude > INPUT_DEADZONE)
                {
                    // For an orthographic camera, the full vertical world height is
                    // (2 * orthographicSize). Divide by screen height to get world
                    // units per pixel. Horizontal works out to the same ratio because
                    // Unity orthographic uses the screen aspect ratio symmetrically.
                    float worldPerPixel = (mainCamera.orthographicSize * 2.0f) / Screen.height;

                    // Negate: cursor moves right → camera moves left so world appears
                    // dragged right. Build delta in local (camera-relative) space first,
                    // then rotate to world space by the pivot's Y rotation.
                    Vector3 localDelta = new Vector3(-screenDelta.x * worldPerPixel, 0.0f, -screenDelta.y * worldPerPixel);
                    Quaternion pivotRotation = Quaternion.Euler(0.0f, targetYRotation, 0.0f);
                    Vector3 worldDelta = pivotRotation * localDelta;
                    targetRigPosition += worldDelta;
                }
            }
        }

        /// <summary>
        /// Pans the camera when the cursor sits near a screen edge (only when edgePanEnabled is true).
        /// </summary>
        private void HandleEdgePan()
        {
            if (edgePanEnabled)
            {
                // Pointer position is in screen pixels: (0, 0) at bottom-left,
                // (Screen.width, Screen.height) at top-right.
                Vector2 pointerPos = input.Camera.PointerPosition.ReadValue<Vector2>();

                if (IsCursorOnScreen(pointerPos))
                {
                    Vector2 edgeInput = GetEdgePanDirection(pointerPos);

                    // sqrMagnitude > 0 means at least one axis hit an edge. In a corner
                    // both are non-zero — a diagonal pan, which falls out automatically
                    // because each axis is handled independently.
                    if (edgeInput.sqrMagnitude > INPUT_DEADZONE)
                    {
                        ApplyEdgePan(edgeInput);
                    }
                }
            }
        }

        /// <summary>
        /// True when the cursor sits inside the game window. Guards against an alt-tabbed
        /// user returning to a camera that drifted while the OS reported an off-screen cursor.
        /// </summary>
        private bool IsCursorOnScreen(Vector2 pointerPos)
        {
            return pointerPos.x >= 0.0f
                && pointerPos.x <= Screen.width
                && pointerPos.y >= 0.0f
                && pointerPos.y <= Screen.height;
        }

        /// <summary>
        /// Returns a -1/0/1 per-axis vector marking which edge(s) the cursor is hugging.
        /// Same shape as the WASD Pan action so it feeds straight into the pan math.
        /// </summary>
        private Vector2 GetEdgePanDirection(Vector2 pointerPos)
        {
            Vector2 edgeInput = Vector2.zero;

            // X axis: cursor near left edge → pan left (-1). Right edge → +1.
            if (pointerPos.x <= edgePanMargin)
            {
                edgeInput.x = -1.0f;
            }
            else if (pointerPos.x >= Screen.width - edgePanMargin)
            {
                edgeInput.x = 1.0f;
            }

            // Y axis: cursor near bottom edge → pan -Z. Top edge → +Z.
            // Screen Y maps to world Z because we're looking down at the XZ plane.
            if (pointerPos.y <= edgePanMargin)
            {
                edgeInput.y = -1.0f;
            }
            else if (pointerPos.y >= Screen.height - edgePanMargin)
            {
                edgeInput.y = 1.0f;
            }

            return edgeInput;
        }

        /// <summary>
        /// Adds a camera-relative, zoom-scaled edge-pan delta to the target rig position.
        /// </summary>
        private void ApplyEdgePan(Vector2 edgeInput)
        {
            // Same camera-relative pattern as HandleKeyboardPan:
            //   1. Treat input as a local-space direction (x = right, z = forward).
            //   2. Rotate by the pivot's TARGET Y rotation so direction follows
            //      the current view, not the world axes.
            //   3. Scale by zoom so pan speed stays perceptually consistent.
            Vector3 localDirection = new Vector3(edgeInput.x, 0.0f, edgeInput.y);
            Quaternion pivotRotation = Quaternion.Euler(0.0f, targetYRotation, 0.0f);
            Vector3 worldDirection = pivotRotation * localDirection;
            float zoomScale = mainCamera.orthographicSize / panZoomScalingRef;
            targetRigPosition += worldDirection * edgePanSpeed * zoomScale * Time.deltaTime;
        }

        /// <summary>
        /// Adjusts targetZoom by the scroll input, clamped to [zoomMin, zoomMax].
        /// </summary>
        private void HandleZoom()
        {
            float scrollInput = input.Camera.Zoom.ReadValue<float>();

            if (Mathf.Abs(scrollInput) > INPUT_DEADZONE)
            {
                // Subtraction inverts the sign: scrolling UP (positive) → zooms IN
                // (smaller ortho size). Clamping keeps zoom in a sensible range.
                targetZoom = Mathf.Clamp(targetZoom - scrollInput * zoomSpeed, zoomMin, zoomMax);
            }
        }

        /// <summary>
        /// Clamps targetRigPosition X/Z to panBounds (no-op without a GridSystem reference).
        /// </summary>
        private void ClampTargetToBounds()
        {
            if (gridSystem != null)
            {
                // Y is untouched — we don't restrict vertical placement.
                targetRigPosition.x = Mathf.Clamp(targetRigPosition.x, panBounds.xMin, panBounds.xMax);
                targetRigPosition.z = Mathf.Clamp(targetRigPosition.z, panBounds.yMin, panBounds.yMax);
            }
        }

        /// <summary>
        /// Eases current transform values toward their targets via SmoothDamp.
        /// </summary>
        private void ApplySmoothing()
        {
            // SmoothDamp is acceleration-based — accelerates toward the target and
            // decelerates as it nears, for a natural feeling motion curve. The
            // 'velocity' ref fields are SmoothDamp's internal state between calls;
            // never touch them directly.
            cameraRig.position = Vector3.SmoothDamp(cameraRig.position, targetRigPosition,
                                                    ref panVelocity, panSmoothTime);

            mainCamera.orthographicSize = Mathf.SmoothDamp(mainCamera.orthographicSize, targetZoom,
                                                            ref zoomVelocity, zoomSmoothTime);

            // SmoothDampAngle handles the 360°/0° wrap correctly: going from 350° to
            // 20° eases through 360°/0° (the short way), not the long way around.
            // Writing into localRotation (not rotation) keeps the cameraRig's own
            // rotation, if any, independent.
            currentYRotation = Mathf.SmoothDampAngle(currentYRotation, targetYRotation,
                                                     ref rotationVelocity, rotationSmoothTime);
            cameraPivot.localRotation = Quaternion.Euler(0.0f, currentYRotation, 0.0f);
        }

        /// <summary>
        /// Q → schedule a 90° left rotation of the world view.
        /// </summary>
        private void OnRotateLeft(InputAction.CallbackContext ctx)
        {
            // Q rotates the WORLD left. Camera spins clockwise around Y (+90°),
            // which from the player's view makes the world appear to swing left.
            targetYRotation += ROTATION_STEP_DEGREES;
        }

        /// <summary>
        /// E → schedule a 90° right rotation of the world view.
        /// </summary>
        private void OnRotateRight(InputAction.CallbackContext ctx)
        {
            targetYRotation -= ROTATION_STEP_DEGREES;
        }

        /// <summary>
        /// Middle-mouse pressed → start a drag and snapshot the cursor for delta tracking.
        /// </summary>
        private void OnDragStarted(InputAction.CallbackContext ctx)
        {
            lastPointerPosition = input.Camera.PointerPosition.ReadValue<Vector2>();
            isDragging = true;
        }

        /// <summary>
        /// Middle-mouse released → stop the drag.
        /// </summary>
        private void OnDragCanceled(InputAction.CallbackContext ctx)
        {
            isDragging = false;
        }

        /// <summary>
        /// Moves the camera target to the X/Z of worldPosition (smoothed via SmoothDamp).
        /// </summary>
        public void FocusOn(Vector3 worldPosition)
        {
            // Preserve current Y — we only reposition in the XZ plane. Smoothing
            // happens naturally in ApplySmoothing on subsequent frames.
            targetRigPosition = new Vector3(worldPosition.x, targetRigPosition.y, worldPosition.z);
        }

        /// <summary>
        /// Draws the pan-bounds rect as a cyan wire frame at grid ground level.
        /// </summary>
        private void OnDrawGizmos()
        {
            // Compute bounds from gridSystem each call so it works in edit mode
            // (before Start populates the cached panBounds).
            if (gridSystem != null)
            {
                Gizmos.color = Color.cyan;
                Vector3 gridOrigin = gridSystem.Origin;
                float worldWidth = gridSystem.Width * gridSystem.CellSize;
                float worldHeight = gridSystem.Height * gridSystem.CellSize;

                Vector3 corner00 = gridOrigin;
                Vector3 corner10 = gridOrigin + new Vector3(worldWidth, 0.0f, 0.0f);
                Vector3 corner11 = gridOrigin + new Vector3(worldWidth, 0.0f, worldHeight);
                Vector3 corner01 = gridOrigin + new Vector3(0.0f, 0.0f, worldHeight);

                Gizmos.DrawLine(corner00, corner10);
                Gizmos.DrawLine(corner10, corner11);
                Gizmos.DrawLine(corner11, corner01);
                Gizmos.DrawLine(corner01, corner00);
            }
        }
    }
}
