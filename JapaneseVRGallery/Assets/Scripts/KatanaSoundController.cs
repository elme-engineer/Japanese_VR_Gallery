using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody), typeof(AudioSource))]
public class KatanaSoundController : MonoBehaviour
{
    [Header("Sound Settings")]
    [SerializeField] private AudioClip slashSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private float slashVolume = 0.7f;
    [SerializeField] private float hitVolume = 0.8f;

    [Header("Slash Detection")]
    [SerializeField] private float slashVelocityThreshold = 2.0f;
    [SerializeField] private float minSlashCooldown = 0.3f;
    [SerializeField] private float slashAngleThreshold = 30f; // Minimum angle change for slash

    [Header("Hit Detection")]
    [SerializeField] private float minHitVelocity = 1.0f;
    [SerializeField] private float hitSoundCooldown = 0.1f;

    [Header("Two-Handed Mode")]
    [SerializeField] private Transform secondHandGrabPoint;
    [SerializeField] private float twoHandedForceMultiplier = 1.5f;

    // Components
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private AudioSource audioSource;
    private AudioSource hitAudioSource; // Separate audio source for hits

    // Slash tracking
    private Vector3 lastPosition;
    private Vector3 lastVelocity;
    private Quaternion lastRotation;
    private float lastSlashTime;
    private float slashCooldownTimer;
    private bool canSlash = true;

    // Hit tracking
    private float lastHitTime;
    private bool canPlayHitSound = true;

    // Two-handed tracking
    private IXRSelectInteractor primaryHand;
    private IXRSelectInteractor secondaryHand;
    private bool isTwoHanded = false;

    // Velocity buffer for smooth calculations
    private Vector3[] velocityBuffer = new Vector3[3];
    private int velocityIndex = 0;

    private void Start()
    {
        // Get components
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        // Setup main audio source for slash sounds
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.spatialBlend = 1.0f;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 20f;

        // Setup separate audio source for hit sounds
        hitAudioSource = gameObject.AddComponent<AudioSource>();
        hitAudioSource.spatialBlend = 1.0f;
        hitAudioSource.minDistance = 1f;
        hitAudioSource.maxDistance = 20f;
        hitAudioSource.playOnAwake = false;

        // Initialize velocity buffer
        for (int i = 0; i < velocityBuffer.Length; i++)
            velocityBuffer[i] = Vector3.zero;

        // Subscribe to events
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);

