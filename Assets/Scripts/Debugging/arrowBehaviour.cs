using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class arrowBehaviour : MonoBehaviour
{
    [SerializeField] private GameObject other;

    private void OnEnable()
    {
        other.SetActive(false);
    }
    private void OnDisable()
    {
        other.SetActive(true);
    }
}
