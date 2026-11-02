using UnityEngine;
using UnityEngine.InputSystem;

// All of the game's controls live in this one file, using Unity's new Input System.
// Other scripts ask it questions like GameInput.JumpPressed instead of reading keys themselves,
// so if a key or button ever changes, this is the only file to edit.
// Needs the "Input System" package installed (Window > Package Manager > Unity Registry).
public static class GameInput
{
    // -1 = left, 0 = nothing, 1 = right. A/D, arrow keys, or the left stick.
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
            }

            return Mathf.Clamp(x, -1f, 1f);
        }
    }

    // Space, or the A button (bottom button) on a controller.
    public static bool JumpPressed
    {
        get
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.spaceKey.wasPressedThisFrame)
                || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        }
    }

    // Left Shift, or the B button (right button).
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

    // Left mouse click, or the Y button (top button) on a controller.
    public static bool AttackPressed
    {
        get
        {
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (pad != null && pad.buttonNorth.wasPressedThisFrame);
        }
    }

    // J, or the X button (left button). Not used yet; the parry comes next.
    public static bool ParryPressed
    {
        get
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.jKey.wasPressedThisFrame)
                || (pad != null && pad.buttonWest.wasPressedThisFrame);
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