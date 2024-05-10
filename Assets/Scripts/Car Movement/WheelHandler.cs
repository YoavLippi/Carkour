using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelHandler : MonoBehaviour
{
    [SerializeField] private MeshCollider wheelCol;

    [SerializeField] private float drawDistance = 0.5f;

    [SerializeField] private bool isGrounded;

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

    private void OnCollisionEnter(Collision other)
    {
        Debug.Log("Wheeeeeeeeeeeeeeeeeel");
    }
}
