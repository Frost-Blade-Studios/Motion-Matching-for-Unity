// Copyright © 2017-2024 Vault Break Studios Pty Ltd

using UnityEngine;
using UnityEngine.Events;
using MxMGameplay;

namespace MxM
{
    [System.Serializable]
    public class DeltaTimeEvent : UnityEvent<float> { }

    public class MxMAnimationDecoupler : MonoBehaviour
    {
        [Header("Decouple Settings")]
        [SerializeField]
        private float m_maxDecouple = 0.3f;

        //[SerializeField]
        //private float m_maxAngleDecouple = 180f;

        [SerializeField]
        private EDecoupleMode m_decoupleMode = EDecoupleMode.Decoupled;

        [Header("Root Motion Blending")]

        [SerializeField]
        private bool m_rootMotionOverride;

        [SerializeField]
        protected bool p_rootMotionOnNoInput = false;

        [SerializeField]
        protected bool p_useRootMotionBlending = false;

        [SerializeField]
        protected float p_minAngleToBlendRootMotion = 15f;

        [SerializeField]
        protected float p_rootMotionPercent = 0.75f;

        [SerializeField]
        protected AnimationCurve p_rootMotionCurve;

        [Header("Gravity")]
        [SerializeField]
        private bool m_enableGravity = true;

        [SerializeField]
        private bool m_fixedUpdateController = false;

        [Header("Movement Logic")]
        [SerializeField]
        private DeltaTimeEvent m_onUpdateMoveLogic = null;

        protected float m_verticalSpeed;

        private Transform m_modelTransform;
        private Animator m_animator;

        private Vector3 m_modelPos;
        private Quaternion m_modelRot;

        private bool m_fixedUpdate;

        private GenericControllerWrapper m_controllerWrapper;
        private MxMAnimator m_mxmAnimator;

        public bool RootMotionOverride { get { return m_rootMotionOverride; } set { m_rootMotionOverride = value; } }
        public float MaxDecouple { get { return m_maxDecouple; } set { m_maxDecouple = value; } }
        public bool EnableGravity { get { return m_enableGravity; } set { m_enableGravity = value; } }

        public enum EDecoupleMode
        {
            Decoupled,
            LockRotation,
            LockPosition,
            LockAll,
        }

        private void Start()
        {
            m_maxDecouple = Mathf.Clamp(Mathf.Abs(m_maxDecouple), 0f, float.MaxValue);

            m_controllerWrapper = GetComponent<GenericControllerWrapper>();

            m_mxmAnimator = GetComponentInChildren<MxMAnimator>();

            if(m_mxmAnimator != null)
            {
                m_modelTransform = m_mxmAnimator.transform;
                m_animator = m_mxmAnimator.GetComponent<Animator>();
            }
            else
            {
                Debug.LogError("MxMAnimationDecoupler: Cannot find child component with MxMAnimator attached. Decoupler disabled");
                enabled = false;
                return;
            }

            Animator animator = GetComponentInChildren<Animator>();
            if(animator != null)
            {
#if UNITY_6_1_OR_NEWER
                m_fixedUpdate = animator.updateMode == AnimatorUpdateMode.Fixed;
#else
                m_fixedUpdate = animator.updateMode == AnimatorUpdateMode.AnimatePhysics;
#endif
            }
            else
            {
                Debug.LogError("MxMAnimationDecoupler: Cannot find child component with Animator attached. Decoupler disabled");
                enabled = false;
                return;
            }

            m_modelPos = m_modelTransform.position;
            m_modelRot = m_modelTransform.rotation;
        }

        public void UpdatePhase1(float a_deltaTime)
        {
            m_modelPos = m_modelTransform.position;
            m_modelRot = m_modelTransform.rotation;

            //Update the position of the character controller
            m_onUpdateMoveLogic.Invoke(a_deltaTime);
        }

        private void FixedUpdate()
        {
            if (m_fixedUpdate)
                UpdatePhase2();
        }

        private void Update()
        {
            if (!m_fixedUpdate && m_fixedUpdateController)
                UpdatePhase2();
        }

        private void LateUpdate()
        {
            if (!m_fixedUpdate && !m_fixedUpdateController)
                UpdatePhase2();
        }

