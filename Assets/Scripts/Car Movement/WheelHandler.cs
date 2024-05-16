using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelHandler : MonoBehaviour
{
    [SerializeField] private MeshCollider wheelCol;

    [SerializeField] private float drawDistance = 0.5f;

    [SerializeField] private bool isGrounded;

    [SerializeField] private Rigidbody rb;

    [SerializeField] private float stickiness;

    public bool IsGrounded
    {
        get => isGrounded;
        set => isGrounded = value;
    }

    // Start is called before the first frame update
    void Start()
    {
        wheelCol = GetComponent<MeshCollider>();
    }

    private void FixedUpdate()
    {
        //Debug.DrawRay(transform.position, -transform.up, Color.green);
        Debug.DrawLine(transform.position, transform.position-(transform.up*drawDistance), Color.green);
        
        //draw distance is 0.75 units
        RaycastHit hit;
        int layerMask = LayerMask.NameToLayer("CarCol");
        //bitwise inversion
        layerMask = ~layerMask;
        if (Physics.Raycast(transform.position, -transform.up, out hit,drawDistance, layerMask))
        {
            //Debug.Log("Hit something");
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        //Debug.Log("Test entered");
        RaycastHit hit;
        int layerMask = LayerMask.NameToLayer("CarCol");
        //bitwise inversion
        layerMask = ~layerMask;
        if (Physics.SphereCast(transform.position, 0.5f, -transform.up, out hit, drawDistance*2, layerMask))
        {
            Debug.Log("Hitting floor");
            Vector3 forceDirection = hit.point - transform.position;
            rb.AddForceAtPosition(forceDirection*stickiness, transform.position, ForceMode.Acceleration);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        StartCoroutine(CoyoteForce());
    }

    private IEnumerator CoyoteForce()
    {
        float timeApplied = 0.5f, currentTime = 0;
        do
        {
            RaycastHit hit;
            int layerMask = LayerMask.NameToLayer("CarCol");
            //bitwise inversion
            layerMask = ~layerMask;
            if (Physics.SphereCast(transform.position, 0.5f, -transform.up, out hit, drawDistance * 4, layerMask))
            {
                Debug.Log("Hitting floor coyote");
                Vector3 forceDirection = hit.point - transform.position;
                rb.AddForceAtPosition(forceDirection * stickiness, transform.position, ForceMode.Acceleration);
            }

            currentTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        } while (timeApplied > currentTime);
    }
}
