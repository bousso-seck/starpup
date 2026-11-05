using UnityEngine;
using UnityEngine.InputSystem;

// All of the game's controls live in this one file, using Unity's new Input System.
// Other scripts ask it questions like GameInput.JumpPressed instead of reading keys themselves,
// so if a key or button ever changes, this is the only file to edit.
// Needs the "Input System" package installed (Window > Package Manager > Unity Registry).
public static class GameInput
{
    // -1 = left, 0 = nothing, 1 = right. A/D, arrow keys, the left stick, or the d-pad.
    public static float Move
    {
        get
        {
            float x = 0f;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                float stick = pad.leftStick.x.ReadValue();
                if (Mathf.Abs(stick) > 0.2f) x += stick; // small dead zone so a resting stick does nothing
                if (pad.dpad.left.isPressed) x -= 1f;
                if (pad.dpad.right.isPressed) x += 1f;
            }

            return Mathf.Clamp(x, -1f, 1f);
        }
    }

    // Space, or Cross (X, the bottom button) on a PS4 controller, or d-pad up.
    public static bool JumpPressed
    {
        get
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.spaceKey.wasPressedThisFrame)
                || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.dpad.up.wasPressedThisFrame));
        }
    }

    // Left Shift, or Circle (the right button) on a PS4 controller.
    public static bool DodgePressed
    {
        get
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.leftShiftKey.wasPressedThisFrame)
                || (pad != null && pad.buttonEast.wasPressedThisFrame);
        }
    }

    // Left mouse click, or Square (the left button) on a PS4 controller.
    public static bool AttackPressed
    {
        get
        {
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (pad != null && pad.buttonWest.wasPressedThisFrame);
        }
    }

    // True for as long as the attack button is held down (used for the charged heavy strike).
    public static bool AttackHeld
    {
        get
        {
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            return (mouse != null && mouse.leftButton.isPressed)
                || (pad != null && pad.buttonWest.isPressed);
        }
    }

    // Hold C, S or the down arrow, or push the left stick / d-pad down, to crouch.
    // (Left Ctrl is avoided on purpose: on a Mac, Ctrl + click counts as a right click.)
    public static bool CrouchHeld
    {
        get
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && (kb.cKey.isPressed || kb.sKey.isPressed || kb.downArrowKey.isPressed))
                || (pad != null && (pad.leftStick.y.ReadValue() < -0.6f || pad.dpad.down.isPressed));
        }
    }

    // Right mouse click, or Triangle (the top button) on a PS4 controller.
    public static bool ParryPressed
    {
        get
        {
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            return (mouse != null && mouse.rightButton.wasPressedThisFrame)
                || (pad != null && pad.buttonNorth.wasPressedThisFrame);
        }
    }

    // G, or the right bumper. Not used yet; the guitar minigame comes later.
    public static bool GuitarPressed
    {
        get
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.gKey.wasPressedThisFrame)
                || (pad != null && pad.rightShoulder.wasPressedThisFrame);
        }
    }
}