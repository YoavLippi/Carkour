using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DebugConsole : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI console;
    public Vector2 moveInput;
    public float moveSpeed;
    public bool isGrounded;

    private void Update()
    {
        console.text = $"DEBUG CONSOLE:\nDirection Input {moveInput}\nMove Speed: {moveSpeed}\nIs Grounded: {isGrounded}";
    }
}
