using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
[RequireComponent(typeof(PlayerInput))]
#endif
public class FirstPersonController : MonoBehaviour
{
   [Header("Player")]
   [Tooltip("Move speed of the character in m/s")]
   public float MoveSpeed = 4.0f;
   [Tooltip("Sprint speed of the character in m/s")]
   public float SprintSpeed = 6.0f;
   [Tooltip("Rotation speed of the character")]
   public float RotationSpeed = 1.0f;
   [Tooltip("Acceleration and deceleration")]
   public float SpeedChangeRate = 10.0f;

   [Space(10)]
   [Tooltip("The height the player can jump")]
   public float JumpHeight = 1.2f;
   [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
   public float Gravity = -15.0f;

   [Space(10)]
   [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
   public float JumpTimeout = 0.1f;
   [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
   public float FallTimeout = 0.15f;

   [Header("Player Grounded")]
   [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
   public bool Grounded = true;
   [Tooltip("Useful for rough ground")]
   public float GroundedOffset = -0.14f;
   [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
   public float GroundedRadius = 0.5f;
   [Tooltip("What layers the character uses as ground")]
   public LayerMask GroundLayers;

   [Header("Cinemachine")]
   [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
   public GameObject CinemachineCameraTarget;
   [Tooltip("How far in degrees can you move the camera up")]
   public float TopClamp = 90.0f;
   [Tooltip("How far in degrees can you move the camera down")]
   public float BottomClamp = -90.0f;
   [Tooltip("Time it takes for the camera to catch up to mouse input. Higher values are smoother.")]
   public float CameraSmoothTime = 0.12f;
       
   // --- NEW: Player Audio Settings ---
   [Header("Player Audio")]
   [Tooltip("How far (in meters) the enemy can hear you walk")]
   [SerializeField] private float walkNoiseRadius = 10f;
   [Tooltip("How far (in meters) the enemy can hear you sprint")]
   [SerializeField] private float sprintNoiseRadius = 25f;
   [Tooltip("How often to make a 'step' sound when walking")]
   [SerializeField] private float walkStepInterval = 0.6f;
   [Tooltip("How often to make a 'step' sound when sprinting")]
   [SerializeField] private float sprintStepInterval = 0.4f;

   public AudioSource AudioSource;

   // cinemachine
   private float _cinemachineTargetPitch;
   private float _cinemachineTargetYaw;
   private float _pitchVelocity;
   private float _yawVelocity;

   // player
   private float _speed;
   private float _rotationVelocity;
   private float _verticalVelocity;
   private float _terminalVelocity = 53.0f;

   // timeout deltatime
   private float _jumpTimeoutDelta;
   private float _fallTimeoutDelta;
       
   // --- NEW: Audio Timer ---
   private float _stepTimer;

    
#if ENABLE_INPUT_SYSTEM
   private PlayerInput _playerInput;
#endif
   private CharacterController _controller;
   private StarterAssetsInputs _input;
   private GameObject _mainCamera;

   private const float _threshold = 0.01f;

   private bool IsCurrentDeviceMouse
   {
      get
      {
#if ENABLE_INPUT_SYSTEM
         return _playerInput.currentControlScheme == "KeyboardMouse";
#else
             return false;
#endif
      }
   }

   private void Awake()
   {
      // get a reference to our main camera
      if (_mainCamera == null)
      {
         _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
      }
   }

   private void Start()
   {
      _controller = GetComponent<CharacterController>();
      _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
      _playerInput = GetComponent<PlayerInput>();
#else
          Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

      // reset our timeouts on start
      _jumpTimeoutDelta = JumpTimeout;
      _fallTimeoutDelta = FallTimeout;
          
      // Initialize camera rotation targets
      _cinemachineTargetYaw = transform.rotation.eulerAngles.y;
      _cinemachineTargetPitch = CinemachineCameraTarget.transform.localRotation.eulerAngles.x;
   }

   private void Update()
   {
      JumpAndGravity();
      GroundedCheck();
      Move();
          
      // --- NEW: Call HandleSounds() every frame ---
      HandleSounds();
   }

   private void LateUpdate()
   {
      CameraRotation();
   }

   private void GroundedCheck()
   {
      // set sphere position, with offset
      Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
      Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
   }

   private void CameraRotation()
   {
      // if there is an input and the cursor is locked
      if (_input.look.sqrMagnitude >= _threshold)
      {
         //Don't multiply mouse input by Time.deltaTime
         float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
             
         // Update the target yaw and pitch based on input
         _cinemachineTargetYaw += _input.look.x * RotationSpeed * deltaTimeMultiplier;
         _cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
      }

      // Clamp our pitch and yaw rotations
      _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
          
      // Smooth the camera rotation
      float currentYaw = transform.eulerAngles.y;
      float currentPitch = CinemachineCameraTarget.transform.localRotation.eulerAngles.x;

      // Use SmoothDampAngle to smoothly change the current rotation towards the target
      float smoothedYaw = Mathf.SmoothDampAngle(currentYaw, _cinemachineTargetYaw, ref _yawVelocity, CameraSmoothTime);
      float smoothedPitch = Mathf.SmoothDampAngle(currentPitch, _cinemachineTargetPitch, ref _pitchVelocity, CameraSmoothTime);

      // Apply the smoothed rotation to the player (for left/right) and the camera target (for up/down)
      transform.rotation = Quaternion.Euler(0.0f, smoothedYaw, 0.0f);
      CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(smoothedPitch, 0.0f, 0.0f);
   }

   private void Move()
   {
      // set target speed based on move speed, sprint speed and if sprint is pressed
      float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

      // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

      // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
      // if there is no input, set the target speed to 0
      if (_input.move == Vector2.zero) targetSpeed = 0.0f;

      // a reference to the players current horizontal velocity
      float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

      float speedOffset = 0.1f;
      float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

      // accelerate or decelerate to target speed
      if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
      {
         // creates curved result rather than a linear one giving a more organic speed change
         // note T in Lerp is clamped, so we don't need to clamp our speed
         _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

         // round speed to 3 decimal places
         _speed = Mathf.Round(_speed * 1000f) / 1000f;
      }
      else
      {
         _speed = targetSpeed;
      }

      // normalise input direction
      Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

      // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
      // if there is a move input rotate player when the player is moving
      if (_input.move != Vector2.zero)
      {
         // move
         inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
      }

      // move the player
      _controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
   }

   private void JumpAndGravity()
   {
      if (Grounded)
      {
         // reset the fall timeout timer
         _fallTimeoutDelta = FallTimeout;

         // stop our velocity dropping infinitely when grounded
         if (_verticalVelocity < 0.0f)
         {
            _verticalVelocity = -2f;
         }

         // Jump
         if (_input.jump && _jumpTimeoutDelta <= 0.0f)
         {
            // the square root of H * -2 * G = how much velocity needed to reach desired height
            _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
         }

         // jump timeout
         if (_jumpTimeoutDelta >= 0.0f)
         {
            _jumpTimeoutDelta -= Time.deltaTime;
         }
      }
      else
      {
         // reset the jump timeout timer
         _jumpTimeoutDelta = JumpTimeout;

         // fall timeout
         if (_fallTimeoutDelta >= 0.0f)
         {
            _fallTimeoutDelta -= Time.deltaTime;
         }

         // if we are not grounded, do not jump
         _input.jump = false;
      }

      // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
      if (_verticalVelocity < _terminalVelocity)
      {
         _verticalVelocity += Gravity * Time.deltaTime;
      }
   }
       
   // --- NEW: HandleSounds() function ---
   private void HandleSounds()
   {
      // Get the *current* horizontal velocity
      float horizontalVelocity = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

      // Check if the player is on the ground and moving
      if (Grounded && horizontalVelocity > 0.1f)
      {
         _stepTimer -= Time.deltaTime;

         if (_stepTimer <= 0.0f)
         {
            // Use the script's '_input' helper
            if (_input.sprint)
            {
               NoiseManager.MakeNoise(transform.position, sprintNoiseRadius);
               AudioSource.Play();
               _stepTimer = sprintStepInterval;
            }
            else
            {
               NoiseManager.MakeNoise(transform.position, walkNoiseRadius);
               AudioSource.Play();
               _stepTimer = walkStepInterval;
            }
         }
      }
      else
      {
         // Not moving or in the air.
         // Reset the timer so the *next* step is immediate
         _stepTimer = 0.0f;
      }
   }
   // --- END OF NEW ---

   private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
   {
      if (lfAngle < -360f) lfAngle += 360f;
      if (lfAngle > 360f) lfAngle -= 360f;
      return Mathf.Clamp(lfAngle, lfMin, lfMax);
   }

   private void OnDrawGizmosSelected()
   {
      Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
      Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

      if (Grounded) Gizmos.color = transparentGreen;
      else Gizmos.color = transparentRed;

      // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
      Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
   }
}