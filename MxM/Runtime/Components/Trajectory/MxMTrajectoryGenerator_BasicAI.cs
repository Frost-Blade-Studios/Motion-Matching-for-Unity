// Copyright © 2017-2024 Vault Break Studios Pty Ltd

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.AI;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;

namespace MxM
{
    //===========================================================================================
    /**
    *  @brief This class / Unity component (Monobehaviour) is used to generate a trajectory in
    *  3D space for an MxMAnimator (Motion Matching Animation System) that uses a nav-mesh for navigation.
    *  This version of the AI trajectory generator is simplified, providing a trajectory that always
    *  points to the next point in the AI Path.
    *  
    *  The recommended use is to allow the MxMAnimator to control character rotation through root
    *  rotation (set RotationOnly = true on MxMRootMotionApplciator) but allow the NavMeshAgent to 
    *  move the character (be sure to set AngularSpeed on navMeshAgent to 0.
    *  
    *  See MxMTrajectoryGeneratorBase.cs for more details on trajectory generators in general.
    *         
    *********************************************************************************************/

    public class MxMTrajectoryGenerator_BasicAI : MxMTrajectoryGeneratorBase
    {
        [Header("Motion Settings")]
        [SerializeField] private float m_maxSpeed = 4f;
        [SerializeField] private float m_moveResponsiveness= 15f;
        [SerializeField] private float m_turnResponsiveness = 10f;
        [SerializeField] private float m_stoppingDist = 1f;
        [SerializeField] private ETrajectoryMoveMode m_trajectoryMode = ETrajectoryMoveMode.Normal;
        [SerializeField] private bool m_applyRootSpeedToNavAgent = true;
        [SerializeField] private bool m_faceDirectionOnIdle = false;

        private NavMeshAgent m_navAgent;

        private bool m_hasInputThisFrame;
        private NativeArray<float3> m_newTrajPositions;

        public bool FaceDirectiononIdle { get { return m_faceDirectionOnIdle; } set { m_faceDirectionOnIdle = value; } }
        public Vector3 StrafeDirection { get; set; }
        public Vector3 InputVector { get; set; }
        public float StoppingDistance { get { return m_stoppingDist; } set { m_stoppingDist = value; } }
        public float MaxSpeed { get { return m_maxSpeed; } set { m_maxSpeed = value; } }
        public float MoveResponsiveness { get { return m_moveResponsiveness; } set { m_moveResponsiveness = value; } }
        public float TurnResponsiveness { get { return m_turnResponsiveness; } set { m_turnResponsiveness = value; } }
        public ETrajectoryMoveMode TrajectoryMode { get { return m_trajectoryMode; } set { m_trajectoryMode = value; } }
        public bool ApplyRootSpeedToNavAgent { get { return m_applyRootSpeedToNavAgent; } set { m_applyRootSpeedToNavAgent = value; } }

        protected override void Setup(float[] a_predictionTimes) { }

        //===========================================================================================
        /**
        *  @brief This is the core function of a motion matching controller. It is responsible for 
        *  updating the trajectory prediction for movement. This movement model can differ between 
        *  controllers but this controller takes a deceivingly simplistic approach. 
        *  
        *  The predicted trajectory output from this function is in evenly spaced points and it is not
        *  necessarily the trajectory passed to the MxMAnimator. Rather, the high resolution trajectory
        *  calculated here will be sampled using the ExtractMotion function before passing data to the
        *  MxMAnimator.
        *         
        *********************************************************************************************/
        protected override void UpdatePrediction(float a_deltaTime)
        {
            //Desired linear velocity is calculated based on user input
            Vector3 desiredLinearVelocity = CalculateDesiredLinearVelocity();

            //Calculate the desired linear displacement over a single iteration
            Vector3 desiredLinearDisplacement = desiredLinearVelocity / p_sampleRate;

            float desiredOrientation = 0f;
            if (m_trajectoryMode != ETrajectoryMoveMode.Normal || (!m_hasInputThisFrame && m_faceDirectionOnIdle))
            {
                desiredOrientation = Vector3.SignedAngle(Vector3.forward, StrafeDirection, Vector3.up);
            }
            else if (desiredLinearDisplacement.sqrMagnitude > 0.05f)
            {
                desiredOrientation = Mathf.Atan2(desiredLinearDisplacement.x,
                    desiredLinearDisplacement.z) * Mathf.Rad2Deg;
            }
            else
            {
                desiredOrientation = transform.rotation.eulerAngles.y;
            }

            var trajectoryGenerateJob = new TrajectoryGeneratorJob()
            {
                TrajectoryPositions = p_trajPositions,
                TrajectoryRotations = p_trajFacingAngles,
                NewTrajectoryPositions = m_newTrajPositions,
                DesiredLinearDisplacement = desiredLinearDisplacement,
                DesiredOrientation = desiredOrientation,
                MoveRate = m_moveResponsiveness * a_deltaTime,
                TurnRate = m_turnResponsiveness * a_deltaTime
            };

            p_trajectoryGenerateJobHandle = trajectoryGenerateJob.Schedule();
        }

