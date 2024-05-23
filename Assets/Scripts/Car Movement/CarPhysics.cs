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

    [Header("Animation Curves")] 
    [SerializeField] private AnimationCurve steeringCurve;
    [SerializeField] private AnimationCurve softVelocityCapCurve;
    [SerializeField] private AnimationCurve accelerationCurve;
    [SerializeField] private AnimationCurve breakingCurve;
    
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
    }

    private void OnEnable()
    {
        playerMovement.actions.Enable();
    }

    private void OnDisable()
    {
        playerMovement.actions.Disable();
    }
    
    private void FixedUpdate()
    {
        debugConsole.moveSpeed = carBody.velocity.magnitude;
        debugConsole.isGrounded = IsFullyGrounded();
        //we want a local gravity when we're on walls, then a global one to blend to and to fall with and things like that
        //global gravity
        if (IsFullyGrounded())
        {
            carBody.AddForce(downforceScale*-transform.up, ForceMode.Acceleration);
        }
        else
        {
            carBody.AddForce(new Vector3(0,-1,0)*gravityScale, ForceMode.Acceleration);
            DoAerialMovement();
        }
        
        if (!IsPartiallyGrounded() && moveDirection.magnitude == 0 && !isRollingRight)
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
    }

    private void DoAerialMovement()
    {
        //pitch
        carBody.AddTorque(carBody.transform.right*(aerialTurnAcceleration*moveDirection.y), ForceMode.Acceleration);
        
        //roll
        if (isRolling)
        {
            carBody.AddTorque(-carBody.transform.forward*(aerialTurnAcceleration*moveDirection.x), ForceMode.Acceleration);
        }
        else
        {
            //yaw
            carBody.AddTorque(carBody.transform.up*(aerialTurnAcceleration*moveDirection.x), ForceMode.Acceleration);
        }
        
        //air roll right specifically
        if (isRollingRight)
        {
            carBody.AddTorque(-carBody.transform.forward*(aerialTurnAcceleration*1), ForceMode.Acceleration);
        }
        carBody.angularVelocity = ClampVector(carBody.angularVelocity, maxAerialTurnSpeed);
    }
    
    private void OnAirRoll()
    {
        if (!enabled) return;
        float checker = playerMovement.actions.FindAction("AirRoll").ReadValue<float>();
        Debug.Log($"Air Roll {(checker >=0.5f ? "pressed":"released")}");
        debugConsole.isRolling = checker >= 0.5f;
        isRolling = checker >= 0.5f;
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
        debugConsole.moveInput = moveDirection;
        //Debug.Log($"Moving with {moveDirection}");
    }
    
    private void OnThrottle()
    {
        if (!enabled) return;
        float throttleAmount = playerMovement.actions.FindAction("Throttle").ReadValue<float>();
        //Debug.Log($"Throttling with {throttleAmount}");
        currentAcceleration = accelerationCurve.Evaluate(throttleAmount);

        if (!IsPartiallyGrounded()) return;
        currentVelocityCap = isBoosting ? hardVelocityCap : softVelocityCapCurve.Evaluate(throttleAmount) * softVelocityCapForward;
    }
    
    private void OnReverse()
    {
        if (!enabled) return;
        float reverseAmount = playerMovement.actions.FindAction("Reverse").ReadValue<float>();
        //Debug.Log($"Reversing with {reverseAmount}");
        currentBreakForce = breakingCurve.Evaluate(reverseAmount);

        if (!IsPartiallyGrounded()) return;
        currentVelocityCap = isBoosting ? hardVelocityCap : softVelocityCapCurve.Evaluate(reverseAmount) * softVelocityCapBackward;
    }

    private void OnJump()
    {
        Vector3 jumpDirection = carBody.transform.up;
        carBody.AddForce(jumpDirection*jumpForce, ForceMode.VelocityChange);
    }
    
    private void OnBoost()
    {
        if (!enabled) return;
        float checker = playerMovement.actions.FindAction("Boost").ReadValue<float>();
        Debug.Log($"Boost {(checker >=0.5f ? "pressed":"released")}");
        isBoosting = (checker >= 0.5f);
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
