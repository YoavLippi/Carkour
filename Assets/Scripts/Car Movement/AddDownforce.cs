using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CarController))]
[RequireComponent(typeof(Rigidbody))]
public class AddDownforce : MonoBehaviour
{
    [SerializeField] private CarController thisCarController;

    [SerializeField] private Rigidbody rb;

    [SerializeField] private AnimationCurve downforceCurve;

    [SerializeField] private bool debugIsGrounded;
    // Start is called before the first frame update
    void Start()
    {
        thisCarController = GetComponent<CarController>();
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    private void FixedUpdate()
    {
        Debug.DrawLine(transform.position, transform.position - (transform.up*10), Color.blue);
        if (thisCarController.IsFullyGrounded() || debugIsGrounded)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);
            float speedFactor = CarController.MapFloat(0, thisCarController.SoftVelocityCap, 0, 1, Mathf.Abs(localVelocity.z));
            rb.AddForce(-transform.up * downforceCurve.Evaluate(speedFactor), ForceMode.Acceleration);
        }
    }
}