        //===========================================================================================
        /**
        *  @brief Monobehaviour Start function which sets up some initial parameters for the 
        *  trajectory generator
        *         
        *********************************************************************************************/
        void Start()
        {
            m_navAgent = GetComponentInChildren<NavMeshAgent>();
            Assert.IsNotNull(m_navAgent, "Error: MxMTrajectoryGEnerator_AI - cannot find NavMeshAgent component");

            StrafeDirection = Vector3.forward;
        }

        //===========================================================================================
        /**
        *  @brief Monobehaviour FixedUpdate which updates / records the past trajectory every physics
        *  update if the Animator component is set to AnimatePhysics update mode.
        *         
        *********************************************************************************************/
        public void FixedUpdate()
        {
#if UNITY_6_1_OR_NEWER
            if (p_animator.updateMode == AnimatorUpdateMode.Fixed)
#else
            if (p_animator.updateMode == AnimatorUpdateMode.AnimatePhysics)
#endif
            {
                UpdatePastTrajectory(Time.fixedDeltaTime);
            }
        }

        //===========================================================================================
        /**
        *  @brief Monobehaviour Update function which is called every frame that the object is active. 
        *  
        *  This updates / records the past trajectory, provided that the Animator component isn't 
        *  running in 'Animate Physics'
        *         
        *********************************************************************************************/
        public void Update()
        {
#if UNITY_6_1_OR_NEWER
            if (p_animator.updateMode != AnimatorUpdateMode.Fixed)
#else
            if (p_animator.updateMode != AnimatorUpdateMode.AnimatePhysics)
#endif
            {
                UpdatePastTrajectory(Time.deltaTime);
            }
        }

        //===========================================================================================
        /**
        *  @brief Monobehaviour Late Update function which is called every frame that the object is 
        *  active but only after the animation update. This function projects root motion onto the 
        *  navmesh agent's speed to help prevent foot sliding but also ensure that the character 
        *  still stays perfectly on the navmesh path.
        *         
        *********************************************************************************************/
        public void LateUpdate()
        {
            if(p_animator != null)
            {
                if(m_applyRootSpeedToNavAgent)
                {
                    Vector3 v = m_navAgent.velocity * Time.deltaTime;
                    Vector3 animatorDelta = p_animator.deltaPosition;

                    float rootSpeed = 0f;
                    if(Vector3.Angle(animatorDelta, v) < 180f)
                    {
                        float vMag = v.magnitude;

                        Vector3 projectedDelta = (Vector3.Dot(animatorDelta, v) / (vMag * vMag)) * v;
                        rootSpeed = projectedDelta.magnitude / Time.deltaTime;
                    }

                    if (!float.IsNaN(rootSpeed))
                        m_navAgent.speed = Mathf.Max(rootSpeed, 0.5f);
                }
            }
        }

        //===========================================================================================
        /**
        *  @brief This function calculates the desired linear velocity based on AI Input.
        *  
        *  @return Vector3 - the desired linear velocity of the character.
        *         
        *********************************************************************************************/
        private Vector3 CalculateDesiredLinearVelocity()
        {
            float destSqr = (m_navAgent.destination - transform.position).sqrMagnitude;

            if(destSqr < m_stoppingDist)
            {
                InputVector = Vector3.zero;
                m_hasInputThisFrame = false;
                return Vector3.zero;
            }

            InputVector = (m_navAgent.steeringTarget - transform.position);

            if (InputVector.sqrMagnitude > 0.001f)
            {
                InputVector = InputVector.normalized;
                m_hasInputThisFrame = true;
                float maxSpeed = Mathf.Min(Mathf.Sqrt(destSqr), m_maxSpeed);

                return InputVector * maxSpeed;
            }
            else
            {
                m_hasInputThisFrame = false;
            }

            //No input so just return zero
            return Vector3.zero;
        }

        //===========================================================================================
        /**
        *  @brief Allocates and initializes any native arrays required for the trajectory generator
        *  to function.
        *         
        *********************************************************************************************/
        protected override void InitializeNativeData()
        {
            base.InitializeNativeData();

            m_newTrajPositions = new NativeArray<float3>(p_trajectoryIterations,
                Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        //===========================================================================================
        /**
        *  @brief Disposes any native data that has been created for jobs to avoid memory leaks.
        *         
        *********************************************************************************************/
        protected override void DisposeNativeData()
        {
            base.DisposeNativeData();

            if (m_newTrajPositions.IsCreated)
                m_newTrajPositions.Dispose();
        }

        //===========================================================================================
        /**
        *  @brief Checks if the trajectory generator has movement input or not. The movement input is
        *  usually cached because it is processed during the update and may need to be fetched at a 
        *  later point.
        *  
        *  @return bool - true if there is movement input, false if there is not.
        *         
        *********************************************************************************************/

        public override bool HasMovementInput()
        {
            return m_hasInputThisFrame;
        }
    }
}