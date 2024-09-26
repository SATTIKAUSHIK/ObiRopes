using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class KnobController : MonoBehaviour
{
    public Transform interactorTransform; // The transform of the interactor (controller)
    public Transform cylinderTransform; // The transform of the cylinder to rotate
    float lastFrameAngle; // The angle from the last frame
    public float multiplier = 1; // Sensitivity multiplier for rotation

    private void Start()
    {
        XRGrabInteractable interactable = GetComponent<XRGrabInteractable>();
        interactable.selectEntered.AddListener(Selected);
        interactable.selectExited.AddListener(Deselected);
    }

    private void FixedUpdate()
    {
        if (interactorTransform != null)
        {
            // Calculate the angle between the interactor and the knob
            float angle = Vector3.SignedAngle(interactorTransform.position - transform.position, interactorTransform.forward, Vector3.up);
            float delta = angle - lastFrameAngle;

            // Rotate the cylinder based on the angle difference
            cylinderTransform.Rotate(cylinderTransform.up, delta * multiplier);
            lastFrameAngle = angle; // Update the last frame angle
        }
    }

    public void Selected(SelectEnterEventArgs arguments)
    {
        interactorTransform = arguments.interactorObject.transform; // Set the interactor transform
        lastFrameAngle = Vector3.SignedAngle(interactorTransform.position - transform.position, interactorTransform.forward, Vector3.up);
    }

    public void Deselected(SelectExitEventArgs arguments)
    {
        interactorTransform = null; // Clear the interactor transform
    }
}
