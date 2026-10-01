using Godot;

namespace XWing.Client;

/// <summary>
/// Default bindings, registered in code so they live next to the logic and can be remapped at
/// runtime. Keyboard layout follows the original game where known (docs/CONTROLS.md).
/// </summary>
public static class Controls
{
    public const string PitchUp = "pitch_up", PitchDown = "pitch_down";
    public const string YawLeft = "yaw_left", YawRight = "yaw_right";
    public const string RollLeft = "roll_left", RollRight = "roll_right";
    public const string Fire = "fire";
    public const string Throttle0 = "throttle_0", Throttle33 = "throttle_33", Throttle67 = "throttle_67", Throttle100 = "throttle_100";
    public const string ThrottleUp = "throttle_up", ThrottleDown = "throttle_down";
    public const string FireMode = "fire_mode";
    public const string TargetNext = "target_next", TargetPrev = "target_prev", TargetNearest = "target_nearest";
    public const string LaserRecharge = "laser_recharge", ShieldRecharge = "shield_recharge", ShieldFocus = "shield_focus";
    public const string LasersToShields = "lasers_to_shields", ShieldsToLasers = "shields_to_lasers";
    public const string Hyperspace = "hyperspace";
    public const string ToggleMusic = "toggle_music";
    public const string ToggleView = "toggle_view", Pause = "pause", Restart = "restart", Quit = "quit";

    public static void Register()
    {
        // Flight-sim convention: down arrow / stick back pulls the nose up.
        Bind(PitchUp, 0.15f, Key.Down, axis: (JoyAxis.LeftY, 1f));
        Bind(PitchDown, 0.15f, Key.Up, axis: (JoyAxis.LeftY, -1f));
        Bind(YawLeft, 0.15f, Key.Left, axis: (JoyAxis.LeftX, -1f));
        Bind(YawRight, 0.15f, Key.Right, axis: (JoyAxis.LeftX, 1f));
        Bind(RollLeft, 0.15f, Key.Q, axis: (JoyAxis.RightX, -1f));
        Bind(RollRight, 0.15f, Key.E, axis: (JoyAxis.RightX, 1f));
        Bind(Fire, 0.5f, Key.Space, button: JoyButton.A);

        Bind(Throttle0, 0.5f, Key.Backspace);
        Bind(Throttle33, 0.5f, Key.Bracketleft);
        Bind(Throttle67, 0.5f, Key.Bracketright);
        Bind(Throttle100, 0.5f, Key.Backslash);
        Bind(ThrottleUp, 0.5f, Key.Equal, button: JoyButton.RightShoulder);
        Bind(ThrottleDown, 0.5f, Key.Minus, button: JoyButton.LeftShoulder);

        Bind(FireMode, 0.5f, Key.X, button: JoyButton.X);
        Bind(TargetNext, 0.5f, Key.T, button: JoyButton.Y);
        Bind(TargetPrev, 0.5f, Key.Y);
        Bind(TargetNearest, 0.5f, Key.R, button: JoyButton.B);
        Bind(LaserRecharge, 0.5f, Key.F9);
        Bind(ShieldRecharge, 0.5f, Key.F10);
        Bind(ShieldFocus, 0.5f, Key.S);
        Bind(LasersToShields, 0.5f, Key.Semicolon);
        Bind(ShieldsToLasers, 0.5f, Key.Apostrophe);
        Bind(Hyperspace, 0.5f, Key.H);

        Bind(ToggleView, 0.5f, Key.V);
        Bind(ToggleMusic, 0.5f, Key.M);
        Bind(Pause, 0.5f, Key.P);
        Bind(Restart, 0.5f, Key.F5);
        Bind(Quit, 0.5f, Key.Escape);
    }

    private static void Bind(string action, float deadzone, Key key, (JoyAxis axis, float dir)? axis = null, JoyButton? button = null)
    {
        if (InputMap.HasAction(action)) return;
        InputMap.AddAction(action, deadzone);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
        if (axis is { } a) InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Axis = a.axis, AxisValue = a.dir });
        if (button is { } b) InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = b });
    }
}
