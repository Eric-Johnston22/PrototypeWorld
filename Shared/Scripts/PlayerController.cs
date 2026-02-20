using Godot;

namespace Hoarders;

/// <summary>
/// First-person player controller with WASD movement, mouse look, jump, and sprint.
/// Attach to a CharacterBody3D with a "Head" Node3D child containing the Camera3D.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
    [Export] public float WalkSpeed = 5.0f;
    [Export] public float SprintSpeed = 8.5f;
    [Export] public float JumpVelocity = 5.0f;
    [Export] public float MouseSensitivity = 0.002f;
    [Export] public float Acceleration = 10.0f;
    [Export] public float Friction = 8.0f;

    private Node3D _head;
    private float _gravity;

    public override void _Ready()
    {
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        _head = GetNode<Node3D>("Head");
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);
            _head.RotateX(-mouseMotion.Relative.Y * MouseSensitivity);
            var headRot = _head.Rotation;
            headRot.X = Mathf.Clamp(headRot.X, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
            _head.Rotation = headRot;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var velocity = Velocity;

        // Gravity
        if (!IsOnFloor())
            velocity.Y -= _gravity * dt;

        // Jump
        if (Input.IsActionJustPressed("jump") && IsOnFloor())
            velocity.Y = JumpVelocity;

        // Movement
        var inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        var direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        float speed = Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;

        if (direction != Vector3.Zero)
        {
            velocity.X = Mathf.MoveToward(velocity.X, direction.X * speed, Acceleration * dt * speed);
            velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * speed, Acceleration * dt * speed);
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0, Friction * dt * speed);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0, Friction * dt * speed);
        }

        Velocity = velocity;
        MoveAndSlide();
    }
}
