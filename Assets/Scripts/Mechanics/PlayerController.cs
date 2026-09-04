using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Platformer.Gameplay;
using static Platformer.Core.Simulation;
using Platformer.Model;
using Platformer.Core;
using UnityEngine.InputSystem;

namespace Platformer.Mechanics
{
    /// <summary>
    /// This is the main class used to implement control of the player.
    /// It is a superset of the AnimationController class, but is inlined to allow for any kind of customisation.
    /// </summary>
    public class PlayerController : KinematicObject
    {
        public AudioClip jumpAudio;
        public AudioClip respawnAudio;
        public AudioClip ouchAudio;

        /// <summary>
        /// Max horizontal speed of the player.
        /// </summary>
        public float maxSpeed = 7;
        /// <summary>
        /// Initial jump velocity at the start of a jump.
        /// </summary>
        public float jumpTakeOffSpeed = 7;

        [Header("Click Movement")]
        public bool clickToJump = true;
        public float clickHorizontalSpeed = 6.5f;
        public float clickJumpSpeed = 8f;
        public float clickVerticalInfluence = 1.5f;
        [Range(0.05f, 1f)] public float midairRedirectStrength = 0.65f;
        public float groundFriction = 30f;

        [Header("Cake Weight")]
        public float baseGravityModifier = 1f;
        public float gravityPerExtraLayer = 0.12f;
        public float weightPerExtraLayer = 0.12f;
        public float CurrentWeight { get; private set; } = 1f;

        public JumpState jumpState = JumpState.Grounded;
        private bool stopJump;
        /*internal new*/ public Collider2D collider2d;
        /*internal new*/ public AudioSource audioSource;
        public Health health;
        public bool controlEnabled = true;

        bool jump;
        Vector2 move;
        SpriteRenderer spriteRenderer;
        internal Animator animator;
        readonly PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        private InputAction m_MoveAction;
        private InputAction m_JumpAction;

        public Bounds Bounds => collider2d.bounds;

        void Awake()
        {
            health = GetComponent<Health>();
            audioSource = GetComponent<AudioSource>();
            collider2d = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();

            if (!clickToJump)
            {
                m_MoveAction = InputSystem.actions.FindAction("Player/Move");
                m_JumpAction = InputSystem.actions.FindAction("Player/Jump");
                m_MoveAction.Enable();
                m_JumpAction.Enable();
            }
        }

        protected override void Update()
        {
            if (controlEnabled)
            {
                if (clickToJump)
                {
                    UpdateClickMovement();
                }
                else
                {
                    move.x = m_MoveAction.ReadValue<Vector2>().x;
                    if (jumpState == JumpState.Grounded && m_JumpAction.WasPressedThisFrame())
                        jumpState = JumpState.PrepareToJump;
                    else if (m_JumpAction.WasReleasedThisFrame())
                    {
                        stopJump = true;
                        Schedule<PlayerStopJump>().player = this;
                    }
                }
            }
            else
            {
                move.x = 0;
            }
            UpdateJumpState();
            base.Update();
        }

        void UpdateClickMovement()
        {
            move.x = 0;
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame)
                return;

            var camera = Camera.main;
            if (camera == null) return;

            var screenPoint = pointer.position.ReadValue();
            var ray = camera.ScreenPointToRay(screenPoint);
            var gameplayPlane = new Plane(Vector3.forward,
                new Vector3(0f, 0f, transform.position.z));
            if (gameplayPlane.Raycast(ray, out var distance))
                JumpToward(ray.GetPoint(distance));
        }

        public void JumpToward(Vector2 worldPoint)
        {
            if (!controlEnabled) return;

            var delta = worldPoint - (Vector2)transform.position;
            var weightScale = Mathf.Sqrt(CurrentWeight);

            if (IsGrounded)
            {
                velocity.x = Mathf.Clamp(delta.x * 1.5f,
                    -clickHorizontalSpeed, clickHorizontalSpeed) / weightScale;
                velocity.y = (clickJumpSpeed +
                    Mathf.Clamp(delta.y * clickVerticalInfluence, -1.5f, 2.5f)) /
                    weightScale;
                jumpState = JumpState.Jumping;
                return;
            }

            var redirectAmount = midairRedirectStrength / CurrentWeight;
            var targetHorizontalSpeed = Mathf.Clamp(delta.x * 1.5f,
                -clickHorizontalSpeed, clickHorizontalSpeed) / weightScale;
            var targetVerticalSpeed = Mathf.Clamp(delta.y * clickVerticalInfluence,
                -clickJumpSpeed, clickJumpSpeed) / weightScale;

            velocity.x = Mathf.Lerp(velocity.x, targetHorizontalSpeed, redirectAmount);
            velocity.y = Mathf.Lerp(velocity.y, targetVerticalSpeed, redirectAmount * 0.45f);
            jumpState = JumpState.InFlight;
        }

        public void ApplyImpulse(Vector2 impulse)
        {
            velocity += impulse / CurrentWeight;
            jumpState = JumpState.Jumping;
        }

        public void SetCakeLayerCount(int layerCount)
        {
            var extraLayers = Mathf.Max(0, layerCount - 1);
            CurrentWeight = 1f + extraLayers * weightPerExtraLayer;
            gravityModifier = baseGravityModifier + extraLayers * gravityPerExtraLayer;
        }

        void UpdateJumpState()
        {
            jump = false;
            switch (jumpState)
            {
                case JumpState.PrepareToJump:
                    jumpState = JumpState.Jumping;
                    jump = true;
                    stopJump = false;
                    break;
                case JumpState.Jumping:
                    if (!IsGrounded)
                    {
                        Schedule<PlayerJumped>().player = this;
                        jumpState = JumpState.InFlight;
                    }
                    break;
                case JumpState.InFlight:
                    if (IsGrounded)
                    {
                        Schedule<PlayerLanded>().player = this;
                        jumpState = JumpState.Landed;
                    }
                    break;
                case JumpState.Landed:
                    jumpState = JumpState.Grounded;
                    break;
            }
        }

        protected override void ComputeVelocity()
        {
            if (clickToJump)
            {
                if (IsGrounded && jumpState == JumpState.Grounded)
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, groundFriction * Time.deltaTime);

                if (velocity.x > 0.01f)
                    spriteRenderer.flipX = false;
                else if (velocity.x < -0.01f)
                    spriteRenderer.flipX = true;

                animator.SetBool("grounded", IsGrounded);
                animator.SetFloat("velocityX", Mathf.Abs(velocity.x) / clickHorizontalSpeed);
                targetVelocity.x = velocity.x;
                return;
            }

            if (jump && IsGrounded)
            {
                velocity.y = jumpTakeOffSpeed * model.jumpModifier;
                jump = false;
            }
            else if (stopJump)
            {
                stopJump = false;
                if (velocity.y > 0)
                {
                    velocity.y = velocity.y * model.jumpDeceleration;
                }
            }

            if (move.x > 0.01f)
                spriteRenderer.flipX = false;
            else if (move.x < -0.01f)
                spriteRenderer.flipX = true;

            animator.SetBool("grounded", IsGrounded);
            animator.SetFloat("velocityX", Mathf.Abs(velocity.x) / maxSpeed);

            targetVelocity = move * maxSpeed;
        }

        public enum JumpState
        {
            Grounded,
            PrepareToJump,
            Jumping,
            InFlight,
            Landed
        }
    }
}