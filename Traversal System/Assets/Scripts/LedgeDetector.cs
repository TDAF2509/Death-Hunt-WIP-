using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LedgeDetector : MonoBehaviour
{
    public float distancePlayerToLedge;

    private Vector3 ledgePos;
    public Vector3 ledgeNormal;


    [SerializeField] float grabbingDistance = 0.3f;

    private Transform playerFace;
    private Transform playerSpine;

    public bool hanging = false;
    public bool ledgeFound;
    bool aboveLedgeCheck;
    bool ledgeCheck;
    public bool noWallLeft;
    public bool noWallRight;
    GameObject ledgeDetector;
    GameObject ledgePosIndicator;

    private Vector3 raycastFloorPos;

    private Vector3 CombinedRaycastNormal;

    private Vector3 cornerNormal;

    private Vector3 cornerPoint;

    private Quaternion turnToNormal;

    public float grabOffset = 0.55f;

    public Transform ledgeTransform;
    
    Animator anim;
    CharacterMovement charM;

    string currentMoveState;

    [Space]

    [Header("Scanner Settings")]
    [SerializeField] private float startHeight;
    [SerializeField] private float scanRange = 1f;
    [SerializeField] private LayerMask hitMask;
    [SerializeField] private LayerMask ledgeMask;
    private float scanPointDist = Mathf.Infinity;
    private float wallScanPointDist = Mathf.Infinity;
    private float targetLedgeDist = Mathf.Infinity;
    public Vector3 scanPointHit;
    public Vector3 scanPointNormal;
    public Vector3 scanPointOrigin;
    public Vector3 targetLedgePoint;
    public Vector3 targetLedgeNormal;
    public Vector3 wallScanPointHit;
    public Vector3 wallScanPointNormal;
    private Vector3 ledgeDepthCheckOrigin;
    public Vector3 ledgeDepthCheckHit;
    private Vector3 platformDepthCheckOrigin;
    public Vector3 platformDepthCheckHit;
    private Vector3 ledgeTopPoint;
    private Vector3 raypoint;
    private Vector3 wallLeft;
    private Vector3 wallRight;
    private bool wallLeftCheck;
    private bool wallRightCheck;
    private bool rightCornerCheck;
    private bool leftCornerCheck;
    private RaycastHit hit, lowClimbWallHit, highClimbWallHit, ledgeDepthHit, platformDepthHit, ledgeEdgeHitLeft, ledgeEdgeHitRight, leftRayHit, rightRayHit , ledgeHit;
    public bool canVaultOver;
    Ray ray;
    // Use this for initialization
    void Start()
    {
        charM = transform.GetComponent<CharacterMovement>();
        anim = GetComponent<Animator>();
        playerFace = anim.GetBoneTransform(HumanBodyBones.Head); // these are from the humanoid rig
        playerSpine = anim.GetBoneTransform(HumanBodyBones.Spine);
    }

    // Update is called once per frame
    void Update()
    {
        ledgeTopPoint = new Vector3(scanPointHit.x, scanPointHit.y + .2f, scanPointHit.z);
        Scan();
        LedgeDepthCheck();
        if (charM.IsGrounded())
        {
            PlatformDepthCheck();
        }
        

        scanPointDist = Mathf.Infinity;
        wallScanPointDist = Mathf.Infinity;

        if (hanging)
        {
            WallScan();
        }


        distancePlayerToLedge = Vector3.Distance(playerFace.position, scanPointHit);
    }

    private void Scan()
    {

        if (charM.isGrounded)
        {
            startHeight = transform.position.y + 4f;
        }
        else
        {
            startHeight = playerFace.position.y + 0.2f;
        }
        ledgeFound = false;
        for (float i = startHeight; i > transform.position.y; i -= 0.2f)
        {
            
            raypoint = new Vector3(transform.position.x, i, transform.position.z);
            //Debug.DrawRay(raypoint, transform.forward * scanRange, Color.red);
            if (Physics.Raycast(raypoint, transform.forward, out hit, scanRange, hitMask))
            {
                
              //  Debug.DrawRay(raypoint, transform.forward * scanRange, Color.green);
                float newDist = Vector3.Distance(raypoint, hit.point);
                if (newDist < scanPointDist && !Mathf.Approximately(newDist, scanPointDist))
                {
                    scanPointDist = hit.distance;
                    scanPointHit = hit.point;
                    scanPointNormal = hit.normal;
                    scanPointOrigin = transform.position;
                    scanPointOrigin.y = raypoint.y;
                    ledgeTransform = hit.collider.transform;
                    //ledgePos = scanPointHit;
                    charM.wallNormal = scanPointNormal;
                    ledgeFound = true;
                }
            }
        }

        if (!ledgeFound)
        {
            StopHanging();
        }
    }

    public void WallScan()
    {
        wallRight = new Vector3(transform.position.x, ledgeDepthCheckHit.y - 0.05f, transform.position.z) + transform.right*0.5f;
        wallLeft = new Vector3(transform.position.x, ledgeDepthCheckHit.y - 0.05f, transform.position.z) - transform.right*0.5f;
        wallLeftCheck = Physics.SphereCast(wallLeft, 0.1f, transform.forward*2f, out ledgeEdgeHitLeft, 1f, ledgeMask);
        wallRightCheck = Physics.SphereCast(wallRight, 0.1f, transform.forward*2f, out ledgeEdgeHitRight, 1f, ledgeMask);
        leftCornerCheck = Physics.SphereCast(wallLeft + transform.forward ,0.1f, transform.right, out leftRayHit, 0.4f, ledgeMask);
        rightCornerCheck = Physics.SphereCast(wallRight + transform.forward ,0.1f, -transform.right, out rightRayHit, 0.4f, ledgeMask);

        if (charM.horizontal < 0 && (!wallLeftCheck || ledgeTransform.gameObject.layer != ledgeEdgeHitLeft.collider.gameObject.layer && wallLeftCheck) && !leftCornerCheck)
        {
            charM.moveDirection.x = 0;
            charM.moveDirection.z = 0;
            charM.moveDirection.y = 0;
        }
        else
        {
            noWallLeft = false;
        }

        if (charM.horizontal > 0 && (!wallRightCheck || ledgeTransform.gameObject.layer != ledgeEdgeHitRight.collider.gameObject.layer && wallRightCheck) && !rightCornerCheck)
        {
            charM.moveDirection.x = 0;
            charM.moveDirection.z = 0;
            charM.moveDirection.y = 0;
        }

        if (wallRightCheck)
        {
            noWallRight = false;
        }
        else
        {
            noWallRight = true;
        }

        if (wallLeftCheck)
        {
            noWallLeft = false;
        }
        else
        {
            noWallLeft = true;
        }
        

        if (noWallRight && rightCornerCheck)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, rightRayHit.transform.rotation, 1);
            Vector3 LedgeTopPosition = new Vector3(rightRayHit.point.x, rightRayHit.point.y - 1.8f, rightRayHit.point.z);
            transform.position = Vector3.Slerp(transform.position, LedgeTopPosition, 1);
            scanPointHit = rightRayHit.point;
            charM.wallNormal = rightRayHit.normal;
            noWallRight = false;

        }

        if (noWallLeft && leftCornerCheck)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, leftRayHit.transform.rotation, 1);
            Vector3 LedgeTopPosition = new Vector3(leftRayHit.point.x, leftRayHit.point.y - 1.8f, leftRayHit.point.z);
            transform.position = Vector3.Slerp(transform.position, LedgeTopPosition, 1);
            scanPointHit = leftRayHit.point;
            charM.wallNormal = leftRayHit.normal;
            noWallLeft = false;
        }

        if (hanging && Vector3.Distance(playerFace.position, ledgeDepthCheckHit) > 0.3f)
        {

            if (playerFace.position.y < ledgeDepthCheckHit.y)
            {
                charM.moveDirection.y += 0.2f;
            }

            if (playerFace.position.y > ledgeDepthCheckHit.y)
            {
                charM.moveDirection.y -= 0.2f;
            }

            if((charM.vertical<0 || charM.vertical>0) && charM.horizontal<0.5 && charM.horizontal > -0.5)
            {
                charM.moveDirection.y = 0;
            }
        }



    }


    void LedgeDepthCheck()
    {
        ledgeDepthCheckOrigin = new Vector3(scanPointHit.x, scanPointHit.y + 1.75f, scanPointHit.z) + transform.forward * 0.1f;
        bool depthHit = Physics.Raycast(ledgeDepthCheckOrigin, -transform.up, out ledgeDepthHit, 2f, hitMask);
        if (depthHit)
        {
            ledgeDepthCheckHit = ledgeDepthHit.point;
            ledgeNormal = ledgeDepthHit.normal;
            ledgePos = new Vector3(scanPointHit.x, ledgeDepthCheckHit.y, scanPointHit.z);
        }
        else
        {
            if (ledgePos != Vector3.zero)
            {
                ledgePos = Vector3.zero;
            }
        }
        

        ray.origin = ledgeTopPoint;
        ray.direction = -transform.up;

        ledgeCheck = Physics.Raycast(ray, out RaycastHit here, 1, hitMask);



        if (distancePlayerToLedge < grabbingDistance && ledgeNormal.y > 0.9f && ledgeNormal.y < 1.1f && ledgePos.y>playerFace.position.y)
        {

            // if not already hanging, grab
            if (!hanging)
            {
                hanging = true;
                charM.GrabLedgePos(ledgePos);
            }
        }
    }

    void PlatformDepthCheck()
    {
        platformDepthCheckOrigin = new Vector3(scanPointHit.x, scanPointHit.y + 1.75f, scanPointHit.z) + transform.forward * 1.5f;

        if (Physics.Raycast(platformDepthCheckOrigin, -transform.up,out platformDepthHit, 2f, hitMask))
        {
            platformDepthCheckHit = platformDepthHit.point;

            if (scanPointHit.y<playerFace.position.y && scanPointHit.y>=playerSpine.position.y && scanPointDist<0.9f && !hanging && charM.isGrounded && !aboveLedgeCheck &&(charM.isRunning || charM.jump_input_down) )
            {
                charM.isGrounded = true;
                charM.Vault("Mount", true);
            }else if (scanPointHit.y<playerSpine.position.y && scanPointDist < 0.9f && !hanging && charM.isGrounded && !aboveLedgeCheck && charM.isRunning)
            {
                charM.isGrounded = true;
                charM.Vault("Low Mount", true);
            }
        }
        else
        {
            if (scanPointHit.y <= playerSpine.position.y && scanPointDist < 0.9f && !hanging && charM.isRunning && platformDepthCheckHit.y<playerSpine.position.y)
            {
                Debug.Log("Vault ATTEMPT 1");
                charM.Vault("Vault", false);
            }
        }
    }


    void FindSuitablePlaceForClimbup()
    {
        
        if (distancePlayerToLedge < grabbingDistance + 0.4f )
        {
            charM.safeForClimbUp = true;
            // update the ledgeposition so it's in the correct place when moving
            charM.ledgePos = ledgePos;
            ledgePosIndicator.transform.position = ledgePos;
        }
        else
        {
            charM.safeForClimbUp = false;
        }
    }


    public void StopHanging()
    {
        // only drop when not currently climbing up or we get stuck
        if (hanging && !anim.GetCurrentAnimatorStateInfo(0).IsName("ClimbUp"))
        {
            hanging = false;
            charM.CancelHanging();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(scanPointOrigin, scanPointHit);
        Gizmos.DrawSphere(scanPointHit, 0.1f);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(ledgeDepthCheckOrigin, ledgeDepthCheckHit);
        Gizmos.DrawSphere(ledgeDepthCheckHit, 0.05f);
        Gizmos.DrawLine(platformDepthCheckOrigin, platformDepthCheckHit);
        Gizmos.DrawSphere(platformDepthCheckHit, 0.05f);

        Gizmos.color = Color.gray;
        Gizmos.DrawRay(ledgeTopPoint, -transform.up*1f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(wallLeft, transform.forward);
        Gizmos.DrawSphere(ledgeEdgeHitLeft.point, 0.1f);
        Gizmos.DrawRay(wallRight, transform.forward);
        Gizmos.DrawSphere(ledgeEdgeHitRight.point, 0.1f);
        Gizmos.DrawRay(wallLeft + transform.forward * 2f, transform.right);
        Gizmos.DrawRay(wallRight + transform.forward * 2f, -transform.right);
        Gizmos.DrawSphere(leftRayHit.point, 0.05f);
        Gizmos.DrawSphere(rightRayHit.point, 0.05f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(targetLedgePoint, 0.05f);
        Gizmos.DrawRay(targetLedgePoint,targetLedgeNormal);
    }

}
