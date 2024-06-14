using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class CarPhysics : MonoBehaviour
{
    [Header("Setup")] 
    [SerializeField] private Rigidbody carBody;
    [SerializeField] private WheelHandlerRaycast frWheel;
    [SerializeField] private WheelHandlerRaycast flWheel;
    [SerializeField] private WheelHandlerRaycast brWheel;
    [SerializeField] private WheelHandlerRaycast blWheel;
    [SerializeField] private List<WheelHandlerRaycast> wheels;
    [SerializeField] private PlayerInput playerMovement;
    [SerializeField] private TrailRenderer boostRenderer;
    [SerializeField] private TrailRenderer otherBoostRenderer;
    //This arrow tracks the raw stick input...
    [SerializeField] private GameObject turnArrow;

    [Header("Physics")]
    [SerializeField] private float gravityScale;
    [SerializeField] private float gravityBlendFactor;
    [SerializeField] private float downforceScale;
    [SerializeField] private float softVelocityCapForward;
    [SerializeField] private float softVelocityCapBackward;
    [SerializeField] private float hardVelocityCap;
    [SerializeField] private float jumpForce;
    [SerializeField] private float maxAerialTurnSpeed;
    [SerializeField] private float aerialTurnAcceleration;
    [SerializeField] private float boostAcceleration;
    [SerializeField] private float slideGripFactor;
    [SerializeField] private float flipDeadzone;
    [SerializeField] private float totalFlipSpeed;
    [SerializeField] private float velocityDampingFactor;

    [Header("Animation Curves")] 
    [SerializeField] private AnimationCurve steeringCurve;
    [SerializeField] private AnimationCurve softVelocityCapCurve;
    [SerializeField] private AnimationCurve accelerationCurve;
    [SerializeField] private AnimationCurve breakingCurve;
    [SerializeField] private AnimationCurve flipSpeedCurve;
    
    [Header("Listeners")]
    [SerializeField] private DebugConsole debugConsole;
    [SerializeField] private Vector2 moveDirection;
    [SerializeField] private float currentAcceleration;
    [SerializeField] private float currentBreakForce;
    [SerializeField] private float currentVelocityCap;
    [SerializeField] private bool isRolling;
    [SerializeField] private bool isRollingRight;
    [SerializeField] private bool isBoosting;
    [SerializeField] private bool isSliding;
    [SerializeField] private float throttleAmount;
    [SerializeField] private float reverseAmount;
    [SerializeField] private float turnDegrees;
    [SerializeField] private FlipDirection currentFlipDirection;
    [SerializeField] private bool hasFlip;
    [SerializeField] private CarState currentState;
    [SerializeField] private float aerialJumpForce;
    [SerializeField] private float aerialNeutralJumpForce;
    public AnimationCurve SteeringCurve => steeringCurve;

    public AnimationCurve SoftVelocityCapCurve => softVelocityCapCurve;

    public AnimationCurve AccelerationCurve => accelerationCurve;

    public Vector2 MoveDirection => moveDirection;

    public float CurrentVelocityCap => currentVelocityCap;

    public float CurrentAcceleration => currentAcceleration;

    public float CurrentBreakForce => currentBreakForce;

    public float DownforceScale => downforceScale;

    private void Start()
    {
        debugConsole = GameObject.FindWithTag("DebugLogger").GetComponent<DebugConsole>();
        carBody = GetComponent<Rigidbody>();
        wheels = new List<WheelHandlerRaycast>();
        wheels.Add(frWheel);
        wheels.Add(flWheel);
        wheels.Add(brWheel);
        wheels.Add(blWheel);
        currentState = CarState.Grounded | CarState.Aerial;
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
        //very expensive, but needed
        if (isBoosting)
        {
            currentVelocityCap = hardVelocityCap;
            return;
        }
        
        if (currentBreakForce != 0 && currentAcceleration != 0)
        {
            currentVelocityCap = 0;
        } else if (currentAcceleration > 0)
        {
            currentVelocityCap = softVelocityCapCurve.Evaluate(throttleAmount) * softVelocityCapForward;
        } else if (currentBreakForce > 0)
        {
            currentVelocityCap = softVelocityCapCurve.Evaluate(reverseAmount) * softVelocityCapBackward;
        }
        else
        {
            currentVelocityCap = 0;
        }
    }

    private void FixedUpdate()
    {
        #region Debug

        Transform thisTransform = transform;
        /*Vector3 forwardFlipDir = thisTransform.position + (transform.forward*5);
        Vector3 backwardFlipDir = thisTransform.position - (transform.forward*5);
        Vector3 rightFlipDir = thisTransform.position + (transform.right * 5);
        Vector3 leftFlipDir = thisTransform.position - (transform.right * 5);*/
        Vector3 forwardFlipDir = thisTransform.position + (new Vector3(thisTransform.forward.x, 0, thisTransform.forward.z).normalized * 5);
        Vector3 backwardFlipDir = thisTransform.position - (new Vector3(thisTransform.forward.x, 0, thisTransform.forward.z).normalized * 5);
        Vector3 rightFlipDir = thisTransform.position + (new Vector3(thisTransform.right.x, 0, thisTransform.right.z).normalized * 5);
        Vector3 leftFlipDir = thisTransform.position - (new Vector3(thisTransform.right.x, 0, thisTransform.right.z).normalized * 5);
        Debug.DrawLine(thisTransform.position, forwardFlipDir, Color.blue);
        Debug.DrawLine(thisTransform.position, backwardFlipDir, Color.green);
        Debug.DrawLine(thisTransform.position, rightFlipDir, Color.red);
        Debug.DrawLine(thisTransform.position, leftFlipDir, Color.magenta);
        
        debugConsole.moveSpeed = carBody.velocity.magnitude;
        debugConsole.isGrounded = IsFullyGrounded();        

        #endregion

        #region State Control

        if (IsFullyGrounded())
        {
            currentState |= CarState.Grounded;
        }
        else
        {
            currentState &= ~CarState.Grounded;
        }

        if (IsPartiallyGrounded())
        {
            currentState |= CarState.PartiallyGrounded;
            currentState &= ~CarState.Aerial;
        }
        else
        {
            currentState &= ~CarState.PartiallyGrounded;
            currentState |= CarState.Aerial;
        }

        #endregion

        #region Physics

        //we want a local gravity when we're on walls, then a global one to blend to and to fall with and things like that
        //global gravity
        if (IsFullyGrounded())
        {
            carBody.AddForce(downforceScale*-transform.up, ForceMode.Acceleration);
            hasFlip = true;
        }
        else
        {
            carBody.AddForce(new Vector3(0,-1,0)*gravityScale, ForceMode.Acceleration);
            DoAerialMovement();
        }
        
        if (!IsPartiallyGrounded() && moveDirection.magnitude == 0 && !isRollingRight && (currentState & CarState.Flipping) != CarState.Flipping)
        {
            carBody.angularVelocity = new Vector3(0,0,0);
        }

        if (isBoosting)
        {
            carBody.AddForce(carBody.transform.forward*boostAcceleration, ForceMode.Acceleration);
        }
        
        //we want to rotate the wheels based on the inputted move direction, hopefully the traction calculations will do the rest
        float steerAngle = steeringCurve.Evaluate(Mathf.Abs(moveDirection.x)) * (moveDirection.x >=0 ? 1 : -1);
        //TODO: interpolate rotations rather than hard snapping
        flWheel.transform.localRotation = Quaternion.Euler(0,steerAngle,0);
        frWheel.transform.localRotation = Quaternion.Euler(0,steerAngle,0);

        carBody.velocity = ClampVector(carBody.velocity, hardVelocityCap);
        if ((currentState & CarState.Aerial) != CarState.Aerial)
        {
            ApplyRubberBanding();
        }

        #endregion
    }

    private void ApplyRubberBanding()
    {
        // Calculate the magnitude of the current velocity
        float currentSpeed = carBody.velocity.magnitude;

        // Determine the desired velocity cap in the direction of current movement
        Vector3 desiredVelocity = carBody.velocity.normalized * Mathf.Min(currentSpeed, currentVelocityCap);

        // Calculate the difference between the current velocity and the desired velocity
        Vector3 velocityDifference = desiredVelocity - carBody.velocity;

        // Apply the damping factor to the velocity difference to get the adjustment
        Vector3 velocityAdjustment = velocityDifference * velocityDampingFactor;

        // Apply the velocity adjustment to the car's current velocity
        carBody.velocity += velocityAdjustment;
    }

    private void DoAerialMovement()
    {
        //This isn't the best implementation, since it causes direction switching to break because of leftover momentum
        
        //pitch
        carBody.AddTorque(carBody.transform.right*(aerialTurnAcceleration*moveDirection.y), ForceMode.Acceleration);
        
        //air roll right specifically
        if (isRollingRight)
        {
            carBody.AddTorque(-carBody.transform.forward*(aerialTurnAcceleration*1), ForceMode.Acceleration);
        }
        
        //roll
        if ((currentState & CarState.Flipping) != CarState.Flipping)
        {
            if (isRolling)
            {
                carBody.AddTorque(-carBody.transform.forward*(aerialTurnAcceleration*moveDirection.x), ForceMode.Acceleration);
            }
            else
            {
                //yaw
                carBody.AddTorque(carBody.transform.up*(aerialTurnAcceleration*moveDirection.x), ForceMode.Acceleration);
            }
        }
        carBody.angularVelocity = ClampVector(carBody.angularVelocity, maxAerialTurnSpeed);
    }

    [Serializable]
    [Flags]
    //state control tracker, will be used theoretically in the future to track every state and change other behaviours accordingly
    //implemented as a binary sequence in case multiple flags can be true in the future - can check each flag with bitwise operations
    //add multiple flags with binary OR (|)
    //check a state with binary AND (&) operation against what you're checking for: currentstate & CarState.Grounded == Carstate.Grounded
    //Remove a state with an inverted AND operation: currentstate = currentstate & ~Carstate.Grounded (or currentstate &= ~Carstate.Grounded)
    //Add a state with an OR operation: currentstate = currentstate | Carstate.Grounded (or currentstate |= Carstate.grounded)
    private enum CarState
    {
        Grounded = 1<<0,            //0001
        Aerial = 1<<1,              //0010
        PartiallyGrounded = 1<<2,   //0100
        Flipping = 1<<3             //1000
    }
    
    [Serializable]
    private enum FlipDirection
    {
        Right,           // 0°
        RightUpRight,   // 22.5°
        UpRight,        // 45°
        UpRightUp,      // 67.5°
        Up,             // 90°
        UpLeftUp,       // 112.5°
        UpLeft,         // 135°
        LeftUpLeft,     // 157.5°
        Left,           // 180° or -180°
        LeftDownLeft,   // -157.5°
        DownLeft,       // -135°
        DownLeftDown,   // -112.5°
        Down,           // -90°
        DownRightDown,  // -67.5°
        DownRight,      // -45°
        RightDownRight, // -22.5°
        None            // directional input is 0 or outside the range somehow
    }
    
    private FlipDirection AngleToDirection(float angle)
    {
        if (angle is >= -11.25f and < 11.25f) return FlipDirection.Right;
        if (angle is >= 11.25f and < 33.75f) return FlipDirection.RightUpRight;
        if (angle is >= 33.75f and < 56.25f) return FlipDirection.UpRight;
        if (angle is >= 56.25f and < 78.75f) return FlipDirection.UpRightUp;
        if (angle is >= 78.75f and < 101.25f) return FlipDirection.Up;
        if (angle is >= 101.25f and < 123.75f) return FlipDirection.UpLeftUp;
        if (angle is >= 123.75f and < 146.25f) return FlipDirection.UpLeft;
        if (angle is >= 146.25f and < 168.75f) return FlipDirection.LeftUpLeft;
        if (angle is >= 168.75f and < 180.45f or < -168.75f and >= -180.45f) return FlipDirection.Left;
        if (angle is >= -168.75f and < -146.25f) return FlipDirection.LeftDownLeft;
        if (angle is >= -146.25f and < -123.75f) return FlipDirection.DownLeft;
        if (angle is >= -123.75f and < -101.25f) return FlipDirection.DownLeftDown;
        if (angle is >= -101.25f and < -78.75f) return FlipDirection.Down;
        if (angle is >= -78.75f and < -56.25f) return FlipDirection.DownRightDown;
        if (angle is >= -56.25f and < -33.75f) return FlipDirection.DownRight;
        if (angle is >= -33.75f and < -11.25f) return FlipDirection.RightDownRight;
        
        return FlipDirection.None;
    }
    
    private void OnAirRoll()
    {
        if (!enabled) return;
        float checker = playerMovement.actions.FindAction("AirRoll").ReadValue<float>();
        Debug.Log($"Air Roll {(checker >=0.5f ? "pressed":"released")}");
        debugConsole.isRolling = checker >= 0.5f;
        isRolling = checker >= 0.5f;
        if (isRolling) carBody.angularVelocity = new Vector3(carBody.angularVelocity.x,0,carBody.angularVelocity.z);
    }

    private void OnAirRollRight()
    {
        if (!enabled) return;
        float checker = playerMovement.actions.FindAction("AirRollRight").ReadValue<float>();
        Debug.Log($"Air Roll Right {(checker >=0.5f ? "pressed":"released")}");
        isRollingRight = checker >= 0.5f;
    }
    
    private void OnMove()
    {
        if (!enabled) return;
        moveDirection = playerMovement.actions.FindAction("Move").ReadValue<Vector2>();
        
        //Deadzone calculations for flips
        // Calculate the magnitude of the input vector
        float magnitude = moveDirection.magnitude;
        
        // Normalize the input vector and scale it outside the deadzone
        Vector2 normalizedInput = moveDirection / magnitude;
        Vector2 scaledInput = normalizedInput * ((magnitude - flipDeadzone) / (1 - flipDeadzone));
        
        //getting the turn degrees for the flips, checking if it is within the deadzone
        turnDegrees = magnitude < flipDeadzone ? 420f : Mathf.Atan2(scaledInput.y, scaledInput.x) * (180/Mathf.PI);

        if (turnDegrees == 420f)
        {
            turnArrow.SetActive(false);
        }
        else
        {
            turnArrow.SetActive(true);
            turnArrow.transform.rotation = Quaternion.Euler(0,0,turnDegrees);
        }
        currentFlipDirection = AngleToDirection(turnDegrees);
        debugConsole.moveInput = moveDirection;
        //Debug.Log($"Moving with {moveDirection}");
    }
    
    private void OnThrottle()
    {
        if (!enabled) return;
        throttleAmount = playerMovement.actions.FindAction("Throttle").ReadValue<float>();
        //Debug.Log($"Throttling with {throttleAmount}");
        currentAcceleration = accelerationCurve.Evaluate(throttleAmount);

        //if (!IsPartiallyGrounded()) return;
        //currentVelocityCap = isBoosting ? hardVelocityCap : softVelocityCapCurve.Evaluate(throttleAmount) * softVelocityCapForward;
    }
    
    private void OnReverse()
    {
        if (!enabled) return;
        reverseAmount = playerMovement.actions.FindAction("Reverse").ReadValue<float>();
        //Debug.Log($"Reversing with {reverseAmount}");
        currentBreakForce = breakingCurve.Evaluate(reverseAmount);

        //if (!IsPartiallyGrounded()) return;
        //currentVelocityCap = isBoosting ? hardVelocityCap : softVelocityCapCurve.Evaluate(reverseAmount) * softVelocityCapBackward;
    }

    private void OnJump()
    {
        Vector3 jumpDirection = carBody.transform.up;
        carBody.angularVelocity = new Vector3(0, 0, 0);
        if (IsFullyGrounded())
        {
            carBody.AddForce(jumpDirection * jumpForce, ForceMode.VelocityChange);
            StartCoroutine(doFlipCooldown());
        }
        else
        {
            if (hasFlip)
            {
                StartCoroutine(DoFlip());
            }
        }
    }
    
    private IEnumerator DoFlip()
    {
        hasFlip = false;
        Vector3 jumpDirection = carBody.transform.up;
        //float flipSpeed = 5f;

        #region Influence Setup

        float forwardInfluence = 0f;
        float sideInfluence = 0f;
        switch (currentFlipDirection)
        {
            case FlipDirection.None :
                carBody.AddForce(jumpDirection * aerialNeutralJumpForce, ForceMode.VelocityChange);
                yield break;
            
            case FlipDirection.Right :
                sideInfluence = 1f;
                break;
            
            case FlipDirection.RightUpRight :
                forwardInfluence = 0.25f;
                sideInfluence = 0.75f;
                break;
            
            case FlipDirection.UpRight :
                forwardInfluence = 0.5f;
                sideInfluence = 0.5f;
                break;
            
            case FlipDirection.UpRightUp :
                forwardInfluence = 0.75f;
                sideInfluence = 0.25f;
                break;
            
            case FlipDirection.Up :
                forwardInfluence = 1f;
                break;
            
            case FlipDirection.UpLeftUp :
                forwardInfluence = 0.75f;
                sideInfluence = -0.25f;
                break;
            
            case FlipDirection.UpLeft :
                forwardInfluence = 0.5f;
                sideInfluence = -0.5f;
                break;
            
            case FlipDirection.LeftUpLeft :
                forwardInfluence = 0.25f;
                sideInfluence = -0.75f;
                break;
            
            case FlipDirection.Left :
                sideInfluence = -1f;
                break;
            
            case FlipDirection.LeftDownLeft :
                forwardInfluence = -0.25f;
                sideInfluence = -0.75f;
                break;
            
            case FlipDirection.DownLeft :
                forwardInfluence = -0.5f;
                sideInfluence = -0.5f;
                break;
            
            case FlipDirection.DownLeftDown :
                forwardInfluence = -0.75f;
                sideInfluence = -0.25f;
                break;
            
            case FlipDirection.Down :
                forwardInfluence = -1f;
                break;
            
            case FlipDirection.DownRightDown :
                forwardInfluence = -0.75f;
                sideInfluence = 0.25f;
                break;
            
            case FlipDirection.DownRight :
                forwardInfluence = -0.5f;
                sideInfluence = 0.5f;
                break;
            
            case FlipDirection.RightDownRight :
                forwardInfluence = -0.25f;
                sideInfluence = 0.75f;
                break;
        }

        #endregion
        
        currentState |= CarState.Flipping;
        
        //todo impulse calculations here
        Transform thisTransform = transform;
        Vector3 forwardFlipDir = (new Vector3(thisTransform.forward.x, 0, thisTransform.forward.z).normalized);
        Vector3 rightFlipDir = (new Vector3(thisTransform.right.x, 0, thisTransform.right.z).normalized);
        Vector3 flipForce = (forwardFlipDir * forwardInfluence) + (rightFlipDir * sideInfluence);
        flipForce.Normalize();
        carBody.AddForce(flipForce * aerialJumpForce, ForceMode.VelocityChange);
        
        float flipTime = 0.9f;
        float verticalCancelTime = 0.3f;
        float currentTime = 0;
        
        do
        {
            if (currentTime <= verticalCancelTime)
            {
                //cancelling vertical velocity
                Vector3 carVelocity = carBody.velocity;
                carBody.velocity = new Vector3(carVelocity.x, 0, carVelocity.z);
            }
            //checking if the held direction is going against the flip direction
            if (!((moveDirection.y > 0.5 && forwardInfluence < 0) || (moveDirection.y < -0.5 && forwardInfluence > 0)))
            {
                //pitch
                carBody.AddTorque(carBody.transform.right*(flipSpeedCurve.Evaluate(currentTime/flipTime)*forwardInfluence*totalFlipSpeed), ForceMode.Acceleration);
            }
        
            //roll
            carBody.AddTorque(-carBody.transform.forward*(flipSpeedCurve.Evaluate(currentTime/flipTime)*sideInfluence*totalFlipSpeed), ForceMode.Acceleration);
            
            yield return new WaitForEndOfFrame();
            currentTime += Time.deltaTime;
        } while (currentTime <= flipTime);

        currentState &= ~CarState.Flipping;
    }

    private IEnumerator doFlipCooldown()
    {
        yield return new WaitForSeconds(0.1f);
        //same cooldown time as in rocket league
        float cooldownTime = 1.5f;
        float currentTime = 0f;
        
        //timer method
        do
        {
            if (IsFullyGrounded())
            {
                yield break;
            }
            yield return new WaitForEndOfFrame();
            currentTime += Time.deltaTime;
        } while (currentTime <= cooldownTime);

        hasFlip = false;
    }
    
    private void OnBoost() 
    {
        if (!enabled) return;
        float checker = playerMovement.actions.FindAction("Boost").ReadValue<float>();
        Debug.Log($"Boost {(checker >=0.5f ? "pressed":"released")}");
        isBoosting = (checker >= 0.5f);
        boostRenderer.enabled = otherBoostRenderer.enabled = isBoosting;
        boostRenderer.Clear();
        otherBoostRenderer.Clear();
        if (isBoosting)
        {
            currentVelocityCap = hardVelocityCap;
        }
        else
        {
            OnReverse();
            OnThrottle();
        }
    }
    
    private void OnPowerslide()
    {
        if (!enabled) return;
        float checker = playerMovement.actions.FindAction("Powerslide").ReadValue<float>();
        Debug.Log($"Poweslide {(checker >=0.5f ? "pressed":"released")}");
        isSliding = (checker >= 0.5f);
        ChangeWheelTractions();
    }

    private void ChangeWheelTractions()
    {
        //TODO: Change traction calculation to blend between back and front wheels based on local z velocity (?)
        float tractionVal = isSliding ? slideGripFactor : 1f;
        wheels[2].TireGripFactor = tractionVal;
        wheels[3].TireGripFactor = tractionVal;
    }
    
    public bool IsFullyGrounded()
    {
        return frWheel.IsGrounded && flWheel.IsGrounded && brWheel.IsGrounded && blWheel.IsGrounded;
    }

    public bool IsPartiallyGrounded()
    {
        return frWheel.IsGrounded || flWheel.IsGrounded || brWheel.IsGrounded || blWheel.IsGrounded;
    }
    
    public static Vector3 ClampVector(Vector3 vector, float maxMagnitude)
    {
        if (vector.magnitude > maxMagnitude)
        {
            vector = vector.normalized * maxMagnitude;
        }
        return vector;
    }
}
