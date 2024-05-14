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
    public bool isPressingDirection;
    public bool isRolling;

    private void Update()
    {
        console.text = $"DEBUG CONSOLE:\nDirection Input {moveInput}\nMove Speed: {moveSpeed}\n" +
                       $"Is Grounded: {isGrounded}\nIs pressing dir:{isPressingDirection}\nIs Rolling:{isRolling}";
    }
}
