using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Transform_Visualiser : MonoBehaviour
{
    public static float lineLength = 1f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Debug.DrawLine(transform.position, transform.position+(transform.forward*lineLength), Color.blue);
        Debug.DrawLine(transform.position, transform.position+(transform.right*lineLength), Color.red);
        Debug.DrawLine(transform.position, transform.position+(transform.up*lineLength), Color.green);
    }
}
