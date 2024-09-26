using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace UnityEngine.XR.Content.Interaction
{
    public class XRKnobLever : XRBaseInteractable
    {
        const float k_LeverDeadZone = 0.1f;

        [SerializeField]
        [Tooltip("The object that is visually grabbed and manipulated")]
        Transform m_Handle = null;

        [SerializeField]
        [Tooltip("The value of the lever")]
        bool m_Value = false;

        [SerializeField]
        [Tooltip("If enabled, the lever will snap to the value position when released")]
        bool m_LockToValue;

        [SerializeField]
        [Tooltip("Angle of the lever in the 'on' position")]
        [Range(-90.0f, 90.0f)]
        float m_MaxAngle = 90.0f;

        [SerializeField]
        [Tooltip("Angle of the lever in the 'off' position")]
        [Range(-90.0f, 90.0f)]
        float m_MinAngle = -90.0f;

        [SerializeField]
        [Tooltip("Speed at which the lever returns to its target angle")]
        float m_DampingSpeed = 5.0f;

        [SerializeField]
        [Tooltip("Events to trigger when the lever activates")]
        UnityEvent m_OnLeverActivate = new UnityEvent();

        [SerializeField]
        [Tooltip("Events to trigger when the lever deactivates")]
        UnityEvent m_OnLeverDeactivate = new UnityEvent();

        IXRSelectInteractor m_Interactor;
        bool m_IsColliding = false;

        float m_CurrentAngle;
        bool m_IsReturningToRest;
        Vector3 m_InitialControllerPosition;

        public Transform handle
        {
            get => m_Handle;
            set => m_Handle = value;
        }

        public bool value
        {
            get => m_Value;
            set => SetValue(value, true);
        }

        public bool lockToValue { get; set; }

        public float maxAngle
        {
            get => m_MaxAngle;
            set => m_MaxAngle = value;
        }

        public float minAngle
        {
            get => m_MinAngle;
            set => m_MinAngle = value;
        }

        public UnityEvent onLeverActivate => m_OnLeverActivate;

        public UnityEvent onLeverDeactivate => m_OnLeverDeactivate;

        void Start()
        {
            SetValue(m_Value, true);
            m_CurrentAngle = m_Value ? m_MaxAngle : m_MinAngle;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            selectEntered.AddListener(StartGrab);
            selectExited.AddListener(EndGrab);
        }

        protected override void OnDisable()
        {
            selectEntered.RemoveListener(StartGrab);
            selectExited.RemoveListener(EndGrab);
            base.OnDisable();
        }

        void StartGrab(SelectEnterEventArgs args)
        {
            if (args.interactorObject.transform.CompareTag("Controller"))
            {
                m_Interactor = args.interactorObject;
                m_IsReturningToRest = false;
                m_InitialControllerPosition = m_Interactor.GetAttachTransform(this).position; // Store initial position
            }
        }

        void EndGrab(SelectExitEventArgs args)
        {
            if (args.interactorObject.transform.CompareTag("Controller"))
            {
                //m_IsReturningToRest = true;  Start returning to rest when released
                m_Interactor = null;
            }
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic && m_IsColliding)
            {
                if (isSelected && m_Interactor != null)
                {
                    UpdateValue();
                }
                else if (m_IsReturningToRest)
                {
                    ReturnToRestPosition();
                }
            }
        }

        // Variable for tracking the velocity of the damping change
        float dampingSpeedVelocity = 0.0f;

        void UpdateValue()
        {
            if (m_Interactor == null) return;

            Vector3 currentControllerPosition = m_Interactor.GetAttachTransform(this).position;

            // Check if the controller has moved since grabbing
            float controllerDistance = Vector3.Distance(currentControllerPosition, m_InitialControllerPosition);
            if (controllerDistance > k_LeverDeadZone)
            {
                // Get the current rotation of the controller
                var controllerRotation = m_Interactor.GetAttachTransform(this).rotation;

                // Align the rotation to the lever’s local space
                Quaternion localRotation = Quaternion.Inverse(transform.rotation) * controllerRotation;

                // Extract the Z angle of the rotation
                float targetAngle = localRotation.eulerAngles.z;

                // Adjust the angle to be within the limits
                if (targetAngle > 180.0f) targetAngle -= 360.0f; // Convert from 0-360 to -180 to 180 range
                targetAngle = Mathf.Clamp(targetAngle, m_MinAngle, m_MaxAngle);

                // Dynamically update damping speed based on controller movement, but use smooth damp to prevent jitter
                float targetDampingSpeed = Mathf.Lerp(2.0f, 10.0f, Mathf.InverseLerp(0, 1, controllerDistance));

                // Use SmoothDamp to smoothly adjust damping speed over time
                m_DampingSpeed = Mathf.SmoothDamp(m_DampingSpeed, targetDampingSpeed, ref dampingSpeedVelocity, 0.1f);

                // Smoothly update the lever's angle
                m_CurrentAngle = Mathf.Lerp(m_CurrentAngle, targetAngle, Time.deltaTime * m_DampingSpeed);

                // Apply the angle to the handle
                SetHandleAngle(m_CurrentAngle);

                // Update the lever's value state
                bool newValue = Mathf.Abs(m_MaxAngle - m_CurrentAngle) < Mathf.Abs(m_MinAngle - m_CurrentAngle);
                SetValue(newValue);
            }
        }

        void ReturnToRestPosition()
        {
            float targetAngle = m_Value ? m_MaxAngle : m_MinAngle;

            // Smoothly interpolate back to the target angle
            m_CurrentAngle = Mathf.Lerp(m_CurrentAngle, targetAngle, Time.deltaTime * m_DampingSpeed);
            SetHandleAngle(m_CurrentAngle);

            // Stop returning when the angle is near the target
            if (Mathf.Abs(m_CurrentAngle - targetAngle) < 0.1f)
            {
                m_IsReturningToRest = false;
                SetHandleAngle(targetAngle);
            }
        }

        void SetValue(bool isOn, bool forceRotation = false)
        {
            if (m_Value == isOn)
            {
                if (forceRotation)
                    SetHandleAngle(m_Value ? m_MaxAngle : m_MinAngle);

                return;
            }

            m_Value = isOn;

            if (m_Value)
            {
                m_OnLeverActivate.Invoke();
            }
            else
            {
                m_OnLeverDeactivate.Invoke();
            }

            if (!isSelected && (m_LockToValue || forceRotation))
                SetHandleAngle(m_Value ? m_MaxAngle : m_MinAngle);
        }

        void SetHandleAngle(float angle)
        {
            if (m_Handle != null)
                m_Handle.localRotation = Quaternion.Euler(0.0f, angle, 0.0f);
        }

        void OnDrawGizmosSelected()
        {
            var angleStartPoint = transform.position;

            if (m_Handle != null)
                angleStartPoint = m_Handle.position;

            const float k_AngleLength = 0.25f;

            var angleMaxPoint = angleStartPoint + transform.TransformDirection(Quaternion.Euler(m_MaxAngle, 0.0f, 0.0f) * Vector3.up) * k_AngleLength;
            var angleMinPoint = angleStartPoint + transform.TransformDirection(Quaternion.Euler(m_MinAngle, 0.0f, 0.0f) * Vector3.up) * k_AngleLength;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(angleStartPoint, angleMaxPoint);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(angleStartPoint, angleMinPoint);
        }

        void OnValidate()
        {
            SetHandleAngle(m_Value ? m_MaxAngle : m_MinAngle);
        }

        void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Controller"))
            {
                m_IsColliding = true;
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Controller"))
            {
                m_IsColliding = false;
            }
        }
    }
}