        private void UpdatePhase2()
        {
            //if(m_rootMotionOverride)
            //{
            //    m_controllerWrapper.SetPositionAndRotation(m_modelPos, m_modelRot);
            //    m_modelTransform.SetPositionAndRotation(m_modelPos, m_modelRot);
            //}

            Vector3 controllerPosition = transform.position;
            Quaternion controllerRotation = transform.rotation;

            //Decouple
            m_modelTransform.SetPositionAndRotation(new Vector3(m_modelPos.x, controllerPosition.y, m_modelPos.z), m_modelRot);

            //ClampPosition
            Vector3 decoupleDelta = m_modelTransform.position - controllerPosition;

            if(decoupleDelta.sqrMagnitude > m_maxDecouple * m_maxDecouple)
            {
                decoupleDelta = decoupleDelta.normalized * m_maxDecouple;

                m_modelTransform.position = controllerPosition + decoupleDelta;
            }

            //Clamp rotation 
            //Quaternion decoupleRotDelta = m_modelTransform.rotation * Quaternion.Inverse(controllerRotation);
            //float rotYDelta = Mathf.DeltaAngle(controllerRotation.eulerAngles.y, m_modelTransform.eulerAngles.y);

            //DebugGraph.Log("RotYDelta: ", rotYDelta);

            //if (rotYDelta > m_maxAngleDecouple)
            //{
            //    m_modelTransform.Rotate(Vector3.up, (rotYDelta - m_maxAngleDecouple) * - 1f);

                //Quaternion allowableRotation = controllerRotation * Quaternion.AngleAxis(m_maxAngleDecouple, Vector3.up);
                //m_modelTransform.rotation = allowableRotation;
            //}
            //else if (rotYDelta < -m_maxAngleDecouple)
            //{
            //    m_modelTransform.Rotate(Vector3.up, (rotYDelta + m_maxAngleDecouple) * -1f);
            //}

            //Manage locking
            switch (m_decoupleMode)
            {
                case EDecoupleMode.LockAll:
                    m_modelTransform.SetPositionAndRotation(transform.position, controllerRotation);
                    break;
                case EDecoupleMode.LockPosition:
                    m_modelTransform.position = transform.position;
                    break;
                case EDecoupleMode.LockRotation:
                    m_modelTransform.rotation = controllerRotation;
                    break;
            }
        }

        //Use this to calculate root motion blending. It returns a new move delta to apply to your controller movement
        public (Vector3 moveDelta, float rotDelta) CalculateRootMotionBlending(Vector3 a_moveDelta, float a_rotDelta, bool a_hasInput)
        {
            if(m_rootMotionOverride)
            {
                return (m_animator.deltaPosition, m_animator.deltaRotation.eulerAngles.y);
            }

            if (!a_hasInput && p_rootMotionOnNoInput)
            {
                a_moveDelta = m_animator.deltaPosition;
            }
            else if (p_useRootMotionBlending)
            {
                float angle = Vector3.Angle(m_modelTransform.forward, a_moveDelta.normalized);

                if (angle > p_minAngleToBlendRootMotion)
                {
                    float lerp = (angle - p_minAngleToBlendRootMotion) / (180f - p_minAngleToBlendRootMotion);
                    lerp = p_rootMotionCurve.Evaluate(lerp) * p_rootMotionPercent;

                    //Vector3 rootDelta = m_animator.rootPosition - m_modelTransform.position;

                    Vector3 rootDelta = m_animator.deltaPosition;
                    float rootRotDelta = m_animator.deltaRotation.eulerAngles.y;

                    a_moveDelta = Vector3.Lerp(a_moveDelta, rootDelta, lerp);
                    a_rotDelta = Mathf.Lerp(a_rotDelta, rootRotDelta, lerp);
                }
            }

            if (m_mxmAnimator.LongErrorWarpType == ELongitudinalErrorWarp.Stride
                && m_mxmAnimator.LongitudinalWarper != null)
            {
                a_moveDelta *= m_mxmAnimator.LongitudinalWarper.RootMotionScale();
            }

            return (a_moveDelta, a_rotDelta);
        }

        //Use this function to get the Y component of the move delta for gravity if the character is not grounded
        public float CalculateGravityMoveDelta(float a_deltaTime)
        {
            if (m_enableGravity)
            {
                m_verticalSpeed += Physics.gravity.y * a_deltaTime;
            }
            else
            {
                m_verticalSpeed = 0f;
            }

            return m_verticalSpeed * a_deltaTime;
        }

    }//End of class: MxMAnimationDecoupler
}//End of namespace: MxM