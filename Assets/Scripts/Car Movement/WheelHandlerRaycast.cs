using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WheelHandlerRaycast : MonoBehaviour
{
    [SerializeField] private Rigidbody carBody;
    [SerializeField] private CarPhysics parentPhysics;
    [SerializeField] private bool useSuspensionOnly;
    [SerializeField] private float wheelRadius;
    private float wheelDiameter;
    [SerializeField] private bool isGrounded;
    [SerializeField] private float suspensionRestDistance;
    [SerializeField] private float springForce;
    [SerializeField] private float springDamping;
    [SerializeField] private float tireGripFactor;

    public float TireGripFactor
    {
        get => tireGripFactor;
        set => tireGripFactor = value;
    }

    private void Start()
    {
        wheelDiameter = wheelRadius * 2;
        parentPhysics = carBody.transform.GetComponent<CarPhysics>();
    }

    public bool IsGrounded
    {
        get => isGrounded;
        set => isGrounded = value;
    }

    //a huge portion of this is coming from this explaination: https://www.youtube.com/watch?v=CdPYlj5uZeI&t=273s
    private void FixedUpdate()
    {
        #if UNITY_EDITOR
            wheelDiameter = wheelRadius * 2;
        #endif
        RaycastHit hit;
        int lm = LayerMask.NameToLayer("CarCol");
        lm = ~lm;
        Debug.DrawLine(transform.position+transform.up*wheelRadius, transform.position-transform.up*wheelRadius);
        
        //Suspension calculations
        //using diameter here so we can start the raycast at the top of the wheel
        if (Physics.Raycast(transform.position+transform.up*wheelRadius, -transform.up, out hit, wheelDiameter, lm))
        {
            isGrounded = true;
            
            Vector3 springDir = transform.up;

            Vector3 tireWorldVelocity = carBody.GetPointVelocity(transform.position);

            float offset = suspensionRestDistance - hit.distance;

            float vel = Vector3.Dot(springDir, tireWorldVelocity);

            float suspensionForce = (offset * springForce) - (vel * springDamping);
            
            if (useSuspensionOnly && suspensionForce<0) return;

            float fallingVel = -Vector3.Dot(springDir, tireWorldVelocity);
            //carBody.AddForceAtPosition(springDir*fallingVel, transform.position, ForceMode.Acceleration);
            //carBody.AddForceAtPosition(parentPhysics.DownforceScale*springDir, transform.position, ForceMode.Acceleration);
            carBody.AddForceAtPosition(suspensionForce*springDir, transform.position, ForceMode.Acceleration);
        }
        else
        {
            isGrounded = false;
        }

        if (useSuspensionOnly) return;
        
        //traction calculation
        if (isGrounded)
        {
            Vector3 steeringDir = transform.right;

            Vector3 tireWorldVelocity = carBody.GetPointVelocity(transform.position);

            //Getting the velocity in the x direction, so that we can cancel it
            float steeringVel = Vector3.Dot(steeringDir, tireWorldVelocity);
            
            //This change in velocity is -steeringVel*gripFactor
            //Using gripfactor in range from 0-1 0=no grip, 1=full grip
            float desiredVelChange = -steeringVel * tireGripFactor;
            
            //Watch out in the future for if the car overturns - we can fix this by changing exactly where we're applying this force
            Vector3 applicationPoint = transform.position;
            //adding the y-displacement to the application point so it applies at the center and doesn't cause unwanted torque
            applicationPoint += transform.up * 0.693f;
            
            Debug.DrawLine(applicationPoint, applicationPoint+(transform.up * 1), Color.magenta);
            carBody.AddForceAtPosition(steeringDir*desiredVelChange, applicationPoint, ForceMode.Acceleration);
        }
        
        //Acceleration Calculation
        if (isGrounded)
        {
            Vector3 accelDir = transform.forward;
            
            Vector3 tireWorldVelocity = carBody.GetPointVelocity(transform.position);
            //We want to accelerate constantly, then the main physics body can clamp the speed based on the throttle input
            //We also want to take in the current soft cap, and only accelerate when we are below that cap. The main body will handle rubber-banding it down
            float accelerationAmount = parentPhysics.CurrentAcceleration;
            float breakAmount = parentPhysics.CurrentBreakForce;
            
            if (tireWorldVelocity.magnitude < parentPhysics.CurrentVelocityCap)
            {
                carBody.AddForceAtPosition(accelDir*(accelerationAmount-breakAmount), carBody.position, ForceMode.Acceleration);
            }

            /*if (tireWorldVelocity.magnitude < parentPhysics.CurrentVelocityCap)
            {
                carBody.AddForceAtPosition(accelDir*-breakAmount, carBody.position, ForceMode.Acceleration);
            }*/
            
            //TODO: apply rubberband instead of hard clamping
            carBody.velocity = CarPhysics.ClampVector(carBody.velocity, parentPhysics.CurrentVelocityCap);
        }
    }
    
}
