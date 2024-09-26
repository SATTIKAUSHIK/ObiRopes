using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class KnobStopper : MonoBehaviour
{
    private HingeJoint hingeJoint;
    private XRGrabInteractable grabInteractable;

    void Start()
    {
        hingeJoint = GetComponent<HingeJoint>();
        grabInteractable = GetComponent<XRGrabInteractable>();

        // Subscribe to interaction events
        grabInteractable.onSelectEntered.AddListener(EnableHinge);
        grabInteractable.onSelectExited.AddListener(DisableHinge);
    }

    private void EnableHinge(XRBaseInteractor interactor)
    {
        hingeJoint.useMotor = true;  // Enable the hinge joint's motor
        // Optionally, configure the motor's target velocity and force
        JointMotor motor = hingeJoint.motor;
        motor.targetVelocity = 100; // Set a target velocity
        motor.force = 100; // Set the force
        hingeJoint.motor = motor;
    }

    private void DisableHinge(XRBaseInteractor interactor)
    {
        hingeJoint.useMotor = false; // Disable the motor when interaction ends
    }

    void OnDestroy()
    {
        // Clean up event listeners
        grabInteractable.onSelectEntered.RemoveListener(EnableHinge);
        grabInteractable.onSelectExited.RemoveListener(DisableHinge);
    }
}