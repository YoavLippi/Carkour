using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class CameraFollowBehaviour : MonoBehaviour
{
    [SerializeField] private Transform followTarget;

    [SerializeField] private Transform lookTarget;

    [SerializeField] private CarPhysics attachedCar;

    [SerializeField] private Vector3 followPosFloating;

    [SerializeField] private float rotationFollowSpeed;

    [SerializeField] private float posFollowSpeed;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        //should be a state controller with easing
        if (attachedCar.IsFullyGrounded())
        {
            DoGroundedFollow();
            DoLookTowards(lookTarget.position);
        }
        else
        {
            DoAerialFollow();
        }
    }

    private void DoGroundedFollow()
    {
        Vector3 direction = followTarget.position - transform.position;
        float distance = direction.magnitude;
        float adjustedSpeed = posFollowSpeed * distance;
        //Automatic follow behaviour
        transform.position = Vector3.Slerp(transform.position, followTarget.position, adjustedSpeed * Time.deltaTime);
        
        followPosFloating = transform.position - attachedCar.transform.position;
    }

    private void DoAerialFollow()
    {
        //follow the movements of the car, ignoring rotations
        transform.position = attachedCar.transform.position + followPosFloating;
    }

    private void DoLookTowards(Vector3 target)
    {
        // Calculate the direction to the target
        Vector3 direction = target - transform.position;
        
        // Calculate the target rotation
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        // Calculate distance to the target
        float distance = direction.magnitude;
        // Adjust rotation speed based on distance
        float adjustedSpeed = rotationFollowSpeed * distance;

        // Smoothly interpolate between the current rotation and the target rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, adjustedSpeed * Time.deltaTime);
    }
}
