using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

public class CharacterMovement : MonoBehaviour
{
    private PlayerInputManager playerInputManager;
    private InputAction move;
    private InputAction jump;
    private InputAction drop;
    private InputAction run;

    public float floorOffsetY;
    public float moveSpeed = 6f;
    public float rotateSpeed = 10f;
    public float slopeLimit = 45f;
    public float slopeInfluence = 5f;
    public float jumpPower = 10f;
    public int maxJumps = 2;
    public int remainingJumps;
    public int maxDash = 1;
    public int remainingDash;
    public bool isDashing;
    public float characterHangOffset = 1.4f;
    public bool isRunning = false;
    public bool canWallJump = false;

    private Vector3 lastMoveVelocity;
    private Vector3 groundedVelocity;

    ContactPoint contactPoint;

    public bool isWallRunning = false;
    public float wallRunDuration = 4f;
    public float upForce = 15f;
    public float constantUpForce = 10;
    public float wallForce = 4000;
    public float forwardForce = 12;
    public bool isWallLeft;
    public bool isWallRight;
    private Vector3 wallDir;
    public static CharacterMovement instance;
    private bool isCancellingWallRunning;
    string currentMoveState;

    public Volume volume;
    private MotionBlur motionBlur;
    private LensDistortion lensDistortion;
    public bool maxBlur;
    public bool maxLens;

    [SerializeField] private Rigidbody rb;
    [SerializeField] private Animator anim;
    [SerializeField] public float vertical;
    [SerializeField] public float horizontal;

    public Vector3 moveDirection;
    [SerializeField] private float inputAmount;
    [SerializeField] private Vector3 raycastFloorPos;
    [SerializeField] private Vector3 floorMovement;
    [SerializeField] private Vector3 gravity;
    [SerializeField] private Vector3 CombinedRaycast;

    float jumpFalloff = 2f;
    public bool jump_input_down;
    public bool drop_input_down;
    float slopeAmount;
    Vector3 floorNormal;

    [SerializeField] private Vector3 velocity;
    public Vector3 combinedInput;

    public bool safeForClimbUp;
    public bool isVaulting;

    [Header("Ledge Climbing")]
    LedgeDetector ledgeDetector;
    [SerializeField] private float grabbingDistance = 0.3f;
    public bool grabLedge;
    public Vector3 ledgePos;
    public Vector3 wallNormal;
    public Vector3 scanHitPos;
    public bool lockZMovement;

    [Space]

    [Header("Grounded Settings")]
    public bool isGrounded = true;
    [SerializeField] bool isOnEdge = false;
    [SerializeField] float GroundOffset = -0.14f;
    [SerializeField] float GroundedRadius = 0.28f;
    [SerializeField] LayerMask GroundLayers;
    private Vector3 groundCheckOrigin;
    private Vector3 groundCheckHit;
    private float groundCheckDist;

   

    Coroutine vault;

    public enum MovementControlType {Normal,Climbing,WallRunning };

    [SerializeField] MovementControlType movementControlType;

