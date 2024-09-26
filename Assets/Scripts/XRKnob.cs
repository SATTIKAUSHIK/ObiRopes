using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace UnityEngine.XR.Content.Interaction
{
    public class XRKnob : XRBaseInteractable
    {
        const float k_ModeSwitchDeadZone = 0.1f;
        const float k_SmoothingFactor = 0.1f; // Smoothing factor for position tracking

        struct TrackedRotation
        {
            float m_BaseAngle;
            float m_CurrentOffset;
            float m_AccumulatedAngle;

            public float totalOffset => m_AccumulatedAngle + m_CurrentOffset;

            public void Reset()
            {
                m_BaseAngle = 0.0f;
                m_CurrentOffset = 0.0f;
                m_AccumulatedAngle = 0.0f;
            }

            public void SetBaseFromVector(Vector3 direction)
            {
                m_AccumulatedAngle += m_CurrentOffset;
                m_BaseAngle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
                m_CurrentOffset = 0.0f;
            }

            public void SetTargetFromVector(Vector3 direction)
            {
                var targetAngle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
                m_CurrentOffset = ShortestAngleDistance(m_BaseAngle, targetAngle, 360.0f);

                if (Mathf.Abs(m_CurrentOffset) > 90.0f)
                {
                    m_BaseAngle = targetAngle;
                    m_AccumulatedAngle += m_CurrentOffset;
                    m_CurrentOffset = 0.0f;
                }
            }
        }

        [Serializable]
        public class ValueChangeEvent : UnityEvent<float> { }

        [SerializeField]
        Transform m_Handle = null;
        [SerializeField]
        [Range(0.0f, 1.0f)]
        float m_Value = 0.5f;
        [SerializeField]
        bool m_ClampedMotion = true;
        [SerializeField]
        float m_MaxAngle = 90.0f;
        [SerializeField]
        float m_MinAngle = -90.0f;
        [SerializeField]
        float m_AngleIncrement = 0.0f;
        [SerializeField]
        float m_PositionTrackedRadius = 0.1f;
        [SerializeField]
        float m_TwistSensitivity = 1.5f;
        [SerializeField]
        ValueChangeEvent m_OnValueChange = new ValueChangeEvent();

        IXRSelectInteractor m_Interactor;
        bool m_PositionDriven = false;
        bool m_UpVectorDriven = false;
        bool m_IsColliding = false;

        TrackedRotation m_PositionAngles = new TrackedRotation();
        TrackedRotation m_UpVectorAngles = new TrackedRotation();
        TrackedRotation m_ForwardVectorAngles = new TrackedRotation();

        float m_BaseKnobRotation = 0.0f;
        Vector3 m_LastInteractorPosition;

        public Transform handle { get => m_Handle; set => m_Handle = value; }
        public float value
        {
            get => m_Value;
            set
            {
                SetValue(value);
                SetKnobRotation(ValueToRotation());
            }
        }
        public bool clampedMotion { get => m_ClampedMotion; set => m_ClampedMotion = value; }
        public float maxAngle { get => m_MaxAngle; set => m_MaxAngle = value; }
        public float minAngle { get => m_MinAngle; set => m_MinAngle = value; }
        public float positionTrackedRadius { get => m_PositionTrackedRadius; set => m_PositionTrackedRadius = value; }
        public ValueChangeEvent onValueChange => m_OnValueChange;

        void Start()
        {
            SetValue(m_Value);
            SetKnobRotation(ValueToRotation());
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
            m_Interactor = args.interactorObject;

            m_PositionAngles.Reset();
            m_UpVectorAngles.Reset();
            m_ForwardVectorAngles.Reset();

            UpdateBaseKnobRotation();
            UpdateRotation(true);
        }

        void EndGrab(SelectExitEventArgs args)
        {
            m_Interactor = null;
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic && m_IsColliding)
            {
                if (isSelected)
                {
                    UpdateRotation();
                }
            }
        }

        void UpdateRotation(bool freshCheck = false)
        {
            if (m_Interactor == null) return;

            var interactorTransform = m_Interactor.GetAttachTransform(this);
            var interactorPosition = interactorTransform.position;

            // Smooth position tracking
            interactorPosition = SmoothPosition(interactorPosition, m_LastInteractorPosition);
            m_LastInteractorPosition = interactorPosition;

            // Calculate direction to the knob from the controller
            var localOffset = transform.InverseTransformVector(interactorPosition - m_Handle.position);
            localOffset.y = 0.0f; // Keep the Y component zero
            var radiusOffset = transform.TransformVector(localOffset).magnitude;
            localOffset.Normalize();

            // Use the controller's forward vector instead of wrist forward
            var localForward = transform.InverseTransformDirection(interactorTransform.forward);
            var localY = Math.Abs(localForward.y);
            localForward.y = 0.0f; // Flatten the forward vector
            localForward.Normalize();

            // Use the controller's up vector instead of wrist up
            var localUp = transform.InverseTransformDirection(interactorTransform.up);
            localUp.y = 0.0f; // Flatten the up vector
            localUp.Normalize();

            // Update Position Driven Logic
            if (m_PositionDriven && !freshCheck)
                radiusOffset *= (1.0f + k_ModeSwitchDeadZone);

            if (radiusOffset >= m_PositionTrackedRadius)
            {
                if (!m_PositionDriven || freshCheck)
                {
                    m_PositionAngles.SetBaseFromVector(localOffset);
                    m_PositionDriven = true;
                }
            }
            else
                m_PositionDriven = false;

            // Determine Up Vector Driven Logic
            if (!freshCheck)
            {
                if (!m_UpVectorDriven)
                    localY *= (1.0f - (k_ModeSwitchDeadZone * 0.5f));
                else
                    localY *= (1.0f + (k_ModeSwitchDeadZone * 0.5f));
            }

            if (localY > 0.707f)
            {
                if (!m_UpVectorDriven || freshCheck)
                {
                    m_UpVectorAngles.SetBaseFromVector(localUp);
                    m_UpVectorDriven = true;
                }
            }
            else
            {
                if (m_UpVectorDriven || freshCheck)
                {
                    m_ForwardVectorAngles.SetBaseFromVector(localForward);
                    m_UpVectorDriven = false;
                }
            }

            // Update the angles based on the derived vectors
            if (m_PositionDriven)
                m_PositionAngles.SetTargetFromVector(localOffset);

            if (m_UpVectorDriven)
                m_UpVectorAngles.SetTargetFromVector(localUp);
            else
                m_ForwardVectorAngles.SetTargetFromVector(localForward);

            // Calculate knob rotation
            var knobRotation = m_BaseKnobRotation - ((m_UpVectorAngles.totalOffset + m_ForwardVectorAngles.totalOffset) * m_TwistSensitivity) - m_PositionAngles.totalOffset;

            // Clamp motion if necessary
            if (m_ClampedMotion)
                knobRotation = Mathf.Clamp(knobRotation, m_MinAngle, m_MaxAngle);

            SetKnobRotation(knobRotation);

            // Calculate knob value based on rotation
            var knobValue = (knobRotation - m_MinAngle) / (m_MaxAngle - m_MinAngle);
            SetValue(knobValue);
        }


        Vector3 SmoothPosition(Vector3 newPosition, Vector3 currentSmoothedPosition)
        {
            return Vector3.Lerp(currentSmoothedPosition, newPosition, k_SmoothingFactor);
        }

        void SetKnobRotation(float angle)
        {
            if (m_AngleIncrement > 0)
            {
                var normalizeAngle = angle - m_MinAngle;
                angle = (Mathf.Round(normalizeAngle / m_AngleIncrement) * m_AngleIncrement) + m_MinAngle;
            }

            if (m_Handle != null)
                m_Handle.localEulerAngles = new Vector3(0.0f, angle, 0.0f);
        }

        void SetValue(float value)
        {
            if (m_ClampedMotion)
                value = Mathf.Clamp01(value);

            if (m_AngleIncrement > 0)
            {
                var angleRange = m_MaxAngle - m_MinAngle;
                var angle = Mathf.Lerp(0.0f, angleRange, value);
                angle = Mathf.Round(angle / m_AngleIncrement) * m_AngleIncrement;
                value = Mathf.InverseLerp(0.0f, angleRange, angle);
            }

            m_Value = value;
            m_OnValueChange.Invoke(m_Value);
        }

        float ValueToRotation()
        {
            return m_ClampedMotion ? Mathf.Lerp(m_MinAngle, m_MaxAngle, m_Value) : Mathf.LerpUnclamped(m_MinAngle, m_MaxAngle, m_Value);
        }

        void UpdateBaseKnobRotation()
        {
            m_BaseKnobRotation = Mathf.LerpUnclamped(m_MinAngle, m_MaxAngle, m_Value);
        }

        static float ShortestAngleDistance(float start, float end, float max)
        {
            var angleDelta = end - start;
            var angleSign = Mathf.Sign(angleDelta);
            angleDelta = Math.Abs(angleDelta) % max;
            if (angleDelta > (max * 0.5f))
                angleDelta = -(max - angleDelta);
            return angleDelta * angleSign;
        }

        void OnDrawGizmosSelected()
        {
            const int k_CircleSegments = 16;
            const float k_SegmentRatio = 1.0f / k_CircleSegments;

            if (m_PositionTrackedRadius <= Mathf.Epsilon)
                return;

            var circleCenter = transform.position;

            if (m_Handle != null)
                circleCenter = m_Handle.position;

            var circleX = transform.right;
            var circleY = transform.forward;

            Gizmos.color = Color.green;
            var segmentCounter = 0;
            while (segmentCounter < k_CircleSegments)
            {
                var startAngle = (float)segmentCounter * k_SegmentRatio * 2.0f * Mathf.PI;
                segmentCounter++;
                var endAngle = (float)segmentCounter * k_SegmentRatio * 2.0f * Mathf.PI;

                Gizmos.DrawLine(circleCenter + (Mathf.Cos(startAngle) * circleX + Mathf.Sin(startAngle) * circleY) * m_PositionTrackedRadius,
                                circleCenter + (Mathf.Cos(endAngle) * circleX + Mathf.Sin(endAngle) * circleY) * m_PositionTrackedRadius);
            }
        }

        void OnValidate()
        {
            if (m_ClampedMotion)
                m_Value = Mathf.Clamp01(m_Value);

            if (m_MinAngle > m_MaxAngle)
                m_MinAngle = m_MaxAngle;

            SetKnobRotation(ValueToRotation());
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