        // Initialize tracking variables
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastSlashTime = Time.time;
    }

    private void Update()
    {
        if (grabInteractable.isSelected)
        {
            UpdateSlashDetection();
        }

        // Update cooldowns
        slashCooldownTimer -= Time.deltaTime;
        if (slashCooldownTimer <= 0)
            canSlash = true;
    }

    private void FixedUpdate()
    {
        // Update velocity buffer for smooth velocity calculations
        if (grabInteractable.isSelected)
        {
            velocityBuffer[velocityIndex] = rb.linearVelocity;
            velocityIndex = (velocityIndex + 1) % velocityBuffer.Length;
        }

        // Store last velocity for comparison
        lastVelocity = rb.linearVelocity;
    }

    private void UpdateSlashDetection()
    {
        if (!canSlash || !grabInteractable.isSelected)
            return;

        // Calculate smooth velocity from buffer
        Vector3 smoothVelocity = Vector3.zero;
        foreach (Vector3 vel in velocityBuffer)
            smoothVelocity += vel;
        smoothVelocity /= velocityBuffer.Length;

        // Calculate speed
        float currentSpeed = smoothVelocity.magnitude;

        // Calculate angular velocity (swing detection)
        Quaternion rotationDelta = transform.rotation * Quaternion.Inverse(lastRotation);
        float angle;
        Vector3 axis;
        rotationDelta.ToAngleAxis(out angle, out axis);

        // Check for slash conditions
        bool hasMinimumSpeed = currentSpeed > slashVelocityThreshold;
        bool hasMinimumAngle = angle > slashAngleThreshold;
        bool isAccelerating = currentSpeed > lastVelocity.magnitude * 1.2f;

        if (hasMinimumSpeed && hasMinimumAngle && isAccelerating)
        {
            PlaySlashSound(currentSpeed);
            slashCooldownTimer = minSlashCooldown;
            canSlash = false;
        }

        // Update tracking variables
        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    private void PlaySlashSound(float speed)
    {
        if (slashSound == null) return;

        // Adjust pitch based on speed
        float speedRatio = Mathf.Clamp(speed / (slashVelocityThreshold * 2), 0.5f, 1.5f);
        audioSource.pitch = speedRatio;

        // Adjust volume based on speed
        float volume = slashVolume * Mathf.Clamp(speed / (slashVelocityThreshold * 1.5f), 0.5f, 1f);

        // Play slash sound
        audioSource.PlayOneShot(slashSound, volume);

        // Optional: Haptic feedback
        if (primaryHand is XRBaseInteractor primaryInteractor)
        {
            SendHapticImpulse(primaryInteractor, volume * 0.7f, 0.1f);
        }

        if (isTwoHanded && secondaryHand is XRBaseInteractor secondaryInteractor)
        {
            SendHapticImpulse(secondaryInteractor, volume * 0.5f, 0.1f);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!canPlayHitSound) return;

        // Calculate impact velocity
        float impactVelocity = collision.relativeVelocity.magnitude;

        // Only play hit sound for significant impacts
        if (impactVelocity > minHitVelocity)
        {
            PlayHitSound(impactVelocity, collision);
            lastHitTime = Time.time;
            canPlayHitSound = false;

            // Start cooldown coroutine
            StartCoroutine(HitSoundCooldown());
        }
    }

    private void PlayHitSound(float impactVelocity, Collision collision)
    {
        if (hitSound == null) return;

        // Adjust pitch based on impact velocity
        float velocityRatio = Mathf.Clamp(impactVelocity / (minHitVelocity * 2), 0.8f, 1.5f);
        hitAudioSource.pitch = velocityRatio;

        // Adjust volume based on impact velocity
        float volume = hitVolume * Mathf.Clamp(impactVelocity / (minHitVelocity * 1.5f), 0.5f, 1f);

        // Play hit sound
        hitAudioSource.PlayOneShot(hitSound, volume);

        // Optional: More intense haptic feedback for hits
        if (primaryHand is XRBaseInteractor primaryInteractor)
        {
            SendHapticImpulse(primaryInteractor, volume, 0.15f);
        }

        if (isTwoHanded && secondaryHand is XRBaseInteractor secondaryInteractor)
        {
            SendHapticImpulse(secondaryInteractor, volume * 0.7f, 0.15f);
        }
    }

    private System.Collections.IEnumerator HitSoundCooldown()
    {
        yield return new WaitForSeconds(hitSoundCooldown);
        canPlayHitSound = true;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (primaryHand == null)
        {
            primaryHand = args.interactorObject;
        }
        else if (secondaryHand == null)
        {
            secondaryHand = args.interactorObject;
            isTwoHanded = true;

            // Optional: Adjust physics for two-handed grip
            if (rb != null)
            {
                rb.mass *= 0.7f; // Make it feel lighter with two hands
            }
        }

        // Reset slash detection
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastSlashTime = Time.time;

        // Clear velocity buffer
        for (int i = 0; i < velocityBuffer.Length; i++)
            velocityBuffer[i] = Vector3.zero;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (args.interactorObject == primaryHand)
        {
            primaryHand = secondaryHand;
            secondaryHand = null;
        }
        else if (args.interactorObject == secondaryHand)
        {
            secondaryHand = null;
        }

        isTwoHanded = (primaryHand != null && secondaryHand != null);

        // Reset physics if not two-handed anymore
        if (!isTwoHanded && rb != null)
        {
            // Restore original mass (you might want to store the original value)
            rb.mass = 1.0f;
        }

        // If no hands are holding, reset everything
        if (primaryHand == null)
        {
            isTwoHanded = false;
        }
    }

    private void SendHapticImpulse(XRBaseInteractor interactor, float amplitude, float duration)
    {
        // Try to get the controller for haptic feedback
        var controller = GetControllerFromInteractor(interactor);
        if (controller != null)
        {
            controller.SendHapticImpulse(amplitude, duration);
        }
    }

    private ActionBasedController GetControllerFromInteractor(XRBaseInteractor interactor)
    {
        if (interactor is XRDirectInteractor directInteractor)
        {
            return directInteractor.GetComponentInParent<ActionBasedController>();
        }
        return interactor.GetComponent<ActionBasedController>();
    }

    // Optional: Debug visualization
    private void OnDrawGizmos()
    {
        if (grabInteractable != null && grabInteractable.isSelected)
        {
            // Draw velocity vector
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, rb.linearVelocity.normalized * 2f);

            // Draw forward vector
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
        }
    }

    // Public method to manually trigger slash (for testing)
    public void ManualSlash()
    {
        PlaySlashSound(slashVelocityThreshold * 1.5f);
    }
}