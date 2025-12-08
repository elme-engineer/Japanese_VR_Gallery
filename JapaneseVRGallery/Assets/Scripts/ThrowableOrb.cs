using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ThrowableOrb : MonoBehaviour
{
    [Header("Throw Settings")]
    [SerializeField] private float throwForce = 20f;
    [SerializeField] private float throwDelay = 0.1f;
    [SerializeField] private float throwCooldown = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private float throwSoundVolume = 0.7f;

    [Header("References")]
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Rigidbody rb;

    private bool canThrow = true;
    private XRBaseInteractor currentInteractor;

    private void Start()
    {

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();


        if (rb != null)
        {
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
            grabInteractable.activated.AddListener(OnTriggerPressed);
           // grabInteractable.deactivated.AddListener(OnTriggerReleased);
        }
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
            grabInteractable.activated.RemoveListener(OnTriggerPressed);
           // grabInteractable.deactivated.RemoveListener(OnTriggerReleased);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {

        currentInteractor = args.interactorObject as XRBaseInteractor;


        if (rb != null)
        {
            rb.isKinematic = false;
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {

        currentInteractor = null;


        if (rb != null)
        {
            rb.isKinematic = false;
        }
    }

    private void OnTriggerPressed(ActivateEventArgs args)
    {
        if (!canThrow || !grabInteractable.isSelected || currentInteractor == null)
            return;

        StartCoroutine(ThrowOrb(currentInteractor));
    }


    private IEnumerator ThrowOrb(XRBaseInteractor interactor)
    {
        if (interactor == null || rb == null)
            yield break;

        canThrow = false;


        if (throwSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(throwSound, throwSoundVolume);
        }


        Vector3 linearVelocity = rb.linearVelocity;
        Vector3 angularVelocity = rb.angularVelocity;


        if (grabInteractable.isSelected && interactor != null)
        {

            grabInteractable.enabled = false;


            if (interactor.hasSelection)
            {

                var controller = GetControllerFromInteractor(interactor);


                if (controller != null)
                {
                    controller.SendHapticImpulse(0.8f, 0.1f);
                }


                interactor.interactionManager.SelectExit(
                    interactor as IXRSelectInteractor,
                    grabInteractable as IXRSelectInteractable
                );
            }


            yield return new WaitForSeconds(throwDelay);


            grabInteractable.enabled = true;
        }


        Vector3 throwDirection = interactor.transform.forward;


        rb.AddForce(throwDirection * throwForce, ForceMode.VelocityChange);


        rb.AddForce(Vector3.up * (throwForce * 0.3f), ForceMode.VelocityChange);


        rb.linearVelocity = linearVelocity * 1.5f + throwDirection * throwForce;


        yield return new WaitForSeconds(throwCooldown);
        canThrow = true;
    }

    private ActionBasedController GetControllerFromInteractor(XRBaseInteractor interactor)
    {

        if (interactor is XRDirectInteractor directInteractor)
        {
            return directInteractor.GetComponentInParent<ActionBasedController>();
        }

        return null;
    }


    private void Update()
    {

    }

}