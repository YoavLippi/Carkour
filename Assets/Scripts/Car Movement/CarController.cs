using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class CarController : MonoBehaviour
{
    private DebugConsole debugConsole;
    [Header("Setup")] 
    [SerializeField] private WheelHandler wheelHandlerFr;
    [SerializeField] private WheelHandler wheelHandlerFl;
    [SerializeField] private WheelHandler wheelHandlerBr;
    [SerializeField] private WheelHandler wheelHandlerBl;
    [Header("Controls")]
    [SerializeField] private PlayerInput playerMovement;

    [Header("Physics")]
    //dont want to index, rather easier to just have explicit references to each
    //[SerializeField] private WheelCollider[] wheels;
    [SerializeField] private float gravityScale;
    [SerializeField] private BoxCollider carCollider;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private WheelCollider frWheel;
    [SerializeField] private WheelCollider flWheel;
    [SerializeField] private WheelCollider brWheel;
    [SerializeField] private WheelCollider blWheel;

    [SerializeField] private Transform orientationFixer;
    /*[SerializeField] private BoxCollider frWheel;
    [SerializeField] private BoxCollider flWheel;
    [SerializeField] private BoxCollider brWheel;
    [SerializeField] private BoxCollider blWheel;*/

    //[SerializeField] private float acceleration = 500f;
    [SerializeField] private AnimationCurve accelerationCurve;
    [SerializeField] private AnimationCurve breakingCurve;
    [SerializeField] private AnimationCurve turningCurve;
    [SerializeField] private AnimationCurve turnEffectorCurve;
    //While acceleration is constant, the maximum speed a car can reach is what is tempered by the throttle
    [SerializeField] private AnimationCurve velocityCapCurve;
    [SerializeField] private float turnRadius;
    [SerializeField] private float aerialTurnAcceleration;
    [SerializeField] private float maxAerialTurnSpeed;
    [SerializeField] private float softVelocityCap = 10f;
    [SerializeField] private float hardVelocityCap = 20f;
    [SerializeField] private float boostMultiplier = 1.1f;

    private float currentAcceleration;
    private float currentBreakForce;
    
    [SerializeField] private Vector2 moveDirection;

    [SerializeField] private bool isBoosting = false;
    [SerializeField] private bool isRolling = false;
    [SerializeField] private bool isRollingRight = false;

    public float SoftVelocityCap
    {
        get => softVelocityCap;
        set => softVelocityCap = value;
    }

    private void Start()
    {
        debugConsole = GameObject.FindWithTag("DebugLogger").GetComponent<DebugConsole>();
    }

    private void OnEnable()
    {
        playerMovement.actions.Enable();
    }

    private void OnDisable()
    {
        playerMovement.actions.Disable();
    }

    private void Update()
    {
        //moveDirection = playerMove.ReadValue<Vector2>();
    }
    
    private void FixedUpdate()
    {
        if (currentAcceleration == 0 && currentBreakForce == 0)
        {
            rb.AddForce(Physics.gravity*gravityScale, ForceMode.Acceleration);
        }
        Move();
        if (IsFullyGrounded())
        {
            DoTurn();
        }
        else
        {
            DoAerialMovement();
        }
        
        debugConsole.isPressingDirection = moveDirection.magnitude != 0;
        if (!IsPartiallyGrounded() && moveDirection.magnitude == 0 && !isRollingRight)
        {
            rb.angularVelocity = new Vector3(0,0,0);
        }
    }

    private void DoTurn()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);

        //Vector maths to get the turn vector
        float forwardVecUnscaled = MapFloat(0, hardVelocityCap, 0, 1, Mathf.Abs(localVelocity.z));
        Vector3 forwardVecScaled = rb.transform.TransformDirection(new Vector3(0,0,turnEffectorCurve.Evaluate(forwardVecUnscaled)));

        float turnFlipper;
        if (IsFullyGrounded())
        {
            turnFlipper = localVelocity.z >= 0 ? moveDirection.x : -moveDirection.x;
        }
        else
        {
            turnFlipper = moveDirection.x;
        }
        Vector3 turnVec = rb.transform.TransformDirection((new Vector3((turnFlipper/2), 0, 1)));
        Vector3 resultingTurn = IsFullyGrounded()? (forwardVecScaled + turnVec).normalized : turnVec.normalized;
        Debug.DrawLine(rb.position, rb.position + rb.transform.forward*10, Color.red);
        Debug.DrawLine(rb.position, rb.position + (resultingTurn*10), Color.magenta);
            
        //using turning curve to evaluate the rotation speed
        float turningSpeedTemp = IsFullyGrounded() ? turnRadius * turnEffectorCurve.Evaluate(forwardVecUnscaled) : turnRadius;
        
        //Getting the rotation direction based on the calculated vector
        Quaternion targetRotation = Quaternion.LookRotation(resultingTurn);
        //Smoothly interpolating
        rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, turningSpeedTemp * Time.deltaTime);
    }

    private void DoAerialMovement()
    {
        //yaw
        //rb.AddTorque(rb.transform.up*(aerialTurnAcceleration*moveDirection.x), ForceMode.Acceleration);
        
        //pitch
        rb.AddTorque(rb.transform.right*(aerialTurnAcceleration*moveDirection.y), ForceMode.Acceleration);
        
        //roll
        if (isRolling)
        {
            rb.AddTorque(-rb.transform.forward*(aerialTurnAcceleration*moveDirection.x), ForceMode.Acceleration);
        }
        else
        {
            rb.AddTorque(rb.transform.up*(aerialTurnAcceleration*moveDirection.x), ForceMode.Acceleration);
        }
        
        //air roll right specifically
        if (isRollingRight)
        {
            rb.AddTorque(-rb.transform.forward*(aerialTurnAcceleration*1), ForceMode.Acceleration);
        }
        /*rb.angularVelocity = new Vector3(
            Mathf.Clamp(rb.angularVelocity.x, -maxAerialTurnSpeed, maxAerialTurnSpeed), 
            Mathf.Clamp(rb.angularVelocity.y, -maxAerialTurnSpeed, maxAerialTurnSpeed),
            rb.angularVelocity.z);*/
        rb.angularVelocity = ClampVector(rb.angularVelocity, maxAerialTurnSpeed);
    }

    private Vector3 ClampVector(Vector3 vector, float maxMagnitude)
    {
        if (vector.magnitude > maxMagnitude)
        {
            vector = vector.normalized * maxMagnitude;
        }
        return vector;
    }

    private void Move()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);
        debugConsole.moveSpeed = localVelocity.magnitude;
        
        if (IsFullyGrounded() && !isBoosting)
        {

            /*
            //Vector maths to get the turn vector
            float forwardVecUnscaled = MapFloat(0, hardVelocityCap, 0, 1, Mathf.Abs(localVelocity.z));
            Vector3 forwardVecScaled = rb.transform.TransformDirection(new Vector3(0,0,turnEffectorCurve.Evaluate(forwardVecUnscaled)));

            float turnFlipper = localVelocity.z >= 0 ? moveDirection.x : -moveDirection.x;
            Vector3 turnVec = rb.transform.TransformDirection((new Vector3((turnFlipper/2), 0, 1)));
            Vector3 resultingTurn = (forwardVecScaled + turnVec).normalized;
            Debug.DrawLine(rb.position, rb.position + rb.transform.forward*10, Color.red);
            Debug.DrawLine(rb.position, rb.position + (resultingTurn*10), Color.magenta);
            
            //using turning curve to evaluate the rotation speed
            float turningSpeedTemp = 5.0f * turnEffectorCurve.Evaluate(forwardVecUnscaled);
            
            //Getting the rotation direction based on the calculated vector
            Quaternion targetRotation = Quaternion.LookRotation(resultingTurn);
            
            //Smoothly interpolating
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, turningSpeedTemp * Time.deltaTime);
            */

            rb.AddForce(orientationFixer.forward*currentAcceleration, ForceMode.Acceleration);
            rb.AddForce(-(orientationFixer.forward * currentBreakForce), ForceMode.Acceleration);
            
            /*int turnDirection = 1;
            if (moveDirection.x < 0)
            {
                turnDirection = -1;
            }

            int reverseDirection = 1;
            if (localVelocity.z < 0)
            {
                reverseDirection = -1;
            }
            //rb.AddTorque(rb.transform.up * (turnSpeed * moveDirection.x * turnFactor * turnDirection), ForceMode.Acceleration);
            float turnFactor = Mathf.Abs(moveDirection.x) * Mathf.Abs(localVelocity.z);
            float turnEvalFactor = MapFloat(0, hardVelocityCap, 0, 1, turnFactor);
            float currentTurnForce = turningCurve.Evaluate(turnEvalFactor);
            //rb.AddTorque(rb.transform.up * (currentTurnForce*turnDirection*reverseDirection), ForceMode.VelocityChange);*/
            
            //localVelocity.x = 0;
            //Debug.Log($"{localVelocity.z}");
            //Debug.Log($"Current acceleration: {currentAcceleration}, Current Break Force:{currentBreakForce}");
        }
        
        if (IsFullyGrounded())
        {
            debugConsole.isGrounded = true;
            localVelocity.x = 0;
        }
        else
        {
            debugConsole.isGrounded = false;
        }
        
        if (isBoosting)
        {
            rb.AddForce(orientationFixer.forward*(accelerationCurve.Evaluate(1)*boostMultiplier), ForceMode.Acceleration);
        }
        //Hard clamping all directional velocities
        localVelocity.x = Mathf.Clamp(localVelocity.x, -hardVelocityCap, hardVelocityCap);
        localVelocity.y = Mathf.Clamp(localVelocity.y, -hardVelocityCap, hardVelocityCap);
        localVelocity.z = Mathf.Clamp(localVelocity.z, -hardVelocityCap, hardVelocityCap);
        rb.velocity = transform.TransformDirection(localVelocity);
    }

    private void OnBoost()
    {
        float checker = playerMovement.actions.FindAction("Boost").ReadValue<float>();
        Debug.Log($"Boost {(checker >=0.5f ? "pressed":"released")}");
        isBoosting = (checker >= 0.5f);
    }

    private void OnMove()
    {
        moveDirection = playerMovement.actions.FindAction("Move").ReadValue<Vector2>();
        debugConsole.moveInput = moveDirection;
        Debug.Log($"Moving with {moveDirection}");
    }

    private void OnThrottle()
    {
        float accelAmount = playerMovement.actions.FindAction("Throttle").ReadValue<float>();
        Debug.Log($"Throttling with {accelAmount}");
        //currentAcceleration = MapFloat(0, 1, 0, acceleration, accelAmount);
        currentAcceleration = accelerationCurve.Evaluate(accelAmount);
        //using wheelcolliders
        /*frWheel.motorTorque = currentAcceleration;
        flWheel.motorTorque = currentAcceleration;
        blWheel.motorTorque = currentAcceleration;
        brWheel.motorTorque = currentAcceleration;*/
    }

    private void OnReverse()
    {
        float reverseAmount = playerMovement.actions.FindAction("Reverse").ReadValue<float>();
        Debug.Log($"Reversing with {reverseAmount}");
        //currentBreakForce = MapFloat(0, 1, 0, breakForce, reverseAmount);
        currentBreakForce = breakingCurve.Evaluate(reverseAmount);
        //using wheelcolliders
        /*frWheel.motorTorque = currentBreakForce;
        flWheel.motorTorque = currentBreakForce;
        blWheel.motorTorque = currentBreakForce;
        brWheel.motorTorque = currentBreakForce;*/
    }

    private void OnPowerslide()
    {
        Debug.Log($"Powersliding");
    }

    private void OnAirRoll()
    {
        float checker = playerMovement.actions.FindAction("AirRoll").ReadValue<float>();
        Debug.Log($"Air Roll {(checker >=0.5f ? "pressed":"released")}");
        debugConsole.isRolling = checker >= 0.5f;
        isRolling = checker >= 0.5f;
    }

    private void OnAirRollRight()
    {
        float checker = playerMovement.actions.FindAction("AirRollRight").ReadValue<float>();
        Debug.Log($"Air Roll Right {(checker >=0.5f ? "pressed":"released")}");
        isRollingRight = checker >= 0.5f;
    }

    public bool IsFullyGrounded()
    {
        return wheelHandlerFr.IsGrounded && wheelHandlerFl.IsGrounded && wheelHandlerBr.IsGrounded && wheelHandlerBl.IsGrounded;
    }

    public bool IsPartiallyGrounded()
    {
        return wheelHandlerFr.IsGrounded || wheelHandlerFl.IsGrounded || wheelHandlerBr.IsGrounded || wheelHandlerBl.IsGrounded;
    }

    public static float MapFloat(float fromMin, float fromMax, float toMin, float toMax, float val)
    {
        float revFac = Mathf.InverseLerp(fromMin, fromMax, val);
        float output = Mathf.Lerp(toMin, toMax, revFac);
        return output;
    }
}