    // Use this for initialization
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();
        ledgeDetector = GetComponent<LedgeDetector>();
        motionBlur = FindObjectOfType<MotionBlur>();
        lensDistortion = FindObjectOfType<LensDistortion>();
        instance = this;
        wallDir = Vector3.up;
        remainingJumps = maxJumps;
        remainingDash = maxDash;
    }

    private void Awake()
    {
        playerInputManager = new PlayerInputManager();
    }

    private void OnEnable()
    {
        move = playerInputManager.Movement.Move;
        move.Enable();
        jump = playerInputManager.Movement.Jump;
        jump.Enable();
        drop = playerInputManager.Movement.Drop;
        drop.Enable();
        run = playerInputManager.Movement.Run;
        run.Enable();

        run.performed += Run_performed;
        run.canceled += Run_canceled;
    }

    private void Run_performed(InputAction.CallbackContext obj)
    {
        isRunning = true;
    }
    private void Run_canceled(InputAction.CallbackContext obj)
    {
        isRunning = false;
    }

    

    private void OnDisable()
    {
        move.Disable();
        jump.Disable();
        drop.Disable();
        run.Disable();
    }
    private void Update()
    {
        // reset movement
        moveDirection = Vector3.zero;
        // get vertical and horizontal movement input (controller and WASD/ Arrow Keys)
        vertical = move.ReadValue<Vector2>().y;
        horizontal = move.ReadValue<Vector2>().x;

        volume.profile.TryGet(out motionBlur);
        volume.profile.TryGet(out lensDistortion);

        jump_input_down = jump.triggered;
        drop_input_down = drop.triggered;


        // base movement on camera
        Vector3 correctedVertical = vertical * Camera.main.transform.forward;
        Vector3 correctedHorizontal = horizontal * Camera.main.transform.right;

        combinedInput = correctedHorizontal + correctedVertical;
        // normalize so diagonal movement isnt twice as fast, clear the Y so your character doesnt try to
        // walk into the floor/ sky when your camera isn't level

        moveDirection = new Vector3((combinedInput).normalized.x, 0, (combinedInput).normalized.z);

        // make sure the input doesnt go negative or above 1;
        float inputMagnitude = Mathf.Abs(horizontal) + Mathf.Abs(vertical);
        inputAmount = Mathf.Clamp01(inputMagnitude);

        if (moveDirection != Vector3.zero)
        {
            // rotate player to movement direction
            Quaternion rot = Quaternion.LookRotation(moveDirection);
            Quaternion targetRotation = Quaternion.Slerp(transform.rotation, rot, Time.fixedDeltaTime * inputAmount * rotateSpeed);
            transform.rotation = targetRotation;
        }
        

        if (jump_input_down)
        {
            Jump();
        }
        else
        {
            if (lensDistortion.intensity.value < -0.0f && motionBlur.intensity.value > 0.0)
            {
                lensDistortion.intensity.value += 0.01f;
                motionBlur.intensity.value -= 0.01f;
            }
            else
            {
                maxLens = false;
                lensDistortion.intensity.value = -0.0f;
                motionBlur.intensity.value = 0.0f;
            }
        }
        

        // if hanging and cancel input is pressed, back to normal movement, and stop hanging
        if (drop_input_down && grabLedge)
        {
            CancelHanging();
        }

        // handle animation blendtree for walking
        anim.SetFloat("Velocity", inputAmount, 0.2f, Time.deltaTime);
        anim.SetFloat("SlopeNormal", slopeAmount, 0.2f, Time.deltaTime);
        anim.SetFloat("HangingVelocity", horizontal);

        
        Debug.DrawRay(transform.position, transform.right * 2f,Color.red);
        Debug.DrawRay(transform.position, -transform.right * 2f, Color.red);
        
        if (Physics.Raycast(transform.position,transform.right,2f))
        {
            Debug.DrawRay(transform.position, transform.right * 2f, Color.green);
            isWallRight = true;
            isWallLeft = false;
            WallRunning();

        }
        else if (Physics.Raycast(transform.position, -transform.right, 2f))
        {
            Debug.DrawRay(transform.position, -transform.right * 2f, Color.green);
            isWallLeft = true;
            isWallRight = false;
            WallRunning();
        }
        else
        {
            isWallLeft = false;
            isWallRight = false;
            
        }
    }


    private void FixedUpdate()
    {
        // if not grounded , increase down force
        if ((!IsGrounded() || slopeAmount >= 0.1f) && !isVaulting)// if going down, also apply, to stop bouncing
        {
            gravity += Vector3.up * Physics.gravity.y * jumpFalloff * Time.fixedDeltaTime;
        }

        switch (movementControlType)
        {
            case MovementControlType.Normal:
                // normal movement

                if (moveDirection != Vector3.zero)
                {
                    Quaternion rot = Quaternion.LookRotation(moveDirection);
                    Quaternion targetRotation = Quaternion.Slerp(transform.rotation, rot, Time.fixedDeltaTime * inputAmount * rotateSpeed);
                    transform.rotation = targetRotation;
                }

                
                break;

            case MovementControlType.Climbing:

                

                // movement for climbing, going up and down/ left and right instead of forward and sideways like normal movements
                moveDirection = new Vector3(horizontal * 0.2f * inputAmount * transform.forward.z, vertical * 0.3f * inputAmount*transform.up.y, -horizontal * 0.2f * inputAmount * transform.forward.x) + (transform.forward * 0.2f);

                // if we are hanging on a ledge, don't move up or down
                if (grabLedge)
                {
                    Debug.DrawRay(transform.position + transform.up, moveDirection*5f);
                }

                // no gravity when climbing
                gravity = Vector3.zero;

                if (moveDirection != Vector3.zero)
                {
                    // rotate to the wall
                    Quaternion rotateToWall = Quaternion.LookRotation(-wallNormal);
                    Quaternion climbTargetRotation = Quaternion.Slerp(transform.rotation, rotateToWall, Time.deltaTime * rotateSpeed * 90);
                    transform.rotation = climbTargetRotation;

                    // rotate towards the wall while moving, so you always face the wall even when its curved
                    Vector3 targetDir = -wallNormal;
                    targetDir.y = 0;

                    if (targetDir == Vector3.zero)
                    {
                        targetDir = transform.forward;
                    }

                    Quaternion tr = Quaternion.LookRotation(targetDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, tr, Time.fixedDeltaTime * inputAmount * rotateSpeed);
                }

                ledgeDetector.WallScan();

                break;

            case MovementControlType.WallRunning:

                // movement for climbing, going up and down/ left and right instead of forward and sideways like normal movements
                moveDirection = new Vector3(0, 0, vertical * 0.2f * inputAmount * transform.forward.z) + (transform.forward * 1f);

                gravity = Vector3.zero;

                

                break;

        }

        

        // actual movement of the rigidbody + extra down force
        rb.velocity = (moveDirection * GetMoveSpeed() * inputAmount) + gravity;

        // find the Y position via raycasts
        floorMovement = new Vector3(rb.position.x, FindFloor().y + floorOffsetY, rb.position.z);

        // only stick to floor when grounded
        if (floorMovement != rb.position && IsGrounded() && rb.velocity.y <= 0)
        {
            // move the rigidbody to the floor
            rb.MovePosition(floorMovement);
            gravity = Vector3.zero;
            movementControlType = MovementControlType.Normal;
            grabLedge = false;
        }

        // ledge grab only when not on ground
        if (!IsGrounded())
        {
            LedgeGrab();

            if (isDashing)
            {
                rb.AddForce(transform.forward * moveSpeed * 30f, ForceMode.VelocityChange);
                rb.AddForce(transform.up * vertical * 5f, ForceMode.VelocityChange);
                isDashing = false;
            }

        }

        velocity = rb.velocity;
    }

    Vector3 FindFloor()
    {
        // width of raycasts around the centre of your character
        float raycastWidth = 0.25f;
        // check floor on 5 raycasts   , get the average when not Vector3.zero  
        int floorAverage = 1;

        CombinedRaycast = FloorRaycasts(0, 0, 1.6f);
        floorAverage += (getFloorAverage(raycastWidth, 0) + getFloorAverage(-raycastWidth, 0) + getFloorAverage(0, raycastWidth) + getFloorAverage(0, -raycastWidth));

        return CombinedRaycast / floorAverage;
    }

    // only add to average floor position if its not Vector3.zero
    int getFloorAverage(float offsetx, float offsetz)
    {

        if (FloorRaycasts(offsetx, offsetz, 1.6f) != Vector3.zero)
        {
            CombinedRaycast += FloorRaycasts(offsetx, offsetz, 1.6f);
            return 1;
        }
        else { return 0; }
    }

    public bool IsGrounded()
    {
        if (FloorRaycasts(0, 0, 0.6f) != Vector3.zero)
        {
            slopeAmount = Vector3.Dot(transform.forward, floorNormal);
            remainingJumps = maxJumps;
            remainingDash = maxDash;
            anim.SetBool("isGrounded", true);
            anim.SetBool("isFalling", false);
            ledgeDetector.hanging = false;
            CancelHanging();
            return true;
        }
        else
        {
            anim.SetBool("isGrounded", false);
            anim.SetBool("isFalling", true);
            return false;
        }
    }


    Vector3 FloorRaycasts(float offsetx, float offsetz, float raycastLength)
    {
        RaycastHit hit;
        // move raycast
        raycastFloorPos = transform.TransformPoint(0 + offsetx, 0 + 0.5f, 0 + offsetz);

        Debug.DrawRay(raycastFloorPos, Vector3.down, Color.magenta);
        if (Physics.Raycast(raycastFloorPos, -Vector3.up, out hit, raycastLength))
        {
            floorNormal = hit.normal;

            if (Vector3.Angle(floorNormal, Vector3.up) < slopeLimit)
            {
                return hit.point;
            }
            else return Vector3.zero;
        }
        else return Vector3.zero;
    }

    float GetMoveSpeed()
    {
        // you can add a run here, if run button : currentMovespeed = runSpeed;
        float currentMovespeed = Mathf.Clamp(moveSpeed + (slopeAmount * slopeInfluence), 0, moveSpeed + 1);
        return currentMovespeed;
    }

    void Jump()
    {
        if (IsGrounded()|| remainingJumps>0 && !canWallJump)
        {
            gravity.y = jumpPower;
            anim.SetTrigger("Jump");
            remainingJumps--;
        }

        if (remainingJumps==0 && remainingDash>0 && !canWallJump)
        {
            isDashing = true;
            if (lensDistortion.intensity.value < -0.5f && motionBlur.intensity.value > 0.35)
            {
                lensDistortion.intensity.value += 0.01f;
                motionBlur.intensity.value -= 0.01f;
            }
            else
            {
                maxLens = false;
                lensDistortion.intensity.value = -0.5f;
                motionBlur.intensity.value = 0.35f;
            }
            anim.SetTrigger("AirDash");
            anim.ResetTrigger("Jump");
            remainingDash--;
        }

        if (safeForClimbUp && anim.GetCurrentAnimatorStateInfo(0).IsName("Hang"))
        {
            anim.SetTrigger("ClimbUp");
        }

    }

    public void StartWallRun()
    {
        if (!isWallRunning && moveDirection.magnitude > 0.1 && isRunning && !IsGrounded())
        {
            Debug.Log("START WALL RUN");
            anim.SetTrigger("WallRunStart");

            if (isWallRight)
            {
                anim.SetFloat("WallRun", 0);
            }
            else if (isWallLeft)
            {
                anim.SetFloat("WallRun", 1);
            }
        }

        movementControlType = MovementControlType.WallRunning;
    }

    public void WallRunning()
    {
        if (isWallRunning && isRunning && moveDirection.magnitude > 0.1f && !IsGrounded())
        {
            rb.AddForce(-wallDir * wallForce * Time.deltaTime);
            rb.AddForce(Vector3.up * constantUpForce * Time.deltaTime);

            //anim.ResetTrigger("WallRunStart");

            anim.SetBool("isWallRunning", true);

            if (isWallRight)
            {
                anim.SetFloat("WallRun", 0);
            }
            else if (isWallLeft)
            {
                anim.SetFloat("WallRun", 1);
            }

        }
        else
        {
            StopWallRun();
        }
    }

    private bool CheckSurfaceAngle(Vector3 v, float angle)
    {
        return Mathf.Abs(angle - Vector3.Angle(Vector3.up, v)) < 0.1f;
    }

    public void StopWallRun()
    {
        isWallRunning = false;
        anim.SetBool("isWallRunning", false);

        movementControlType = MovementControlType.Normal;
    }

    private void OnCollisionStay(Collision collision)
    {

        if (!IsGrounded() && (isWallLeft || isWallRight) && isRunning)
        {
            Vector3 surface = collision.contacts[0].normal;
            if (CheckSurfaceAngle(surface, 90))
            {
                StartWallRun();
                isWallRunning = true;
                wallDir = surface;

                isCancellingWallRunning = false;
                CancelInvoke("StopWallRun");
            }

            if (!isCancellingWallRunning)
            {
                isCancellingWallRunning = true;
                Invoke("StopWallRun", wallRunDuration * Time.deltaTime);
            }
        }
        
    }

    private void OnCollisionExit(Collision collision)
    {
        if ((movementControlType == MovementControlType.WallRunning))
        {
            if (isWallRunning)
            {
                StopWallRun();
            }
        }
    }

    public void CancelHanging()
    {
        movementControlType = MovementControlType.Normal;
        grabLedge = false;
        anim.SetTrigger("StopHanging");
    }

    public void GrabLedgePos(Vector3 ledgePos1)
    {
        // set hanging animation trigger
        anim.SetTrigger("Hang");
        anim.ResetTrigger("StopHanging");
        ledgePos = ledgePos1;
        grabLedge = true;
        // set movement type for climbing
        movementControlType = MovementControlType.Climbing;

    }

    void LedgeGrab()
    {
        if (anim.GetCurrentAnimatorStateInfo(0).IsName("ClimbUp"))
        {
            transform.position = Vector3.Lerp(transform.position, ledgePos + (transform.forward * 0.4f), Time.deltaTime * 5f);
        }

        if (anim.GetCurrentAnimatorStateInfo(0).IsName("Enter Hang"))
        {
            // lower position for hanging
            Vector3 LedgeTopPosition = new Vector3(ledgePos.x, ledgePos.y - characterHangOffset, ledgePos.z);

            // lerp the position
            transform.position = Vector3.Lerp(transform.position, LedgeTopPosition, Time.deltaTime * 5f);
            // rotate to the wall
            Quaternion rotateToWall = Quaternion.LookRotation(-wallNormal);
            Quaternion targetRotation = Quaternion.Slerp(transform.rotation, rotateToWall, Time.deltaTime * rotateSpeed);
            transform.rotation = targetRotation;
        }
    }
    public void Vault(string whichState, bool IsMounting)
    {

        currentMoveState = whichState;


        anim.SetBool("isVaulting", true);
        anim.SetTrigger(whichState);


        if (IsMounting)
        {
            rb.DOMove(ledgeDetector.scanPointHit + (transform.up * 0.1f) + (transform.forward * 0.75f), .65f);

            Coroutine climb = StartCoroutine(ClimbCoroutine());

            IEnumerator ClimbCoroutine()
            {
                yield return new WaitForSeconds(.65f);
                anim.ResetTrigger(currentMoveState);
                anim.SetBool("isVaulting", false);
            }
        }
        else
        {
            
            Debug.Log(currentMoveState);
            rb.DOMove(ledgeDetector.scanPointHit + (transform.up*0.5f) + (transform.forward * 0.75f), .65f);
            Debug.Log("Vault ATTEMPT 2");
            Coroutine vault = StartCoroutine(VaultCoroutine());

            IEnumerator VaultCoroutine()
            {
                yield return new WaitForSeconds(1f);
                anim.ResetTrigger(currentMoveState);
                anim.SetBool("isVaulting", false);
            }
        }

    }


    

}
