using UnityEngine;

namespace VoxelWorld.Player
{
    /// <summary>
    /// First-person motor: yaw rotates the body (CharacterController), pitch rotates the
    /// head. <see cref="Step"/> is driven by the composition root instead of Update so
    /// ordering and pausing stay deterministic.
    /// </summary>
    public sealed class PlayerMotor : MonoBehaviour
    {
        private CharacterController _controller;
        private Transform _head;
        private float _walkSpeed;
        private float _sprintSpeed;
        private float _jumpVelocity;
        private float _gravity;
        private float _mouseSensitivity;
        private float _pitchLimit;
        private float _yaw;
        private float _pitch;
        private float _verticalVelocity;
        private Vector2 _worldMin;
        private Vector2 _worldMax;

        public Vector3 Position => transform.position;
        public float Yaw => _yaw;
        public float Pitch => _pitch;

        /// <summary>Wires dependencies and places the head at eye height.</summary>
        public void Init(CharacterController controller, Transform head, float eyeHeight,
            float walkSpeed, float sprintSpeed, float jumpVelocity, float gravity,
            float mouseSensitivity, float pitchLimit, Vector3 worldMin, Vector3 worldMax)
        {
            _controller = controller;
            _head = head;
            _walkSpeed = walkSpeed;
            _sprintSpeed = sprintSpeed;
            _jumpVelocity = jumpVelocity;
            _gravity = gravity;
            _mouseSensitivity = mouseSensitivity;
            _pitchLimit = pitchLimit;
            _worldMin = new Vector2(worldMin.x, worldMin.z);
            _worldMax = new Vector2(worldMax.x, worldMax.z);
            _head.localPosition = new Vector3(0f, eyeHeight, 0f);
        }

        /// <summary>Hard-sets player pose (spawn or save load).</summary>
        public void Teleport(Vector3 position, float yaw, float pitch)
        {
            transform.position = position;
            _yaw = yaw;
            _pitch = pitch;
            _verticalVelocity = 0f;
            ApplyRotation();
        }

        public void Step(float dt, Vector2 moveAxis, Vector2 lookDelta, bool sprintHeld, bool jumpHeld)
        {
            _yaw = Mathf.Repeat(_yaw + lookDelta.x * _mouseSensitivity, 360f);
            _pitch = Mathf.Clamp(_pitch - lookDelta.y * _mouseSensitivity, -_pitchLimit, _pitchLimit);
            ApplyRotation();

            var input = Vector2.ClampMagnitude(moveAxis, 1f);
            var yawRotation = Quaternion.Euler(0f, _yaw, 0f);
            var planar = (yawRotation * Vector3.forward * input.y + yawRotation * Vector3.right * input.x)
                         * (sprintHeld ? _sprintSpeed : _walkSpeed);

            if (_controller.isGrounded)
            {
                _verticalVelocity = -2f; // keeps the controller pressed onto the ground
                if (jumpHeld)
                {
                    _verticalVelocity = _jumpVelocity;
                }
            }
            else
            {
                _verticalVelocity -= _gravity * dt;
            }

            _controller.Move((planar + Vector3.up * _verticalVelocity) * dt);

            // Soft safety clamp (invisible boundary walls are the primary constraint).
            var p = transform.position;
            var clamped = new Vector3(
                Mathf.Clamp(p.x, _worldMin.x, _worldMax.x), p.y,
                Mathf.Clamp(p.z, _worldMin.y, _worldMax.y));
            if (clamped != p)
            {
                transform.position = clamped;
            }
        }

        private void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
