// Copyright © 2017-2024 Vault Break Studios Pty Ltd

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Assertions;
using UnityEngine.Events;
using UnityEngine.Playables;
using Unity.Collections;
using Unity.Jobs;
using UTIL;

#if UNITY_2018_4 || UNITY_2019_1 || UNITY_2019_2
using UnityEngine.Experimental.Animations;
#endif

namespace MxM
{
    //============================================================================================
    /**
    *  @brief Alternative animator component to unity's Animator component, which is used to 
    *  manage motion matching animation for high fidelity, smooth and responsive animation.
    *  
    *  The MxMAnimator class is separated into partial classes. This is the core part of the
    *  MxMAnimator component
    *         
    *********************************************************************************************/
    [DisallowMultipleComponent]
    public partial class MxMAnimator : MonoBehaviour
    {
        //****************************** SERIALIZED ********************************
        //General
        [SerializeField] private float m_updateInterval = 0.012f;       //Frequency of database searches
        [SerializeField] private float m_playbackSpeed = 1f;            //The current global animation playback speed modifier
        [SerializeField] private float m_playbackSpeedSmoothRate = 1f;  //Interpolation rate for smoothly changing playback speed
        [SerializeField] private float m_matchBlendTime = 0.3f;         //The transition time during regular motion matching
        [SerializeField] private float m_turnInPlaceThreshold = 45f;    //The angular threshold between facing direction and trajectory facing to pull the character out of idle state.
        [SerializeField] private Transform m_animationRoot;             //The transform that is used for this character (defaults to Monobehaviour transform but can be overriden in the inspector)  
        [SerializeField] private AvatarMask m_animatorControllerMask = null; //The avatar mask to use for the mecanim layer (if in use)
        [SerializeField] private int m_animControllerLayer = 1; //The layer to use for the animator controller
        [SerializeField] private MxMAnimData[] m_animData = null;       //A list of pre-processed animData (assets) to be used by this MxMAniamtor
        [SerializeField] private CalibrationModule m_calibrationOverride; //Optional calibration module to be used to override MxMAnimData
        [SerializeField] private bool m_doNotSearch = false;

        //Debug
        [SerializeField] private bool m_debugGoal = true;               //If true, the goal trajectory will be drawn as gizmo's in the editor
        [SerializeField] private bool m_debugChosenTrajectory = true;   //If true, the chosen animation trajectory will be drawn as gizmo's in the editor
        [SerializeField] private bool m_debugPoses;                     //None play mode only - will allow a preview of all poses in the editor when true
        [SerializeField] private bool m_debugCurrentPose = true;        //If true the current character pose (that is being matched) will be drawn in the editor
        [SerializeField] private int m_debugPoseId;                     //The id of the pose to view when m_debugPoses is true
        [SerializeField] private int m_debugAnimDataId;                 //The AnimData id to use when m_debugPoses is true
        [SerializeField] private bool m_recordAnalytics;                //If true, analytics about what poses are being use will be recorded and can be dumped

        //Options
        [SerializeField] private EPoseMatchMethod m_poseMatchMethod = EPoseMatchMethod.VelocityCosting; //The algorithm used to calculate a pose matching cost
        [SerializeField] private EMxMRootMotion m_rootMotionMode = EMxMRootMotion.On;                   //The method of applying root motion to the character
        [SerializeField] private ETransitionMethod m_transitionMethod = ETransitionMethod.Blend;        //The method of blending to use to transition between animations
        [SerializeField] private EPastTrajectoryMode m_pastTrajectoryMode = EPastTrajectoryMode.ActualHistory; //The method for obtaining past trajectory points
        [SerializeField] private bool m_applyHumanoidFootIK = true;          //If false, humanoid foot Ik retargetting will be turned off
        [SerializeField] private bool m_applyPlayableIK = true;              //If true playable IK will be applied to this clip.
        [SerializeField] private EFavourTagMethod m_favourTagMethod = EFavourTagMethod.Exclusive; //The method for handling favour tags, do they need to match exactly? partially? or does each matching tag contribute to the favour?
        [SerializeField] private bool m_applyTrajectoryBlending = false;     //If true the desired trajectory will be blended with the current animation with a linear falloff over prediction time                                 
        [SerializeField] private float m_trajectoryBlendingWeight = 0.5f;    //A weighting for 'Trajectory Blending'. Keep between 0-1. Impacts how strong the trajectory blending is.
        [SerializeField] private bool m_favourCurrentPose = false;           //If true the current pose will have a favour multiplier in the cost equation
        [SerializeField] private bool m_transformGoal = true;                //If true the goal generated from the trajectory generator will be transformed to the character
        [SerializeField] private float m_currentPoseFavour = 0.95f;          //The cost of the current pose will be multiplied by this factor if m_favourCurrentPose is true
        [SerializeField] private bool m_nextPoseToleranceTest = false;        //Toggle for turning on next pose tolerance testing (good enough testing)
        [SerializeField] private float m_nextPoseToleranceDist = 0.2f;       //The tolerance threshold for trajectory positions in m/s
        [SerializeField] private float m_nextPoseToleranceAngle = 2f;        //The tolerance threshold for trajectory facing angles in degrees/s
        [SerializeField] private bool m_blendOutEarly = false;               //Whether to exit an animation clip early before it ends or not.
        
        //Advanced Options
        [SerializeField] private ETagBlendMethod m_tagBlendMethod = ETagBlendMethod.HighestWeight; //How tags should be blended between digital poses. Highest weight? or combined?

        //Update Manager
        [SerializeField] private bool m_priorityUpdate = true;
        [SerializeField] private float m_maxUpdateDelay = 0.5f;    
        
        //Trajectory Error Warping
        [SerializeField] private WarpModule m_overrideWarpSettings = null;                                                  //A settings module which overrides all warp settings
        [SerializeField] private EAngularErrorWarp m_angularWarpType = EAngularErrorWarp.On;                                //The type of angular warping to be used
        [SerializeField] private EAngularErrorWarpMethod m_angularWarpMethod = EAngularErrorWarpMethod.CurrentHeading;      //The method of angular warping to be used
        [SerializeField] private float m_angularErrorWarpRate = 45f;                                                        //The rate at which angular warping is applied in degress per second
        [SerializeField] private float m_angularErrorWarpThreshold = 0.5f;                                                  //The minimum speed at which angular error warping will activate
        [SerializeField] private float m_angularErrorWarpAngleThreshold = 60f;                                              //The maximum error under which warping will be active
        [SerializeField] private float m_angularErrorWarpMinAngleThreshold = 0.5f;
        [SerializeField] private ELongitudinalErrorWarp m_longErrorWarpType = ELongitudinalErrorWarp.None;                  //The type of longitudinal error warping to use
        [SerializeField] private Vector2 m_speedWarpLimits;                                                                 //The playback speed limits of longitudinal error warping.

        //Blend Spaces
        [SerializeField] private EBlendSpaceSmoothing m_blendSpaceSmoothing = EBlendSpaceSmoothing.None;    //The method of blend space smoothing
        [SerializeField] private Vector2 m_blendSpaceSmoothRate = new Vector2(0.25f, 0.25f);                //The blend space smoothing rate on x and y axis

        //Animation Playables
        [SerializeField] private int m_maxMixCount = 8;                             //The maximum number of channels for motion matching animations
        [SerializeField] private int m_primaryBlendChannel;                         //The Id of the primary blend channel (whatever channel is playing the chosen pose animation)
        [SerializeField] protected bool p_DIYPlayableGraph = false;                  //If true, the playable graph will not be created. Rather the user must create the playable graph and call CreateMotionMatchPlayable and connect it to their own graph.
        [SerializeField] protected bool p_manualInitialize = false;                 //If true, the initialize function must be called manually on the MxMAnimator. This is called for runtime setups.
        [SerializeField] private bool m_autoCreateAnimatorController = false;       //If true, a mecanim controller will automatically be created and added to layer 1 (second layer) of the system. Provided there is a controller sloted in the animator.

        //Footstep Tracking
        [SerializeField] private float m_minFootstepInterval = 0.2f; //The minimum time interval allowable between triggered footsteps
        [SerializeField] private int m_cachedLastLeftFootstepId = 0; //Optimization for footstep tracking to cull already checked tags
        [SerializeField] private int m_cachedLastRightFootstepId = 0; //Optimization for footstep tracking to cull already checked tags
        
        
        //MxM Extensions
        [SerializeField] private List<IMxMExtension> m_phase1Extensions;
        [SerializeField] private List<IMxMExtension> m_phase2Extensions;
        [SerializeField] private List<IMxMExtension> m_postExtensions;

#if UNITY_EDITOR
        [SerializeField] private bool m_generalFoldout = true;
        [SerializeField] private bool m_animDataFoldout = true;
        [SerializeField] private bool m_optionsFoldout = true;
        [SerializeField] private bool m_warpingFoldout = true;
        [SerializeField] private bool m_optimisationFoldout = false;
        [SerializeField] private bool m_debugFoldout = false;
        [SerializeField] private bool m_callbackFoldout = false;
#endif

        //Unity Events
        [System.Serializable] public class UnityEvent_EEventState : UnityEvent<EEventState> { };    //Custom Unity event for passing current EEventState during events
        [System.Serializable] public class UnityEvent_Int : UnityEvent<int> { };                    //Custom Unity event for passing an integer
        [System.Serializable] public class UnityEvent_FootStepData : UnityEvent<FootStepData> { };  //Custom Unity event for passing FootStepData
        [System.Serializable] public class UnityEvent_Tag : UnityEvent<ETags> { };                  //Custom Unity event for passing required tags
        
        
        [System.Serializable]
        public struct PoseChangeData
        {
            public int PoseId;
            public float SpeedMod;
            public float TimeOffset;
            
            public PoseChangeData(int a_poseId, float a_speedMod, float a_timeOffset)
            {
                PoseId = a_poseId;
                SpeedMod = a_speedMod;
                TimeOffset = a_timeOffset;
            }
        }
        
        [System.Serializable] public class UnityEvent_PoseChange : UnityEvent<PoseChangeData> {};      //Custom Unity event for pose changes

        [SerializeField] private UnityEvent m_onSetupComplete = new UnityEvent();                   //Unity event called when setup of the motion matching playable graph and settings is complete
        [SerializeField] private UnityEvent m_onIdleTriggered = new UnityEvent();                   //Unity event called when the idle state is triggered
        [SerializeField] private UnityEvent m_onIdleEnd = new UnityEvent();                         //Unity event called when the idle state is exited
        [SerializeField] private UnityEvent m_onEventComplete = new UnityEvent();                   //Unity event called when an Event (MxM Action Event) is completed
        [SerializeField] private UnityEvent_Tag m_onRequireTagsChanged = new UnityEvent_Tag();          //Unity event called when required tags are changed.
        [SerializeField] private UnityEvent_EEventState m_onEventStateChanged = new UnityEvent_EEventState();   //Unity event called when an Event (MxM Action Event) state is changed. The event state will be passed
        [SerializeField] private UnityEvent_Int m_onEventContactReached = new UnityEvent_Int();         //Unity event called whenever a contact has been reached in an event (MxM Action Event). The id of the contact will be passed
        [SerializeField] private UnityEvent_FootStepData m_onLeftFootStepStart = new UnityEvent_FootStepData();  //Unity event called when a left footstep is triggered. Footstep data will be passed
        [SerializeField] private UnityEvent_FootStepData m_onRightFootStepStart = new UnityEvent_FootStepData(); //Unity event called when a right footstep is triggered. Footstep data will be passed.  
        [SerializeField] private UnityEvent_PoseChange m_onPoseChanged = new UnityEvent_PoseChange(); //Unity event called when the pose is changed

        public UnityEvent OnSetupComplete { get { return m_onSetupComplete; } } //Can be used to setup OnSetupComplete callbacks during runtime
        public UnityEvent OnIdleTriggered { get { return m_onIdleTriggered; } } //Can be used to setup OnIdleTriggered callbacks during runtime
        public UnityEvent OnIdleEnd { get { return m_onIdleEnd; } } //Can be used to setup OnIdleEnd callbacks during runtime
        public UnityEvent OnEventComplete { get { return m_onEventComplete; } } //Can be used to setup OnEventComplete callbacks during runtime
        public UnityEvent_Tag OnRequireTagsChanged { get {return m_onRequireTagsChanged; } } //Can be used to setup OnRequireTagsChanged callbacks during runtime
        public UnityEvent_EEventState OnEventStateChanged { get { return m_onEventStateChanged; } } // Can be used to setup OnEventStateChanged callbacks during runtime
        public UnityEvent_Int OnEventContactReached { get { return m_onEventContactReached; } } //Can be used to setup OnEventContactReached callbacks during runtime
        public UnityEvent_FootStepData OnLeftFootStepStart { get { return m_onLeftFootStepStart; } } //Can be used to setup OnLeftFootStepStart callbacks during runtime
        public UnityEvent_FootStepData OnRightFootStepStart { get { return m_onRightFootStepStart; } } //Can be used to setup OnRightFootStepStart callbacks during runtime
        public UnityEvent_PoseChange OnPoseChange { get { return m_onPoseChanged; } } //Can be used to setup OnPoseChange callbacks during runtime
       
        //**************************** NON SERIALIZED *******************************
        //Tracking
        private float m_timeSinceMotionUpdate;          //The time passed since the last motion updated
        private float m_timeSinceMotionChosen;          //The time passed since the last motion was chosen
        private bool m_poseSearchThisFrame;
        private float m_poseInterpolationValue = 0f;    //The interpolation value used to calculate the last interpolated pose
        private int m_curChosenPoseId;                  //The id of the chosen pose in the animation database
        private int m_dominantBlendChannel = 0;         //The index of the dominant pose in m_curPoses. i.e. the id of the channel playig the animation with the highest weight
        private PoseData m_curInterpolatedPose;         //The current interpolated pose that was calculated
        private PoseData m_chosenPose;                  //The current chosen pose in the blended stack (i.e. the last pose to be added)
        private PoseData m_dominantPose;                //The dominant pose in the blended stack (i.e. the pose with the most weight)
        private CalibrationData m_curCalibData;         //The calibration data being used for the cost calculations
        protected float p_currentDeltaTime;             //The current delta time (dependent on animator physics mode)
        private bool m_enforcePoseSearch;               //If the clip must be forceably changed this frame (used when a cut clip nears it's end)
        private int m_startFutureTrajIndex = 0;         //The index of the first trajectory point which is in the future
       // private float m_timeDialation = 0f;           //The difference in time between the actual update interval and the desired update interval
       private float m_timeSinceLastLeftFootstep = 0f;  //The time that has passed since the last left footstep was triggered
       private float m_timeSinceLastRightFootstep = 0f; //The time that has passed since the last right footstep was triggered
        
        //Trajectory Error Warping
        private ILongitudinalWarper m_longErrorWarper;  //Reference to a custom longitudinal error warper

        //AnimData swapping
        private int m_currentAnimDataId = 0;            //The Id of the current animation data being used
        private int m_queueAnimDataSwapId = -1;         //If a swap in current anim data is queued, the id of the queued animation data swap is stored here. -1 by default means no swap is queued for the next update.
        private int m_queueAnimDataStartPoseId = 0;     //The start pose id of the next queued animation data swap
        private int m_startPoseOverride = -1;           //A manual override id value for the starting pose. By default, the starting pose is the first idle set pose. This can be used to override that.

        //Animation Playables
        public PlayableGraph MxMPlayableGraph { get; private set; }             //The playable graph used by the system 
        private bool m_animMixerConnected;                                      //Set to true once the MotionMatching graph network has been connected to a playable graph.  
        private AnimationMixerPlayable m_animationMixer;                        //The animation mixer playable responsible for mixing and transitioning the motion matched animation
        private ScriptPlayable<MotionMatchingPlayable> m_motionMatchPlayable;   //The motion matching playable that manages motion matching logic in the animation update.
        private AnimationLayerMixerPlayable m_animationLayerMixer;              //The animation layer mixer responsible for layered animation. m_animationMixer is on layer 0, Mecanim on layer 1 and custom layers on subsequent layers
        private AnimatorControllerPlayable m_animControllerPlayable;            //The playable for controlling mecanim.
        private MxMPlayableState[] m_animationStates;                           //A list of playable states for all the motion matching playable animations.
        
        //Components & Objects
        protected Animator p_animator;                    //Reference to the attached animator component
        protected IMxMTrajectory p_trajectoryGenerator;   //Reference to the require trajectory generator interface
        private IMxMTrajectory[] m_trajectoryGenerators;  //List of all attached trajectory generators
        private IMxMRootMotion m_rootMotion;            //Reference to a root motion applicator interface
        private TrajectoryPoint[] m_desiredGoal;        //The desired goal modified by trajectory blending
        private TrajectoryPoint[] m_desiredGoalBase;    //The un-modified desired goal taken directly from the Trajectory Generator
        private FSM m_fsm;                              //A state machine used to manage the internal states of the MxMAnimator

        //Multi animator management
       // private static int s_mxmAnimatorCount;              //Static counter for the number of active MxMAnimator components in the scene. This is used to assist with batch scheduling jobs when all MxMAnimators are read.
        //private static int s_curMxMAnimatorId;              //Static id of the current MxMAnimator component being update. The jobs will be batch scheduled once this Id reaches s_mxmAnimatorCount
       // private static int s_mxmAnimatorStack;              //The number of MxMAnimator's currently waiting to be scheduled

        //Inertial Blending
        private InertialBlendModule m_inertialBlendModule;  //Experimental module for inertial blending

        //Animation Rigging Integration
#if UNITY_2019_1_OR_NEWER
        protected IMxMUnityRiggingIntegration p_riggingIntegration;   //Animation rigging integration interface for fixing rigging transforms when the playable graph is modified
#endif

        //**************************** PROPERTIES *******************************
        public MxMNativeAnimData CurrentNativeAnimData { get; private set; } 
        public MxMAnimData CurrentAnimData { get; private set; }
        public MxMAnimData[] AnimData { get { return m_animData; } set { m_animData = value; } }
        public Animator UnityAnimator { get { return p_animator; } }
        public bool IsPaused { get; private set; }
        public bool IsInitialized { get; private set; } = false;
        public ref AnimationMixerPlayable MixerPlayable { get { return ref m_animationMixer; } }
        public ref AnimationLayerMixerPlayable LayerMixerPlayable { get { return ref m_animationLayerMixer; } }
        public bool BlendTrajectory { get { return m_applyTrajectoryBlending; } set { m_applyTrajectoryBlending = value; } }
        public float TrajectoryBlendWeight { get { return m_trajectoryBlendingWeight; } set { m_trajectoryBlendingWeight = value; } }
        public EBlendSpaceSmoothing DefaultBlendSpaceSmoothType { get { return m_blendSpaceSmoothing; } set { m_blendSpaceSmoothing = value; } }
        public Vector2 DefaultBlendSpaceSmoothRate { get { return m_blendSpaceSmoothRate; } set { m_blendSpaceSmoothRate = value; } }
        public EMxMRootMotion RootMotion { get { return m_rootMotionMode; } set { m_rootMotionMode = value; } }
        public EPastTrajectoryMode PastTrajectoryMode { get { return m_pastTrajectoryMode; } set { m_pastTrajectoryMode = value; } }
        public EFavourTagMethod FavourTagMode { get { return m_favourTagMethod; } set { m_favourTagMethod = value; } }
        public EAngularErrorWarp AngularErrorWarpType { get { return m_angularWarpType; } set { m_angularWarpType = value; } }
        public EAngularErrorWarpMethod AngularErrorWarpMethod { get { return m_angularWarpMethod; } set { m_angularWarpMethod = value; } }
        public ELongitudinalErrorWarp LongErrorWarpType { get { return m_longErrorWarpType; } set { m_longErrorWarpType = value; } }
        public ILongitudinalWarper LongitudinalWarper { get { return m_longErrorWarper; } }
        public float LongErrorWarpScale { get; set; }
        public float LatErrorWarpAngle { get; private set; }
        public float AngularErrorWarpRate { get { return m_angularErrorWarpRate; } set { m_angularErrorWarpRate = value; } }
        public float AngularErrorWarpThreshold { get { return m_angularErrorWarpThreshold; } set { m_angularErrorWarpThreshold = value; } }

        public AvatarMask AnimatorControllerMask
        {
            get { return m_animatorControllerMask; }
            set
            {
                m_animatorControllerMask = value;
                
                if(m_animControllerLayer > 0)
                    m_animationLayerMixer.SetLayerMaskFromAvatarMask((uint)m_animControllerLayer, m_animatorControllerMask);
            }
        }
        public float DesiredPlaybackSpeed { get; set; }
        public float UserPlaybackSpeedMultiplier { get; set; }
        public float PlaybackSpeedSmoothRate { get { return m_playbackSpeedSmoothRate; } set { m_playbackSpeedSmoothRate = value; } }
        public int MaxMixCount { get { return m_maxMixCount; } }
        public Transform AnimationRoot { get { return m_animationRoot; } set { m_animationRoot = value; } }
        public Vector3 BodyVelocity { get { return m_curInterpolatedPose.LocalVelocity; } }      
        public bool FavourCurrentPose { get { return m_favourCurrentPose; } set { m_favourCurrentPose = value; } }
        public float CurrentDeltaTime { get { return p_currentDeltaTime; } }
        
        public float TimeSinceMotionChosen { get => m_timeSinceMotionChosen; }              //Returns the time since the last pose change
        public float TimeSinceMotionUpdate { get => m_timeSinceMotionUpdate; }              //Returns the time since the motion matching update
        public int ChosenPoseId { get => m_curChosenPoseId; }                               //Returns the chosen pose id
        public int DominantPoseId { get => m_dominantPose.PoseId; }                         //Returns the dominant pose id
        public ref PoseData ChosenPose { get => ref m_chosenPose; }                         //Returns the pose data that is current (i.e. being blended in
        public ref PoseData DominantPose { get => ref m_dominantPose; }                     //Returns the pose data with the most weight
        public ref PoseData CurrentInterpolatedPose { get => ref m_curInterpolatedPose; }   //Returns the current interpolated pose calculated at runtime
        public MxMPlayableState[] AnimationState { get => m_animationStates; }              //Returns the animation state layer stack for motion matching

        public float ChosenAnimationTime => ChosenPose.Time + m_timeSinceMotionChosen;
        public float ChosenAnimationNormalizedTime => ChosenAnimationTime / CurrentAnimData.Clips[ChosenPose.AnimId].length;

        public float MinFootstepInterval
        {
            get => m_minFootstepInterval;
            set => m_minFootstepInterval = value;
        }

        //Used to modify the update interval (i.e. the frequency of database searches) from without the MxMAnimator
        public float UpdateInterval
        {
            get { return m_updateInterval; }
            set { m_updateInterval = Mathf.Clamp(value, 0f, 3f); }
        }

        public float UpdateRate
        {
            get { return 1 / m_updateInterval; }
            set { m_updateInterval = Mathf.Clamp(1 / value, 0f, 3f); }
        }

        //Used to modify the blend time for the general motion matching state. Blend time is clamped
        public float BlendTime
        {
            get { return m_matchBlendTime; }
            set { m_matchBlendTime = Mathf.Clamp(value, 0.0001f, 5f); }
        } 
        
        public bool PriorityUpdate
        {
            get => m_priorityUpdate;
            set => m_priorityUpdate = value;
        }
        
        public float MaxUpdateDelay
        {
            get => m_maxUpdateDelay;
            set => m_maxUpdateDelay = Mathf.Max(0f, value);
        }
        public AnimatorUpdateMode UpdateMode => p_animator ? p_animator.updateMode : AnimatorUpdateMode.Normal;
        public bool CanUpdate => !IsPaused && m_animMixerConnected && isActiveAndEnabled;

        [Obsolete("MatchBlendTime is deprecated. Please use 'BlendTime' instead")]
        public float MatchBlendTime
        {
            get { return m_matchBlendTime; }
            set { m_matchBlendTime = Mathf.Clamp(value, 0.0001f, 5f); }
        }

        [Obsolete("IdleBlendTime is deprecated. Please use 'BlendTime' instead")]
        public float IdleBlendTime
        {
            get { return m_matchBlendTime; }
            set { m_matchBlendTime = Mathf.Clamp(value, 0.0001f, 5f); }
        }

        [Obsolete("EventBlendTime is deprecated. Please use 'BlendTime' instead")]
        public float EventBlendTime
        {
            get { return m_matchBlendTime; }
            set { m_matchBlendTime = Mathf.Clamp(value, 0.0001f, 5f); }
        }


        //Used to modify the playback speed instantly, without any smoothing. This only affects the playback speed of the primary blend channel
        public float PlaybackSpeed
        {
            get { return m_playbackSpeed; }
            set
            {
                m_playbackSpeed = DesiredPlaybackSpeed = value;

                var playable = m_animationMixer.GetInput(m_primaryBlendChannel);

                if (!playable.IsValid())
                    return;

                int inputCount = playable.GetInputCount();

                if (inputCount > 0)
                {
                    for (int i = 0; i < inputCount; ++i)
                    {
                        var clipPlayable = playable.GetInput(i);

                        if (clipPlayable.IsValid())
                            clipPlayable.SetSpeed(m_playbackSpeed);
                    }
                }
                else
                {
                    playable.SetSpeed(m_playbackSpeed);
                }
            }
        }

        public CalibrationModule OverrideCalibration 
        { 
            get { return m_calibrationOverride; } 
            set 
            {
                m_calibrationOverride = value;

                if (value.IsCompatibleWith(CurrentAnimData))
                {
                    CalibrationData calibSet = m_calibrationOverride.GetCalibrationSet(0);

                    if (calibSet != null)
                        m_curCalibData = calibSet;
                }
            } 
        }

        //============================================================================================
        /**
        *  @brief Called before the first update. Initializes the animation data for the first time.
        *  
        *  Note: Only initializes the Animator if not using a DIY playable graph.
        *         
        *********************************************************************************************/
        protected virtual void Start()
        {
            if (!p_DIYPlayableGraph || p_manualInitialize)
            {
                Initialize();
            }

#if UNITY_EDITOR
            StopEditorWarnings();
#endif
    }

    //============================================================================================
    /**
    *  @brief Called once every time the MxMAnimator component is enabled. 
    *  
    *  Ensures accurate batching of scheduled jobs.
    *         
    *********************************************************************************************/
    protected virtual void OnEnable()
        {
            if (m_motionMatchPlayable.IsValid())
            {
                m_motionMatchPlayable.SetInputWeight(0, 1f);
            }

            UnPause();
          
#if UNITY_EDITOR
            if (m_debugArrowMesh == null)
            {
                m_debugArrowMesh = Resources.Load<Mesh>("DebugArrow");
            }
#endif
        }

        //============================================================================================
        /**
        *  @brief Called once evertime the MxMAnimator component is disabled.
        *  
        *  Ensures accurate batching of scheduled jobs and stops any running jobs on this MxMAnimator
        *         
        *********************************************************************************************/
        protected virtual void OnDisable()
        {
            Pause();
            ResetMotion();

            m_poseJobHandle.Complete();
            m_trajJobHandle.Complete();

            if(m_motionMatchPlayable.IsValid())
                m_motionMatchPlayable.SetInputWeight(0, 0f);

            //In case this was disabled after it was updated, we need to stop any running jobs or
            //they will never be collected.
            StopJobs();
        }
        
#if UNITY_2019_1_OR_NEWER
        public void CacheRiggingIntegration()
        {
            if (p_riggingIntegration != null)
                p_riggingIntegration.CacheTransforms();
        }
#endif
        
        //============================================================================================
        /**
        *  @brief Updates the MxMAnimator during the physics animation loop. 
        *  
        *  Note: The logic in this function will only run if the Animator Update mode is set to 
        *  Animate Physics.
        *         
        *********************************************************************************************/
        protected virtual void FixedUpdate_Internal()
        {
            if (CurrentAnimData == null)
            {
                return;
            }
            
#if UNITY_6_1_OR_NEWER
            if (p_animator.updateMode == AnimatorUpdateMode.Fixed)
#else
            if (p_animator.updateMode == AnimatorUpdateMode.AnimatePhysics)
#endif
            {
                p_currentDeltaTime = Time.fixedDeltaTime;

                if (!IsPaused && m_animMixerConnected)
                {
#if UNITY_2019_1_OR_NEWER
                    if (p_riggingIntegration != null)
                        p_riggingIntegration.CacheTransforms();
#endif
                    //Run the first phase update for MxM which is responsible for scheduling jobs
                    MxMUpdate_Phase1(p_currentDeltaTime);

                    //if (m_rootMotion != null)
                    //    m_rootMotion.AnimatorDependentUpdate(p_currentDeltaTime);
                }
            }
        }

        //============================================================================================
        /**
        *  @brief Updates the MxMAnimator during the regular update loop
        *  
        *  Note: The logic in this function will only run if the Animator Update mode is not set to 
        *  Animate Physics.
        *         
        *********************************************************************************************/
        protected virtual void Update_Internal()
        {
            if (CurrentAnimData == null)
            {
                return;
            }
            
#if UNITY_6_1_OR_NEWER
            if (p_animator.updateMode != AnimatorUpdateMode.Fixed)
#else
            if (p_animator.updateMode != AnimatorUpdateMode.AnimatePhysics)
#endif
            {
                p_currentDeltaTime = Time.deltaTime;

                if (!IsPaused && m_animMixerConnected)
                {
#if UNITY_2019_1_OR_NEWER
                    if (p_riggingIntegration != null)
                        p_riggingIntegration.CacheTransforms();
#endif
                    //Run the first phase update for MxM which is responsible for scheduling jobs
                    MxMUpdate_Phase1(p_currentDeltaTime);

                    //if (m_rootMotion != null)
                    //    m_rootMotion.AnimatorDependentUpdate(p_currentDeltaTime);
                }
            }
        }

        //=============================================================================================
        /**
        *  @brief Called when the MxMAnimator component is destroyed. Destroys the playable graph and 
        *  cleans up any allocated native data to avoid memory leaks.
        *         
        *********************************************************************************************/
        protected virtual void OnDestroy()
        {
            if (p_animator == null) //Early Out: Do not destroy if the MxMAnimator has never been initialized in the first place
                return;
            
            if (!p_DIYPlayableGraph)//Don't destroy the playable graph if this is not a DIY graph. The graph doesn't belong to MxM in this ase
            {
                if (MxMPlayableGraph.IsValid())
                {
                    MxMPlayableGraph.Destroy();
                }
            }

            StopJobs(); //Ensure no jobs are running before disposing native data.

            foreach (MxMAnimData animData in m_animData)
            {
                if (animData != null)
                    animData.ReleaseNativeData(); //Note: Since multiple MxMAnimator components use this data, this does not dispose it. It simply releases it's handle on it. 
            }

            //Dispose local Native Arrays
            if (m_poseCosts.IsCreated)
                m_poseCosts.Dispose();

            if (m_trajCosts.IsCreated)
                m_trajCosts.Dispose();

            if (m_chosenPoseId.IsCreated)
                m_chosenPoseId.Dispose();

            if (m_inertialBlendModule != null)
                m_inertialBlendModule.DisposeNativeData();

            if (MxMSearchManager.DoesInstanceExist)
            {
                MxMSearchManager.Instance.UnRegisterMxMAnimator(this);
            }
        }

        //============================================================================================
        /**
        *  @brief Unity callback function for animation movement. Handles custom root motion with
        *  warping or defers the root motion to a custom gameplay controller with IMxMRootMotion
        *  interface.
        *         
        *********************************************************************************************/
        public virtual void OnAnimatorMove()
        {
            if (!m_animMixerConnected/* || IsPaused*/) 
                return;

            if (m_rootMotionMode != EMxMRootMotion.Off)
            {
                Vector3 warp = Vector3.zero;
                Quaternion warpRot = Quaternion.identity;

                //Handle animation root warping if we are currently in an event
                if (m_fsm.CurrentStateId == (uint)EMxMStates.Event)
                {
                    FetchEventLookupData(out var lookupData);

                    if ((int) WarpType > (int) EEventWarpType.Snap)
                    {
                        UpdateEventWarping(out warp, ref lookupData);
                        PosWarpThisFrame = warp;
                    }
                    else
                    {
                        PosWarpThisFrame = Vector3.zero;
                    }

                    if ((int) RotWarpType > (int) EEventWarpType.Snap)
                    {
                        UpdateEventRotWarping(out warpRot, ref lookupData);
                        RotWarpThisFrame = warpRot.eulerAngles.z;
                    }
                    else
                    {
                        RotWarpThisFrame = 0;
                    }
                }
                else
                {
                    //This handles angular error warping when an event is not active.  Angular error warping ensures the the character always runs in the 
                    //direction you want them to go, even if there is no perfect animation that fits.
                    if (m_angularWarpType == EAngularErrorWarp.On)
                    {
                        if (CanPerformTrajectoryErrorAngularWarping())
                        {
                            Quaternion errorWarpRot = ApplyTrajectoryErrorAngularWarping(p_animator.deltaRotation, p_currentDeltaTime);

                            warpRot *= errorWarpRot;
                        }
                        else
                        {
                            LatErrorWarpAngle = 0f;
                        }
                    }
                }

                //This switch statement handles the application of root motion dependent on the user settings.
                switch (m_rootMotionMode)
                {
                    case EMxMRootMotion.On:
                        {
                            Vector3 deltaPos = p_animator.deltaPosition;

                            if (m_longErrorWarper != null)
                            {
                                float rootMotionScale = m_longErrorWarper.RootMotionScale();

                                deltaPos *= rootMotionScale;
                                warp *= rootMotionScale;
                            }

                            //Apply the base root motion + warping directly to the animation transform
                            m_animationRoot.SetPositionAndRotation(m_animationRoot.position
                                    + deltaPos + warp, p_animator.rootRotation * warpRot);
                        }
                        break;
                    case EMxMRootMotion.RootMotionApplicator:
                        {
                            Vector3 deltaPos = p_animator.deltaPosition;

                            if (m_longErrorWarper != null)
                            {
                                float rootMotionScale = m_longErrorWarper.RootMotionScale();

                                deltaPos *= rootMotionScale;
                                warp *= rootMotionScale;
                            }

                            if (m_rootMotion != null)
                            {
                                m_rootMotion.HandleRootMotion(deltaPos, p_animator.deltaRotation,
                                    warp, warpRot, p_currentDeltaTime);
                            }
                            else
                            {
                                m_animationRoot.SetPositionAndRotation(m_animationRoot.position 
                                    + deltaPos + warp, p_animator.rootRotation * warpRot); //Revert to transform if there is no IMxMRootMotion attached
                            }
                            
                        }
                        break;
                    case EMxMRootMotion.RootMotionApplicator_AngularErrorWarpingOnly:
                        {
                            if (m_rootMotion != null)
                            {
                                //Forward angular error warping only to the root motion applicator for custom user handling of the rotation.
                                //Useful for applying the angular warp to a character controller rather than directly to a transform.
                                m_rootMotion.HandleAngularErrorWarping(warpRot);
                            }
                        }
                        break;
                }
            }
            else //root motion is set to off
            {
                Quaternion warpRot = Quaternion.identity;
                
                if (m_fsm.CurrentStateId != (uint)EMxMStates.Event)
                {
                    //This handles angular error warping when an event is not active. Angular error warping ensures the the character always runs in the 
                    //direction you want them to go, even if there is no perfect animation that fits.
                    if (m_angularWarpType == EAngularErrorWarp.On)
                    {
                        if (CanPerformTrajectoryErrorAngularWarping())
                        {
                            Quaternion errorWarpRot = ApplyTrajectoryErrorAngularWarping(p_animator.deltaRotation, p_currentDeltaTime);

                            warpRot *= errorWarpRot;
                        }
                        else
                        {
                            LatErrorWarpAngle = 0f;
                        }
                    }
                }
                
                m_animationRoot.rotation *= warpRot; //Even with root motion off, angular error warping needs to be applied to the transform if it is on.
            }

            m_rootMotion?.FinalizeRootMotion();
        }

        //============================================================================================
        /**
        *  @brief Initializes the MxMAnimator for runtime use.
        *         
        *********************************************************************************************/
        protected void Initialize()
        {
            if (MxMPlayableGraph.IsValid())
                return;

            if (m_debugGoal)
                m_debugGoal = true;

            if (m_debugChosenTrajectory)
                m_debugChosenTrajectory = true;

            if (m_recordAnalytics)
                m_recordAnalytics = true;

            //Assertions
            Assert.IsNotNull(m_animData, "Cannot start MxMAnimator with null m_animdata (MxMAnimData)");
            Assert.IsTrue(m_animData.Length > 0, "Cannot start MxMAnimator with empty m_animdata (MxMAnimData)");
            CurrentAnimData = m_animData[0];
            Assert.IsNotNull(CurrentAnimData, "Cannot start MxMAnimator with null m_curAnimdata (m_animData[0])");
            Assert.IsTrue(CurrentAnimData.Poses.Length > 0,
                "Cannot Start MxMAnimator, Animation data has no poses");

            //Animation Root override -> defaults to the transform this component is placed on it is not specified in the inspector
            if (m_animationRoot == null)
                m_animationRoot = transform;

            p_animator = m_animationRoot.GetComponentInChildren<Animator>();

            Assert.IsNotNull(p_animator, "Cannot start MxMAnimator with no attached Animator Component");

            m_trajectoryGenerators = GetComponents<IMxMTrajectory>();

            //Setup trajectory Generators
            bool startTrajGenChosen = false;
            foreach (IMxMTrajectory trajGen in m_trajectoryGenerators)
            {
                trajGen.SetGoalRequirements(CurrentAnimData.PosePredictionTimes);

                if (!startTrajGenChosen && trajGen.IsEnabled())
                {
                    p_trajectoryGenerator = trajGen;
                    startTrajGenChosen = true;
                }
            }

            if (!startTrajGenChosen && m_trajectoryGenerators.Length > 0)
            {
                p_trajectoryGenerator = m_trajectoryGenerators[0];
            }

            Assert.IsNotNull(p_trajectoryGenerator, "Cannot start MxManimator with no attached Trajectory Generator");

            if (AnimationRoot != null)
            {
                m_rootMotion = AnimationRoot.GetComponentInChildren<IMxMRootMotion>();
            }
            else
            {
                m_rootMotion = GetComponent<IMxMRootMotion>();
            }
            
            //Setup root motion applicators
            if(m_rootMotion == null && m_rootMotionMode >= EMxMRootMotion.RootMotionApplicator)
            {
                m_rootMotion = gameObject.AddComponent<MxMRootMotionApplicator>();
                Debug.LogWarning("MxMAnimator: Initialize - Root Motion is set to use a root motion applicator but " +
                    "a root motion applicator cannot be found. A basic root motion applicator has been added at runtime.");
            }

#if UNITY_2019_1_OR_NEWER
            p_riggingIntegration = GetComponentInChildren<IMxMUnityRiggingIntegration>();
#endif

            //Identify first future trajectory point index
            for (int i = 0; i < CurrentAnimData.PosePredictionTimes.Length; ++i)
            {
                if (CurrentAnimData.PosePredictionTimes[i] > 0)
                {
                    m_startFutureTrajIndex = i;
                    break;
                }
            }

            UserPlaybackSpeedMultiplier = 1f;
            DesiredPlaybackSpeed = m_playbackSpeed;

            PosWarpThisFrame = Vector3.zero;
            RotWarpThisFrame = 0f;

            m_blendSpaceWeightings = new List<float>(5);
            m_blendSpaceClipLengths = new List<float>(5);

            //Setup Layers
            m_layers = new Dictionary<int, MxMLayer>(3);
            m_transitioningLayers = new List<MxMLayer>(3);

            //Setup internal MxM state machine
            m_fsm = new FSM();
            StateIdle idleState = new StateIdle(this);
            StateMatching matchingState = new StateMatching(this);
            StateEvent eventState = new StateEvent(this);
            StateLoopBlend loopBlendState = new StateLoopBlend(this);

            m_fsm.AddState(idleState, (uint)EMxMStates.Idle, false);
            m_fsm.AddState(matchingState, (uint)EMxMStates.Matching, true);
            m_fsm.AddState(eventState, (uint)EMxMStates.Event, false);
            m_fsm.AddState(loopBlendState, (uint)EMxMStates.LoopBlend, false);

            int trajLength = CurrentAnimData.PosePredictionTimes.Length;

            m_desiredGoalBase = new TrajectoryPoint[trajLength];
            m_desiredGoal = new TrajectoryPoint[trajLength];
#if UNITY_EDITOR
            m_debugDesiredGoal = new TrajectoryPoint[trajLength];
#endif

            m_curInterpolatedPose = new PoseData(CurrentAnimData.Poses[0]);

            int startPoseId = m_startPoseOverride;
            if (startPoseId < 0)
                startPoseId = CurrentAnimData.StartPoseId;

            if (startPoseId < CurrentAnimData.Poses.Length)
                m_chosenPose = CurrentAnimData.Poses[startPoseId];
            else
                m_chosenPose = CurrentAnimData.Poses[0];

            //Apply warping overrides
            SetWarpOverride(m_overrideWarpSettings);

            //Set up animation graphs
            if (!p_DIYPlayableGraph)
                SetupAnimator();

            m_timeSinceMotionChosen = m_timeSinceMotionUpdate = 0f;

            //Longitudinal error warping
            m_longErrorWarper = GetComponentInChildren<ILongitudinalWarper>();

            if (m_longErrorWarper == null && m_longErrorWarpType == ELongitudinalErrorWarp.Stride)
                m_longErrorWarpType = ELongitudinalErrorWarp.None;

            //initialize anim data
            foreach (MxMAnimData animData in m_animData)
            {
                if (animData != null)
                    animData.InitializeNativeData();
            }

            FetchNativeAnimData(); //We only need the native data that matches the current tag set

            int totalMaxUsedPoseCount = 0;
            for (int i = 0; i < m_animData.Length; ++i)
            {
                if (m_animData[i] == null)
                    continue;

                int usedPoseCount = m_animData[i].MaxPoseUseCount;

                if (usedPoseCount > totalMaxUsedPoseCount)
                    totalMaxUsedPoseCount = usedPoseCount;
            }

            //Setup local native arrays for job calculation cost results
            m_poseCosts = new NativeArray<float>(totalMaxUsedPoseCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            m_trajCosts = new NativeArray<float>(totalMaxUsedPoseCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            m_chosenPoseId = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            SetupJobDelegates();

            if(m_calibrationOverride != null)
            {
                m_calibrationOverride.CheckUpdateAnimData(CurrentAnimData);

                if (m_calibrationOverride.IsCompatibleWith(CurrentAnimData))
                    m_curCalibData = m_calibrationOverride.GetCalibrationSet(0);
                else
                    m_curCalibData = CurrentAnimData.CalibrationSets[0];
            }
            else
            {
                m_curCalibData = CurrentAnimData.CalibrationSets[0];
            }

            SetupExtensions();

            m_onSetupComplete.Invoke();
            IsInitialized = true;

#if UNITY_EDITOR
            DebugData = new MxMDebugger(this, m_maxMixCount, CurrentAnimData.PosePredictionTimes.Length);
#endif 
        }

        //============================================================================================
        /**
        *  @brief Sets up the animation playable graph and prepares it for motion matching playback.
        *         
        *********************************************************************************************/
        private void SetupAnimator()
        {
#if UNITY_EDITOR
            m_debugPoses = false;
#endif
            MxMPlayableGraph = PlayableGraph.Create(transform.name + "_MotionMatching");
            MxMPlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            m_motionMatchPlayable = ScriptPlayable<MotionMatchingPlayable>.Create(MxMPlayableGraph, 1);
            m_motionMatchPlayable.SetTraversalMode(PlayableTraversalMode.Passthrough);
            m_motionMatchPlayable.GetBehaviour().SetMxMAnimator(this);

            m_animationLayerMixer = AnimationLayerMixerPlayable.Create(MxMPlayableGraph, Mathf.Max(3, m_animControllerLayer + 1));

            if (m_transitionMethod == (ETransitionMethod)3) //Todo: Reintroduce the inertialization module.
            {
                m_inertialBlendModule = new InertialBlendModule();
                AnimationScriptPlayable inertialPlayable = m_inertialBlendModule.Initialize(p_animator, null, MxMPlayableGraph, m_animationLayerMixer);
                m_motionMatchPlayable.ConnectInput(0, inertialPlayable, 0);
            }
            else
            {
                m_motionMatchPlayable.ConnectInput(0, m_animationLayerMixer, 0);
            }
            m_motionMatchPlayable.SetInputWeight(0, 1f);

            m_animationMixer = AnimationMixerPlayable.Create(MxMPlayableGraph, m_maxMixCount);
            m_animationLayerMixer.ConnectInput(0, m_animationMixer, 0);
            m_animationLayerMixer.SetInputWeight(0, 1f);
            m_animationLayerMixer.SetInputWeight(1, 0f);
            m_animationLayerMixer.SetInputWeight(2, 0f);

            if(p_animator.runtimeAnimatorController != null)
            {
                m_animControllerPlayable = AnimatorControllerPlayable.Create(MxMPlayableGraph,
                    p_animator.runtimeAnimatorController);


                m_animationLayerMixer.ConnectInput(m_animControllerLayer, m_animControllerPlayable, 0);

                if (m_animatorControllerMask != null)
                    m_animationLayerMixer.SetLayerMaskFromAvatarMask((uint)m_animControllerLayer, m_animatorControllerMask);

                //p_animator.runtimeAnimatorController = null;
            }

            if (m_transitionMethod == ETransitionMethod.Inertialization) //ETransitionMethod.Inertialization
            {
                m_animationMixer.SetInputCount(CurrentAnimData.Clips.Length);

                for (int i = 0; i < CurrentAnimData.Clips.Length; ++i)
                {
                    var clip = CurrentAnimData.Clips[i];

                    if (clip == null)
                        continue;

                    var clipPlayable = AnimationClipPlayable.Create(MxMPlayableGraph, clip);
                    clipPlayable.SetApplyFootIK(m_applyHumanoidFootIK);
                    clipPlayable.SetApplyPlayableIK(m_applyPlayableIK);
                    //clipPlayable.Play()

                    m_animationMixer.ConnectInput(i, clipPlayable, 0);
                    m_animationMixer.SetInputWeight(i, 0f);
                    
                }
                
                m_activeSlots = new List<int>(8);
                m_inertializationAnimState.Weight = 1f;
                m_inertializationAnimState.HighestWeight = 1f;
                m_inertializationAnimState.AnimType = EMxMAnimtype.IdleSet;
                m_inertializationAnimState.StartPoseId = m_chosenPose.PoseId;
                m_inertializationAnimState.Age = 1000f;
                m_inertializationAnimState.DecayAge = 0f;
                m_inertializationAnimState.BlendStatus = EBlendStatus.Chosen;
                
                m_animationStates = new MxMPlayableState[1];
                SetupPose(ref m_chosenPose);
            }
            else //Blending Setup
            {
                m_animationStates = new MxMPlayableState[m_maxMixCount];
                for (int i = 0; i < m_animationStates.Length; ++i)
                {
                    m_animationStates[i] = new MxMPlayableState(i, ref m_animationMixer);
                }

                ref MxMPlayableState startState = ref m_animationStates[0];
                startState.Weight = 1f;
                startState.HighestWeight = 1f;
                startState.AnimType = EMxMAnimtype.IdleSet;
                startState.StartPoseId = m_chosenPose.PoseId;
                startState.Age = 1000f;
                startState.DecayAge = 0f;
                startState.BlendStatus = EBlendStatus.Dominant;

                AnimationClip clip = CurrentAnimData.Clips[m_chosenPose.PrimaryClipId];

                var clipPlayable = AnimationClipPlayable.Create(MxMPlayableGraph, clip);
                clipPlayable.SetApplyFootIK(m_applyHumanoidFootIK);
                clipPlayable.SetApplyPlayableIK(m_applyPlayableIK);

                startState.TargetPlayable = clipPlayable;

                MxMPlayableGraph.Connect(startState.TargetPlayable, 0, m_animationMixer, 0);
                startState.TimeX2 = m_chosenPose.Time;
                startState.Speed = m_playbackSpeed;
                m_animationMixer.SetInputWeight(0, 1f);

                m_dominantBlendChannel = m_primaryBlendChannel = 0;
            }
            
            m_dominantPose = m_chosenPose;

            m_animMixerConnected = true;

            AnimationPlayableUtilities.Play(p_animator, m_motionMatchPlayable, MxMPlayableGraph);

#if UNITY_2019_1_OR_NEWER
            if(p_riggingIntegration != null)
                p_riggingIntegration.Initialize(MxMPlayableGraph, p_animator);
#endif
            
            MxMSearchManager.Instance.RegisterMxMAnimator(this);
        }

        //============================================================================================
        /**
        *  @brief To be called externally by a user that is using MxM with a custom playable graph. 
        *  
        *  This will only work if DIYPlayableGraph is checked in the MxMAnimator inspector. The user
        *  must pass a create playable graph and will recieve a playable in return which they must
        *  plug into their playable graph, wherever they desire. MxM will still manage and update it's
        *  portion of the playable graph independently.
        *  
        *  #param [ref PlayableGraph] - A reference to a created and valid playable graph
        *  
        *  #return ScriptPlayable<MotionMatchintPlayable> - A created and setup motion matching playable that can be plugged into any graph
        *      
        *         
        *********************************************************************************************/
        public ScriptPlayable<MotionMatchingPlayable> CreateMotionMatchingPlayable(ref PlayableGraph a_playableGraph)
        {
            if(!p_DIYPlayableGraph)
            {
                Debug.LogError("Attempting to use a DIYPlayableGraph feature without checking DIYPlayableGraph in the MxMAnimator. Aborting Operation");
                return m_motionMatchPlayable;
            }

            if(!a_playableGraph.IsValid())
            {
                Debug.LogError("Trying to create a MotionMatchingPlayable, but the passed PlayableGraph is invalid");
            }

            Initialize();

            MxMPlayableGraph = a_playableGraph;

            m_motionMatchPlayable = ScriptPlayable<MotionMatchingPlayable>.Create(MxMPlayableGraph, 1);
            m_motionMatchPlayable.GetBehaviour().SetMxMAnimator(this);

            m_animationLayerMixer = AnimationLayerMixerPlayable.Create(MxMPlayableGraph, Mathf.Max(3, m_animControllerLayer + 1));
            m_motionMatchPlayable.ConnectInput(0, m_animationLayerMixer, 0);
            m_motionMatchPlayable.SetInputWeight(0, 1f);


            m_animationMixer = AnimationMixerPlayable.Create(MxMPlayableGraph, m_maxMixCount);

            m_animationLayerMixer.ConnectInput(0, m_animationMixer, 0);
            m_animationLayerMixer.SetInputWeight(0, 1f);

            for(int i = 1; i < m_animationLayerMixer.GetInputCount(); ++i)
            {
                m_animationLayerMixer.SetInputWeight(i, 0f);
            }

            if (m_autoCreateAnimatorController && p_animator.runtimeAnimatorController != null)
            {
                m_animControllerPlayable = AnimatorControllerPlayable.Create(MxMPlayableGraph,
                    p_animator.runtimeAnimatorController);

                m_animationLayerMixer.ConnectInput(m_animControllerLayer, m_animControllerPlayable, 0);

                if (m_animatorControllerMask != null)
                    m_animationLayerMixer.SetLayerMaskFromAvatarMask((uint)m_animControllerLayer, m_animatorControllerMask);
            }

            m_animationStates = new MxMPlayableState[m_maxMixCount];
            for (int i = 0; i < m_animationStates.Length; ++i)
                m_animationStates[i] = new MxMPlayableState(i, ref m_animationMixer);

            ref MxMPlayableState startState = ref m_animationStates[0];
            startState.Weight = 1f;
            startState.HighestWeight = 1f;
            startState.AnimType = EMxMAnimtype.IdleSet;
            startState.StartPoseId = m_chosenPose.PoseId;
            startState.Age = 1000f;
            startState.DecayAge = 0f;
            startState.BlendStatus = EBlendStatus.Dominant;

            AnimationClip clip = CurrentAnimData.Clips[m_chosenPose.PrimaryClipId];

            var clipPlayable = AnimationClipPlayable.Create(MxMPlayableGraph, clip);
            clipPlayable.SetApplyFootIK(m_applyHumanoidFootIK);
            clipPlayable.SetApplyPlayableIK(m_applyPlayableIK);

            startState.TargetPlayable = clipPlayable;

            m_animationMixer.ConnectInput(0, startState.TargetPlayable, 0);
            startState.TimeX2 = m_chosenPose.Time;
            startState.Speed = m_playbackSpeed;

            m_animationMixer.SetInputWeight(0, 1f);

            m_dominantBlendChannel = m_primaryBlendChannel = 0;
            m_dominantPose = m_chosenPose;

            m_animMixerConnected = true; //MxM trusts that the user connects the mixer to the playable graph straight away

            return m_motionMatchPlayable;
        }

        //============================================================================================
        /**
        *  @brief Sets up any MxM extension components that are attached. This is called during the
        *  Initialize function.
        *         
        *********************************************************************************************/
        private void SetupExtensions()
        {
            //Extensions
            if (m_phase1Extensions == null)
                m_phase1Extensions = new List<IMxMExtension>(2);

            if (m_phase2Extensions == null)
                m_phase2Extensions = new List<IMxMExtension>(2);

            if (m_postExtensions == null)
                m_postExtensions = new List<IMxMExtension>(2);

            IMxMExtension[] attachedExtensions = GetComponents<IMxMExtension>();

            foreach(IMxMExtension extension in attachedExtensions)
            {
                extension.Initialize();

                if (extension.DoUpdatePhase1)
                    m_phase1Extensions.Add(extension);

                if (extension.DoUpdatePhase2)
                    m_phase2Extensions.Add(extension);

                if (extension.DoUpdatePost)
                    m_postExtensions.Add(extension);
            }
        }
        
        //============================================================================================
        /**
        *  @brief This is the first phase update for the MxMAnimator. It is called by either of the 
        *  MonoBehaviour FixedUpdate or Update method depending on the 'Animator' update mode. 
        *  
        *  Depending on the state of the MxMAnimator, this first phase update is responsible for 
        *  scheduling the motion matching jobs. 
        *         
        *********************************************************************************************/
        public void MxMUpdate_Phase1(float a_deltaTime)
        {
            p_currentDeltaTime = a_deltaTime;
            
            if (m_phase1Extensions != null)
            {
                foreach (IMxMExtension extension in m_phase1Extensions)
                {
                    if (extension.IsEnabled)
                        extension.UpdatePhase1();
                }
            }

            UpdateRequireTags();

            float animTimeStep = p_currentDeltaTime * m_playbackSpeed;

            m_timeSinceMotionChosen += animTimeStep;
            m_timeSinceMotionUpdate += animTimeStep;

            //Update the MxM internal state machine
            m_fsm.Update_Phase1();
        }

        //============================================================================================
        /**
        *  @brief This function is called by the MotionMatchingPlayable in the PrepareFrame phase of
        *  the Unity Animator update. It is the highest level 'Second Phase' of the MxM Update loop.
        *  
        *  It calls the prepareFrame function on the current internal state of the MxMAniamtor. In the
        *  case of the motion matching state this will run the second phase where pose and trajectory jobs
        *  are collected and the minima job will run to determine the next animation clip to play.
        *         
        *********************************************************************************************/
        public void MxMUpdate_Phase2()
        {
            m_fsm.Update_Phase2();

            if (m_transitionMethod == ETransitionMethod.Blend)
            {
                UpdateBlending();
            }
            else if (m_transitionMethod == ETransitionMethod.Inertialization)
            {
                m_inertializationAnimState.Age += p_currentDeltaTime * m_playbackSpeed;
            }

            UpdateComplexAnims();
            UpdatePlaybackSpeed();
            UpdateLayers();

            if (m_phase2Extensions != null)
            {
                foreach (IMxMExtension extension in m_phase2Extensions)
                {
                    if (extension.IsEnabled)
                        extension.UpdatePhase2();
                }
            }

#if UNITY_EDITOR
            if (m_recordAnalytics)
                RecordDebugState();
#endif       
        }

        //============================================================================================
        /**
        *  @brief 
        *         
        *********************************************************************************************/
        public void MxMLateUpdate()
        {
            if (CurrentAnimData == null)
            {
                return;
            }
            
            if (m_fsm.CurrentStateId == (int)EMxMStates.Matching && m_poseSearchThisFrame)
            {
                m_minimaJobHandle.Complete();
                FinalizePoseSearch(m_chosenPoseId[0]);
                m_poseSearchThisFrame = false;
            }
            
            UpdateFootSteps();

            if (m_postExtensions != null)
            {
                foreach (IMxMExtension extension in m_postExtensions)
                {
                    if (extension.IsEnabled)
                        extension.UpdatePost();
                }
            }
        }

        //============================================================================================
        /**
        *  @brief This is the 'first phase' update of the MxMAnimator 'Motion Matching' internal state.
        *  
        *  It schedules all the motion matching jobs to be collected before the Animator update and applied.
        *  It also handles any logic that must run for the motion matching state before jobs are scheduled.
        *  E.g. checking if an idle state transition is required.
        *  
        *         
        *********************************************************************************************/
        private void UpdateMatching_Phase1()
        {
            //Check if an idle has been triggered and transition to the idle state instead of scheduling jobs
            if (DetectIdle())
            {
                if (BeginIdle())
                    return;
            }

            m_enforcePoseSearch = false;
            //m_timeDialation = 0f; //Reset the time dialation

            AnimationClip curClip = CurrentAnimData.Clips[m_chosenPose.PrimaryClipId];

            //Determine if a clip change must be enforced
             if (!curClip.isLooping) 
             {
                 float blendTime = 0f;
            
                 if (m_blendOutEarly)
                 {
                     blendTime = m_matchBlendTime * m_animationStates[m_primaryBlendChannel].Weight * m_playbackSpeed;
                 }

                if (m_chosenPose.AnimType == EMxMAnimtype.Composite)
                {
                   /* if (m_timeSinceMotionChosen + m_chosenPose.Time + blendTime > curClip.length + CurrentAnimData.PoseInterval * 5f)
                        m_enforcePoseSearch = true;*/
                }
                else
                {
                    if (m_timeSinceMotionChosen + m_chosenPose.Time + blendTime > curClip.length)
                        m_enforcePoseSearch = true;
                }
            }
             
             //Update blend spaces if the the dominant animation is a blend space
             if (m_dominantPose.AnimType == EMxMAnimtype.BlendSpace)
                 UpdateBlendSpaces();

            if ((m_curInterpolatedPose.GenericTags & EGenericTags.DisableMatching) != EGenericTags.DisableMatching)
            {
                ComputeCurrentPose();
                
                // if ((CurrentInterpolatedPose.Tags & ETags.DoNotUse) == ETags.DoNotUse)
                // {
                //     m_enforcePoseSearch = true;
                // }
                
                GenerateGoalTrajectory(p_trajectoryGenerator.GetCurrentGoal());
                
                if (!m_doNotSearch && 
                    (m_timeSinceMotionUpdate > m_updateInterval || m_enforcePoseSearch))
                {
                    if (MxMSearchManager.Instance.RequestPoseSearch(this, m_timeSinceMotionUpdate - m_updateInterval, m_enforcePoseSearch))
                    {
                        m_poseSearchThisFrame = true;
                        SchedulePoseSearch(); //Schedule the motion matching jobs
                    }
                }
            }
            else
            {
                m_timeSinceMotionUpdate = 0f;
                ComputeCurrentPose();
                GenerateGoalTrajectory(p_trajectoryGenerator.GetCurrentGoal());
            }
        }

        //============================================================================================
        /**
        *  @brief This function applies the results of the motion matching jobs. This is part of the
        *  second phase update. 
        *         
        *********************************************************************************************/
        private void UpdateMatching_Phase2()
        {
            if (m_poseSearchThisFrame)
            {
                // m_timeSinceMotionUpdate = Mathf.Clamp(m_timeSinceMotionUpdate - m_updateInterval, 0f, m_updateInterval - p_currentDeltaTime);
                m_timeSinceMotionUpdate = 0f; //Todo: what about pro-rata (see above)?
                
                //Complete the pose and trajectory jobs
                m_poseJobHandle.Complete();
                m_trajJobHandle.Complete();
                
                if (m_priorityUpdate) //For priority characters we update immediately
                {
                    int chosenPoseId = ComputeMinimaJob(m_curInterpolatedPose.PoseId,
                        m_enforcePoseSearch, CurrentNativeAnimData.UsedPoseIds.Length);
                    
                    FinalizePoseSearch(chosenPoseId);
                    m_poseSearchThisFrame = false;
                }
                else //For non priority characters we run the minima jobs in parallel and collect them in late update.
                {   //Todo: Batch these jobs to save on scheduling overhead
                    GenerateMinimaJob(m_curInterpolatedPose.PoseId,
                        m_enforcePoseSearch, CurrentNativeAnimData.UsedPoseIds.Length);
                }
#if UNITY_EDITOR
                m_updateThisFrame = true;
#endif
            }
#if UNITY_EDITOR
            else
            {
                m_updateThisFrame = false;
            }
#endif
            
            //Perform longitudinal error warping if it is enabled.
            if (m_longErrorWarpType > ELongitudinalErrorWarp.None)
            {
                if ((m_curInterpolatedPose.GenericTags & EGenericTags.DisableWarp_TrajLong)
                    != EGenericTags.DisableWarp_TrajLong)
                {
                    ApplyTrajectoryErrorLongWarping();
                }
            }
        }

        //============================================================================================
        /**
        *  @brief This function schedules the pose and trajectory cost function jobs for calculation
        *  and storing in native arrays for a future minima job to find the minima.
        *  
        *  Before the jobs are scheduled, the current pose and desired trajectory need to be determined.
        *  
        *  Note that the poseJob and trajectoryJob are scheduled through a delegate. This delegate calls 
        *  a Job generator function which is setup dependent on the pose and trajectory configuration of
        *  the MxMAnimator.See SetupJobDelegates() function for more details.
        *         
        *********************************************************************************************/
        private void SchedulePoseSearch()
        {
            if(!m_enforcePoseSearch && m_nextPoseToleranceTest)
            {
                ref PoseData nextPose = ref CurrentAnimData.Poses[CurrentInterpolatedPose.NextPoseId]; //Potential out of range here?

                if ((nextPose.Tags & ETags.DoNotUse) != ETags.DoNotUse)
                {
                    //If the next pose from the current animation is close enough, skip the search
                    if (NextPoseToleranceTest(ref nextPose))
                    {
                        m_timeSinceMotionUpdate = 0f;
                        m_poseSearchThisFrame = false;
                        return;
                    }
                }
            }

            int usedPoseCount = CurrentNativeAnimData.UsedPoseIds.Length;

            m_poseJobHandle = m_poseJobDelegate(usedPoseCount);
            m_trajJobHandle = m_trajJobDelegate(usedPoseCount);
        }

        //============================================================================================
        /**
        *  @brief This function is part of the second phase update. It completes the poseJob and trajectory
        *  job (if not already completed) and then starts a minima job which combines the calculated
        *  costs and find the minima
        *         
        *********************************************************************************************/
        private void FinalizePoseSearch(int a_chosenPoseId)
        {
#if UNITY_EDITOR
            m_lastPoseCost = m_poseCosts[a_chosenPoseId];
            m_lastTrajectoryCost = m_trajCosts[a_chosenPoseId];
            m_lastChosenCost = m_lastPoseCost + m_lastTrajectoryCost;
#endif
            int bestPoseId = CurrentNativeAnimData.UsedPoseIds[a_chosenPoseId];

            ref PoseData bestPose = ref CurrentAnimData.Poses[bestPoseId];

            bool winnerAtSameLocation = bestPose.AnimId == m_curInterpolatedPose.AnimId
                && bestPose.AnimType == m_curInterpolatedPose.AnimType
                && (Mathf.Abs(bestPose.Time - m_curInterpolatedPose.Time) < 0.2f
                || CurrentAnimData.Clips[m_curInterpolatedPose.PrimaryClipId].isLooping);

            if (!winnerAtSameLocation)
            {
                winnerAtSameLocation = bestPose.AnimId == m_chosenPose.AnimId
                    && bestPose.AnimType == m_chosenPose.AnimType
                    && (Mathf.Abs(bestPose.Time - m_chosenPose.Time) < 0.2f
                    || CurrentAnimData.Clips[m_chosenPose.PrimaryClipId].isLooping);
            }

            //Only change the clip if its not at the same location or if it has been enforced to change
            if (!winnerAtSameLocation || m_enforcePoseSearch)
            {
                m_timeSinceMotionChosen = m_timeSinceMotionUpdate;

                m_chosenPose = bestPose;
                TransitionToPose(ref m_chosenPose);
            }

#if UNITY_EDITOR
            if (m_recordAnalytics)
            {
                bool winnerAtExactlyTheSameLocation = bestPoseId == m_curInterpolatedPose.PoseId;

                if (!winnerAtExactlyTheSameLocation)
                {
                    DebugData.UsePose(bestPoseId);
                }
            }
#endif
        }

        //============================================================================================
        /**
        *  @brief Updates the blending logic for the motion matching mixer playable. 
        *  
        *  Any clip that is not the current chosen clip will be gradually faded out to 0 weight while
        *  the current chosen clip will be blended in to a maximum of 1f weight. The blend weight is 
        *  a sin function of the clips age (how long it has been active) and the blend time. For decaying
        *  clips the age used is the 'decay age' which is how long it has been decaying for. 
        *  
        *  This function also updates the PlayableStates for each slot to keep track of their data.
        *  
        *  Note that all weights are normalized when applied to the playables. 
        *         
        *********************************************************************************************/
        private void UpdateBlending()
        {
            float highestBlendVal = 0f;
            int highestBlendChannel = 0;
            float totalBlendPower = 0f;
            int totalBlendCount = 0;
            for (int i = 0; i < m_animationStates.Length; ++i)
            {
                ref MxMPlayableState animState = ref m_animationStates[i];

                if (animState.BlendStatus == EBlendStatus.None)
                    continue;

                float animBlendVal;
                if (i == m_primaryBlendChannel)
                {
                    animState.Age += p_currentDeltaTime * m_playbackSpeed * animState.AnimSpeedMod; //Todo: Should speed mod be included here? it will affect blending speed
                    animBlendVal = Mathf.Sin((Mathf.PI / 2f) * Mathf.Clamp01(animState.Age / m_matchBlendTime));

                    animState.HighestWeight = animState.Weight = animBlendVal;

                    ++totalBlendCount;
                }
                else
                {
                    animBlendVal = animState.HighestWeight * (1f - Mathf.Sin((Mathf.PI / 2f)
                        * Mathf.Clamp01(animState.DecayAge / m_matchBlendTime)));

                    if (animBlendVal < 0.0001f)
                    {
#if UNITY_2019_1_OR_NEWER
                        if (p_riggingIntegration != null)
                            p_riggingIntegration.FixRigTransforms();
#endif
                        animBlendVal = animState.Weight = animState.HighestWeight = 0f;
                        animState.Age = 0f;
                        animState.DecayAge = 0f;
                        animState.BlendStatus = EBlendStatus.None;
                    }
                    else
                    {
                        animState.Weight = animBlendVal;
                        animState.Age += p_currentDeltaTime * m_playbackSpeed * animState.AnimSpeedMod; //Todo: Should speed mod be included here? it will affect blending speed
                        animState.DecayAge += p_currentDeltaTime * m_playbackSpeed;
                        ++totalBlendCount;
                    }
                }

                totalBlendPower += animBlendVal;

                if (animBlendVal > highestBlendVal)
                {
                    highestBlendVal = animBlendVal;
                    highestBlendChannel = i;
                }
            }

            if (m_dominantBlendChannel != highestBlendChannel)
            {
                //First Check if the current dominant blend channel is 'inside' a footstep (i.e. grounded)
                int leftGrounded = -1; 
                int rightGrounded = -1;
                if (m_dominantPose.TracksId > -1)
                {
                    FootstepTagTrackData leftSteps = CurrentAnimData.LeftFootSteps[m_dominantPose.TracksId];
                    FootstepTagTrackData rightSteps = CurrentAnimData.RightFootSteps[m_dominantPose.TracksId];

                    ref MxMPlayableState oldDominantState = ref m_animationStates[m_dominantBlendChannel];

                    leftGrounded = leftSteps.IsGrounded(oldDominantState.Time, ref m_cachedLastLeftFootstepId);
                    rightGrounded = rightSteps.IsGrounded(oldDominantState.Time, ref m_cachedLastRightFootstepId);
                }
                m_cachedLastLeftFootstepId = 0;
                m_cachedLastRightFootstepId = 0;

                m_animationStates[m_dominantBlendChannel].BlendStatus = EBlendStatus.Decaying;
                m_dominantBlendChannel = highestBlendChannel;

                //Update the new dominant state
                ref MxMPlayableState playableState = ref m_animationStates[m_dominantBlendChannel];
                playableState.BlendStatus = EBlendStatus.Dominant;

                int dominantPoseId = playableState.StartPoseId;
                m_dominantPose = CurrentAnimData.Poses[dominantPoseId];
                
                //Check if a footstep tag was perhaps missed in the transition?
                if (m_dominantPose.TracksId > -1)
                {
                    float time = playableState.Time;
                    //If the footstep is not grounded in the previous animation (i.e. grounded == -1) and then it is grounded in the new dominant animation (i.e.footStepId > -1) then we trigger an animation
                    if (leftGrounded == -1 && m_timeSinceLastLeftFootstep >= m_minFootstepInterval)
                    {
                        FootstepTagTrackData leftSteps = CurrentAnimData.LeftFootSteps[m_dominantPose.TracksId];
                        int footStepId = leftSteps.IsGrounded(playableState.Time, ref m_cachedLastLeftFootstepId);

                        if (footStepId > -1)
                        {
                            m_onLeftFootStepStart.Invoke(leftSteps.FootSteps[footStepId]);
                            m_timeSinceLastLeftFootstep = 0f;
                        }
                    }

                    if (rightGrounded == -1 && m_timeSinceLastRightFootstep >= m_minFootstepInterval)
                    {
                        FootstepTagTrackData rightSteps = CurrentAnimData.RightFootSteps[m_dominantPose.TracksId];
                        int footStepId = rightSteps.IsGrounded(playableState.Time, ref m_cachedLastRightFootstepId);

                        if (footStepId > -1)
                        {
                            m_onRightFootStepStart.Invoke(rightSteps.FootSteps[footStepId]);
                            m_timeSinceLastRightFootstep = 0f;
                        }
                    }
                }
            }

            //This guarantees that there is some animation to play
            if(totalBlendPower < Mathf.Epsilon)
                m_animationStates[m_dominantBlendChannel].Weight = totalBlendPower = 0.1f;

            float blendNormalizeFactor = 1f / totalBlendPower;

            for (int i = 0; i < m_animationStates.Length; ++i)
            {
                m_animationMixer.SetInputWeight(i, m_animationStates[i].Weight * blendNormalizeFactor);
            }
        }

        //============================================================================================
        /**
        *  @brief Updates complex animations such as composites with runtime splicing enabled.
        *         
        *********************************************************************************************/
        private void UpdateComplexAnims()
        {
            // if (m_transitionMethod == ETransitionMethod.Inertialization)
            // {
            //     return;
            // }
            
            for (int i = 0; i < m_animationStates.Length; ++i)
            {
                ref MxMPlayableState playableState = ref m_animationStates[i];

                if (playableState.BlendStatus == EBlendStatus.None)
                    continue;
                
                if (!playableState.TargetPlayable.IsValid())
                    continue;
                
                if (playableState.AnimType == EMxMAnimtype.Composite)
                {
                    if (playableState.TargetPlayable.GetInputCount() == 0)
                        continue;

                    if (playableState.TargetPlayable.GetInputWeight(0) < 0.5f)
                        continue;

                    ref CompositeData compositeData = ref CurrentAnimData.Composites[playableState.AnimId];

                    float time = playableState.Age + playableState.StartTime;

                    if (time > compositeData.ClipALength)
                    {
                        playableState.TargetPlayable.SetInputWeight(0, 0f);
                        playableState.TargetPlayable.SetInputWeight(1, 1f);

                        var clipPlayableB = playableState.TargetPlayable.GetInput(1);
                        clipPlayableB.SetTime(time - compositeData.ClipALength);
                        clipPlayableB.SetTime(time - compositeData.ClipALength);
                    }
                }
            }
        }

        //============================================================================================
        /**
        *  @brief Updates the playback speed blending. Instantly changing playback speed of animations
        *  can be jarring. Therefore, once desired playback speed is set by the user, this function
        *  smoothly changes the actual playback speed to meet the desired over time.
        *         
        *********************************************************************************************/
        private void UpdatePlaybackSpeed()
        {
            float finalDesiredSpeed = DesiredPlaybackSpeed * UserPlaybackSpeedMultiplier;

            if (Mathf.Abs(finalDesiredSpeed - m_playbackSpeed) > 0.001f)
            {
                m_playbackSpeed = Mathf.MoveTowards(m_playbackSpeed, finalDesiredSpeed,
                    m_playbackSpeedSmoothRate * p_currentDeltaTime);
            }
        }

        //============================================================================================
        /**
        *  @brief Generates the goal trajectory for the motion matching algorithm to compare with 
        *  for the cost minima function. It has multiple purposes depending on user settings. 
        *  
        *  Firstly, if 'Transform Goal' is check in the MxManimator inspector, it will set the goal relative
        *  to the animation root (or the character). 
        *  
        *  The second purpose, if 'Apply Trajectory Blending' is checked, to blend the goal provided by player 
        *  input with that of the current animation with a falloff over the time horizon of the trajectory. 
        *  This allows for a much more realistic trajectory (but it can be un-responsive).
        *  
        *  @param [TrajectoryPoint[]] a_goalSource - the source goal generated by the trajecotry generator
        *         
        *********************************************************************************************/
        private void GenerateGoalTrajectory(TrajectoryPoint[] a_goalSource)
        {
            switch(m_pastTrajectoryMode)
            {
                case EPastTrajectoryMode.ActualHistory:
                    {
                        for (int i = 0; i < a_goalSource.Length; ++i)
                            m_desiredGoalBase[i] = a_goalSource[i];

                        if (m_transformGoal)
                            Goal.SetRelativeTo(m_desiredGoalBase, m_animationRoot);
                    }
                    break;
                case EPastTrajectoryMode.CopyFromCurrentPose:
                    {
                        for (int i = m_startFutureTrajIndex; i < a_goalSource.Length; ++i)
                            m_desiredGoalBase[i] = a_goalSource[i];

                        if (m_transformGoal)
                            Goal.SetRelativeTo(m_desiredGoalBase, m_animationRoot, m_startFutureTrajIndex);

                        for(int i = 0; i < m_startFutureTrajIndex; ++i)
                        {
                            m_desiredGoalBase[i] = m_curInterpolatedPose.Trajectory[i];
                        }
                    }
                    break;
            }

            if (m_applyTrajectoryBlending)
            {
                //Current Goal
                TrajectoryPoint[] curGoal = CurrentAnimData.Poses[m_curInterpolatedPose.PoseId].Trajectory;

                //Blend the goals
                float[] posePredictionTimes = CurrentAnimData.PosePredictionTimes;
                float maxTime = posePredictionTimes[posePredictionTimes.Length - 1];
                float minTime = posePredictionTimes[0];
                for (int i = 0; i < posePredictionTimes.Length; ++i)
                {
                    float time = posePredictionTimes[i];

                    float blendRatio;

                    if (time > 0f)
                    {
                        blendRatio = (1f - (time / maxTime)) * m_trajectoryBlendingWeight;
                    }
                    else
                    {
                        blendRatio = (1f - (time / minTime) * m_trajectoryBlendingWeight);
                    }

                    TrajectoryPoint desiredPoint = m_desiredGoalBase[i];
                    TrajectoryPoint curPoint = curGoal[i];

                    //Blend position accordingly
                    Vector3 pos = Vector3.Lerp(desiredPoint.Position,
                        curPoint.Position, blendRatio);

                    //Blend facing angle accordingly
                    float fAngle = Mathf.LerpAngle(desiredPoint.FacingAngle,
                        curPoint.FacingAngle, blendRatio);

                    m_desiredGoal[i] = new TrajectoryPoint(pos, fAngle);
                }
            }
            else
            {
                for (int i = 0; i < m_desiredGoalBase.Length; ++i)
                {
                    m_desiredGoal[i] = m_desiredGoalBase[i];
                }
            }
        }

        //============================================================================================
        /**
        *  @brief Computes the cost to go from the current pose to the candidate pose. Essentially this
        *  is the 'difference' between the two poses.
        *  
        *  Note: Most pose cost calculations for MxM are contained within multi-threaded jobs. This
        *  function is used very infrequently to search a very small subset of poses for the lowest 
        *  cost.
        *  
        *  @param [ref PoseData] a_candidate - the candidate pose to compare to the current pose
        *         
        *********************************************************************************************/
        public float ComputePoseCost(ref PoseData a_candidate)
        {
            float poseCost = 0f;

            //Local Velcity Cost
            float poseVelocityCost = Vector3.Distance(m_curInterpolatedPose.LocalVelocity, a_candidate.LocalVelocity)
                * m_curCalibData.PoseVelocityWeight;

            float jointCosts = 0f;

            //PoseData nextCandidate = CurrentAnimData.Poses[a_candidate.NextPoseId];
            //PoseData nextPose = CurrentAnimData.Poses[m_curInterpolatedPose.NextPoseId];

            JointData curJointData;
            JointData candJointData;
            for (int i = 0; i < m_curInterpolatedPose.JointsData.Length; ++i)
            {
                curJointData = m_curInterpolatedPose.JointsData[i];
                candJointData = a_candidate.JointsData[i];

                jointCosts += Vector3.Distance(curJointData.Position,
                                candJointData.Position) * m_curCalibData.JointPositionWeights[i];

                jointCosts += Vector3.Distance(curJointData.Velocity,
                                candJointData.Velocity) * m_curCalibData.JointVelocityWeights[i];
            }

            poseCost = poseVelocityCost + (jointCosts * m_curCalibData.PoseAspectMultiplier);

            return poseCost;
        }

        //============================================================================================
        /**
        *  @brief Compustes the trajetory cost of transitioning to a pose. Essentially this is the 
        *  'difference' between the current trajectory and the trajectory of the passed pose data.
        *  However, it also takes into consideration 
        *  multipliers.
        *  
        *  Note: Most trajectory cost calculations for MxM are contained within multi-threaded jobs. This
        *  function is used very infrequently to search a very small subset of trajectories for the lowest 
        *  cost.
        *  
        *  @param [ref PoseData] a_candidate - the candidate pose to compare to the current pose
        *         
        *********************************************************************************************/
        public float ComputeTrajectoryCost(ref PoseData _candidate)
        {
            float trajectoryCost = 0f;

            for(int i = 0; i < m_curInterpolatedPose.Trajectory.Length; ++i)
            {
                ref TrajectoryPoint trajPoint = ref m_curInterpolatedPose.Trajectory[i];
                ref TrajectoryPoint candidatePoint = ref _candidate.Trajectory[i];

                trajectoryCost += Vector3.Distance(trajPoint.Position, candidatePoint.Position)
                    * m_curCalibData.TrajPosMultiplier;

                trajectoryCost += Mathf.DeltaAngle(trajPoint.FacingAngle, candidatePoint.FacingAngle)
                    * m_curCalibData.TrajFAngleMultiplier;
            }

            return trajectoryCost * (1f - m_curCalibData.PoseTrajectoryRatio);
        }

        //============================================================================================
        /**
        *  @brief Determines the current pose by interpolating between pre-recorded poses. 
        *  
        *  Note that it is not necessary to interpolate the trajectory for the current pose as 
        *  current trajectory is obtained from the prediction model.
        *         
        *********************************************************************************************/
        private void ComputeCurrentPose()
        {
            float chosenClipLength = 0f;
            bool chosenClipLooping = false;

            if (m_chosenPose.AnimType != EMxMAnimtype.Composite)
            {
                AnimationClip chosenClip = CurrentAnimData.Clips[m_chosenPose.PrimaryClipId];
                chosenClipLength = chosenClip.length;
                chosenClipLooping = chosenClip.isLooping;
            }
            else
            {
                ref CompositeData compositeData = ref CurrentAnimData.Composites[m_chosenPose.AnimId];
                chosenClipLength = compositeData.Length;
            }
            //Determine the next Chosen Pose

            float timePassed = m_timeSinceMotionChosen;
            int poseIndex = m_chosenPose.PoseId;

            //Determine if the new time is out of bounds of the chosen pose clip
            float newChosenTime = m_chosenPose.Time + timePassed;
            if (newChosenTime >= chosenClipLength)
            {
                //either clamp or loop the pose by manipulating time passed
                if (chosenClipLooping)
                {
                    newChosenTime = MxMUtility.WrapAnimationTime(newChosenTime, chosenClipLength); //Loop
                }
                else
                {
                    float timeToNextClip = chosenClipLength - (timePassed + m_chosenPose.Time);

                    if (timeToNextClip < CurrentAnimData.PoseInterval / 2f)
                        --poseIndex;

                    newChosenTime = chosenClipLength; //Clamp
                }

                timePassed = newChosenTime - m_chosenPose.Time;
            }

            int numPosesPassed = 0;

            if (timePassed < Mathf.Epsilon)
            {
                numPosesPassed = Mathf.CeilToInt(timePassed / CurrentAnimData.PoseInterval);
            }
            else
            {
                numPosesPassed = Mathf.FloorToInt(timePassed / CurrentAnimData.PoseInterval);
            }

            m_curChosenPoseId = poseIndex + numPosesPassed; //This could use a bit more refinement to check what the closest current pose is!

            if(m_transitionMethod == ETransitionMethod.Inertialization 
               || m_animationStates[m_dominantBlendChannel].AnimDataId != m_currentAnimDataId)
            {
                MxMUtility.CopyPose(ref CurrentAnimData.Poses[m_curChosenPoseId], ref m_curInterpolatedPose);
                return;
            }

            float dominantClipLength = 0f;
            bool dominantClipLooping = false;

            if (m_dominantPose.AnimType != EMxMAnimtype.Composite)
            {
                AnimationClip dominantClip = CurrentAnimData.Clips[m_dominantPose.PrimaryClipId];
                dominantClipLength = dominantClip.length;
                dominantClipLooping = dominantClip.isLooping;
            }
            else
            {
                ref CompositeData compositeData = ref CurrentAnimData.Composites[m_dominantPose.AnimId];
                dominantClipLength = compositeData.Length;
            }

            if (m_transitionMethod == ETransitionMethod.Blend)
            {
                timePassed = m_animationStates[m_dominantBlendChannel].Age;
            }
            else
            {
                timePassed = m_timeSinceMotionChosen;
            }

            poseIndex = m_dominantPose.PoseId;

            //Determine if the new time is out of bounds of the dominant pose clip
            float newDominantTime = m_dominantPose.Time + timePassed;
            if (newDominantTime >= dominantClipLength)
            {
                if (dominantClipLooping)
                {
                    newDominantTime = MxMUtility.WrapAnimationTime(newDominantTime, dominantClipLength);//Loop
                }
                else
                {
                    float timeToNextClip = dominantClipLength - (timePassed + m_dominantPose.Time);

                    if (timeToNextClip < CurrentAnimData.PoseInterval)
                    {
                        --poseIndex;
                    }

                    newDominantTime = dominantClipLength;//Clamps
                }

                timePassed = newDominantTime - m_dominantPose.Time;
            }


            if (timePassed < -Mathf.Epsilon)
                numPosesPassed = Mathf.CeilToInt(timePassed / CurrentAnimData.PoseInterval);
            else
                numPosesPassed = Mathf.FloorToInt(timePassed / CurrentAnimData.PoseInterval);

            poseIndex = Mathf.Clamp(poseIndex + numPosesPassed, 0, CurrentAnimData.Poses.Length);

            PoseData beforePose = CurrentAnimData.Poses[Mathf.Min(poseIndex, CurrentAnimData.Poses.Length - 2)];
            PoseData afterPose = CurrentAnimData.Poses[beforePose.NextPoseId];

            if (timePassed < Mathf.Epsilon)
            {
                afterPose = CurrentAnimData.Poses[poseIndex];
                beforePose = CurrentAnimData.Poses[Mathf.Clamp(afterPose.LastPoseId, 0, CurrentAnimData.Poses.Length - 1)];

                m_poseInterpolationValue = 1f - Mathf.Abs((timePassed / CurrentAnimData.PoseInterval) - (float)numPosesPassed);
            }
            else
            {
                m_poseInterpolationValue = (timePassed / CurrentAnimData.PoseInterval) - (float)numPosesPassed;
            }

            if (m_poseInterpolationValue >= 0.5f)
            {
                m_curInterpolatedPose.AnimId = afterPose.AnimId;
                m_curInterpolatedPose.AnimType = afterPose.AnimType;
                m_curInterpolatedPose.TracksId = afterPose.TracksId;
                m_curInterpolatedPose.PoseId = afterPose.PoseId;
                m_curInterpolatedPose.PrimaryClipId = afterPose.PrimaryClipId;
                m_curInterpolatedPose.Tags = afterPose.Tags;
                m_curInterpolatedPose.UserTags = afterPose.UserTags;
            }
            else
            {
                m_curInterpolatedPose.AnimId = beforePose.AnimId;
                m_curInterpolatedPose.AnimType = beforePose.AnimType;
                m_curInterpolatedPose.TracksId = beforePose.TracksId;
                m_curInterpolatedPose.PoseId = beforePose.PoseId;
                m_curInterpolatedPose.PrimaryClipId = beforePose.PrimaryClipId;
                m_curInterpolatedPose.Tags = beforePose.Tags;
                m_curInterpolatedPose.UserTags = beforePose.UserTags;
                m_curInterpolatedPose.Tags = afterPose.Tags;
                m_curInterpolatedPose.UserTags = afterPose.UserTags;
            }

            switch (m_tagBlendMethod)
            {
                case ETagBlendMethod.Combine:
                {
                    m_curInterpolatedPose.Tags = beforePose.Tags | afterPose.Tags;
                    m_curInterpolatedPose.UserTags = beforePose.UserTags | afterPose.UserTags;
                } break;
                case ETagBlendMethod.AlwaysFormer:
                {
                    m_curInterpolatedPose.Tags = beforePose.Tags;
                    m_curInterpolatedPose.UserTags = beforePose.UserTags;
                } break;
                case ETagBlendMethod.AlwaysLater:
                {
                    m_curInterpolatedPose.Tags = afterPose.Tags;
                    m_curInterpolatedPose.UserTags = afterPose.UserTags;
                } break;
            }
            

            m_curInterpolatedPose.NextPoseId = afterPose.PoseId;
            m_curInterpolatedPose.LastPoseId = beforePose.PoseId;

            m_curInterpolatedPose.LocalVelocity = Vector3.Lerp(beforePose.LocalVelocity,
                afterPose.LocalVelocity, m_poseInterpolationValue);

            Vector3 pos, vel;
            for (int i = 0; i < beforePose.JointsData.Length; ++i)
            {
                pos = Vector3.Lerp(beforePose.JointsData[i].Position,
                    afterPose.JointsData[i].Position, m_poseInterpolationValue);

                vel = Vector3.Lerp(beforePose.JointsData[i].Velocity,
                    afterPose.JointsData[i].Velocity, m_poseInterpolationValue);

                m_curInterpolatedPose.JointsData[i] = new JointData(pos, vel);
            }

            ref readonly PoseData copyPose = ref afterPose;

            if (m_poseInterpolationValue > 0.5f)
                copyPose = ref afterPose;

            for(int i=0; i < beforePose.Trajectory.Length; ++i)
            {
                TrajectoryPoint.Lerp(ref beforePose.Trajectory[i], ref afterPose.Trajectory[i], 
                    m_poseInterpolationValue, out m_curInterpolatedPose.Trajectory[i]);
            }

            m_curInterpolatedPose.GenericTags = copyPose.GenericTags;
            
//#if UNITY_EDITOR
//            if (m_curInterpolatedPose.AnimId != m_dominantPose.AnimId || m_curInterpolatedPose.AnimType != m_dominantPose.AnimType)
//            {
//                Debug.LogWarning("Clip spill over on Dominant Pose: " + m_dominantPose.AnimId + " to "
//                    + m_curInterpolatedPose.AnimId);
//            }
//#endif
        }

        //============================================================================================
        /**
        *  @brief This function tests if the next pose is close enough in trajectory to the current
        *  desired trajectory. If the desired trajectory is considered 'close enough' the same animation
        *  will keep playing and a motion matching search will be avoided.
        *  
        *  @param [ref PoseData] a_nextPose - reference to the pose data for the next pose
        *         
        *********************************************************************************************/
        private bool NextPoseToleranceTest(ref PoseData a_nextPose)
        {
            //We already know that the next pose data will have good pose transition so we only
            //need to test trajectory (closeness). Additionally there is no need to test past trajectory

            if (a_nextPose.Tags != RequiredTags)
                return false;

            int pointCount = a_nextPose.Trajectory.Length;
            for(int i = 0; i < pointCount; ++i)
            {
                float predictionTime = CurrentAnimData.PosePredictionTimes[i];

                if(predictionTime > 0f)
                {
                    float relativeTolerance_Pos = predictionTime * m_nextPoseToleranceDist; //A tolerance of 0.5m over 1s of movement is acceptable
                    float relativeTolerance_Angle = predictionTime * m_nextPoseToleranceAngle; //A tolerance of 5 degrees over 1s of movement is acceptable

                    float sqrDistance = Vector3.SqrMagnitude(a_nextPose.Trajectory[i].Position - m_desiredGoal[i].Position);

                    if(Mathf.Abs(sqrDistance) > relativeTolerance_Pos * relativeTolerance_Pos)
                    {
                        return false;
                    }

                    float angleDelta = Mathf.DeltaAngle(m_desiredGoal[i].FacingAngle, a_nextPose.Trajectory[i].FacingAngle);

                    if (Mathf.Abs(angleDelta) > relativeTolerance_Angle)
                    {
                        return false;
                    }
                }                
            }
            return true;
        }

        public void TransitionToPose(int a_poseId, float a_speedMod, float a_timeOffset)
        {
            if (a_poseId == m_curChosenPoseId)
            {
                Debug.Log("MxMAnimator: Manual pose change - No need for correction");
                return;
            }

            ref PoseData pose = ref CurrentAnimData.Poses[a_poseId];
            
            /* Based on the time offset, we need to determine if the pose needs to be incremented and the offset adjusted
             taking that into consideration. This could where we want to jump to the authoritative pose but need to take into 
             consideration that some time has passed since that pose was last chosen */
            float poseInterval = CurrentAnimData.PoseInterval;
            if (a_timeOffset >= poseInterval)
            {
                int numPoseIncrement = Mathf.FloorToInt(a_timeOffset / poseInterval);
            
                for (int i = 0; i < numPoseIncrement; ++i)
                {
                    int nextPoseId = pose.NextPoseId;
            
                    if (nextPoseId < 0 || nextPoseId > CurrentAnimData.Poses.Length - 1)
                    {
                        Debug.LogError("Pose time offset is greater than animation length.");
                        break;
                    }
            
                    pose = CurrentAnimData.Poses[pose.NextPoseId];
                    a_timeOffset -= poseInterval;
                }
            }

            //This may ore may not be desireable
            m_timeSinceMotionChosen = m_timeSinceMotionUpdate = 0f;
            
            TransitionToPose(ref pose, a_speedMod, a_timeOffset);
        }

        public void TransitionToPose(PoseChangeData a_poseChangeData, float a_latency = 0f)
        {
            if (a_poseChangeData.PoseId == m_curChosenPoseId)
            {
                Debug.Log("MxMAnimator: Manual pose change - No need for correction");
                return;
            }

            ref PoseData pose = ref CurrentAnimData.Poses[a_poseChangeData.PoseId];
            
            /* Based on the time offset, we need to determine if the pose needs to be incremented and the offset adjusted
             taking that into consideration. This could where we want to jump to the authoritative pose but need to take into 
             consideration that some time has passed since that pose was last chosen */
            float poseInterval = CurrentAnimData.PoseInterval;
            float timeOffset = a_poseChangeData.TimeOffset + a_latency;
            if (timeOffset >= poseInterval)
            {
                int numPoseIncrement = Mathf.FloorToInt(timeOffset / poseInterval);
            
                for (int i = 0; i < numPoseIncrement; ++i)
                {
                    int nextPoseId = pose.NextPoseId;
            
                    if (nextPoseId < 0 || nextPoseId > CurrentAnimData.Poses.Length - 1)
                    {
                        Debug.LogError("Pose time offset is greater than animation length.");
                        break;
                    }
            
                    pose = CurrentAnimData.Poses[pose.NextPoseId];
                    timeOffset -= poseInterval;
                }
            }

            //Todo: This may ore may not be desireable
            m_timeSinceMotionChosen = m_timeSinceMotionUpdate = 0f;
            
            TransitionToPose(ref pose, a_poseChangeData.SpeedMod, timeOffset);
        }

        //============================================================================================
        /**
        *  @brief When the motion matching algorithm determines a transition to a new pose (animation)
        *  it calls this function, passing the PoseData. This function is responsible for transitioning
        *  to that pose regardless of the method specified in the MxMAnimator inspector.
        *  
        *  @param [ref PoseData] a_pose - the pose to transition to
        *         
        *********************************************************************************************/
        public void TransitionToPose(ref PoseData a_pose, float a_speedMod = 1f, float a_timeOffset = 0f)
        {
            if (a_pose.PoseId < 0 || a_pose.PoseId >= CurrentAnimData.Poses.Length)
                return;
            
            if (m_onPoseChanged != null)
                m_onPoseChanged.Invoke(new PoseChangeData(a_pose.PoseId, a_speedMod, a_timeOffset));

            switch(m_transitionMethod)
            {
                case ETransitionMethod.None: { JumpToPose(ref a_pose, a_speedMod, a_timeOffset); } break;
                case ETransitionMethod.Blend: { BlendToPose(ref a_pose, a_speedMod, a_timeOffset); } break;
                case ETransitionMethod.Inertialization:
                {
                    
                    SetupPose(ref a_pose, a_speedMod);
                    m_dominantPose = m_chosenPose;
                    
                    //Todo: Add Inertialization here
                    //m_inertialBlendModule.BeginTransition(m_matchBlendTime);
                }
                break;
            }
        }

        //============================================================================================
        /**
        *  @brief Jumps to a pose without any blending. Only used when BlendMethod is set to 'None'
        *  
        *  @param [ref PoseData] a_pose - the pose to jump to.
        *  @param [float] a_speedMod - the speed at which the new animation should playback.
        *         
        *********************************************************************************************/
        public void JumpToPose(ref PoseData a_pose, float a_speedMod = 1f, float a_timeOffset = 0f)
        {
            int clipId = a_pose.PrimaryClipId;

            if (clipId >= CurrentAnimData.Clips.Length || clipId < 0)
                return;

            for (int i = 0; i < m_animationStates.Length; ++i)
            {
                ref MxMPlayableState curPlayableState = ref m_animationStates[i];
                if (curPlayableState.BlendStatus == EBlendStatus.None)
                    continue;

                curPlayableState.Weight = 0f;
                curPlayableState.HighestWeight = 0f;
                curPlayableState.StartPoseId = -1;
                curPlayableState.Age = 0f;
                curPlayableState.DecayAge = 0f;
                curPlayableState.BlendStatus = EBlendStatus.None;

                m_animationMixer.SetInputWeight(i, 0f);
            }

            //We need to delete any playable in the top most playable state to make way for the 
            //new pose we are about to jump to.
            ref MxMPlayableState topPlayableState = ref m_animationStates[0];
            if (topPlayableState.TargetPlayable.IsValid())
            {
                m_animationMixer.DisconnectInput(0);
                PlayableUtils.DestroyPlayableRecursive(ref topPlayableState.TargetPlayable);
            }

            //TODO: This only works with clips and not blend spaces or anything else
            AnimationClip clip = CurrentAnimData.Clips[clipId];

            ref MxMPlayableState playableState = ref m_animationStates[0];
            var clipPlayable = AnimationClipPlayable.Create(MxMPlayableGraph, clip);
            clipPlayable.SetApplyFootIK(m_applyHumanoidFootIK);
            clipPlayable.SetApplyPlayableIK(m_applyPlayableIK);

            playableState.TargetPlayable = clipPlayable;

            playableState.AnimDataId = m_currentAnimDataId;

            MxMPlayableGraph.Connect(playableState.TargetPlayable, 0, m_animationMixer, 0);

            playableState.TimeX2 = a_pose.Time + m_timeSinceMotionChosen + a_timeOffset;
            playableState.Speed = m_playbackSpeed * a_speedMod;
            m_animationMixer.SetInputWeight(0, 1f);

            playableState.Weight = playableState.HighestWeight = 1f;
            playableState.AnimType = a_pose.AnimType;
            playableState.StartPoseId = a_pose.PoseId;
            playableState.Age = m_matchBlendTime;
            playableState.DecayAge = 0f;
            playableState.BlendStatus = EBlendStatus.Dominant;

            m_dominantBlendChannel = m_primaryBlendChannel = 0;
            m_dominantPose = a_pose;
        }

        //============================================================================================
        /**
        *  @brief Blends the animation toward a new pose on the Motion Matching mixer. This sets a 
        *  new chosen animation slot and marks all existing animations for decay.
        *  
        *  @param [ref PoseData] a_pose - the pose to blend to.
        *  @param [float] a_speedMod - the speed at which the new animation should playback.
        *         
        *********************************************************************************************/
        public void BlendToPose(ref PoseData a_pose, float a_speedMod = 1f, float a_timeOffset = 0f)
        {
            ref MxMPlayableState primaryState = ref m_animationStates[m_primaryBlendChannel];
            primaryState.DecayAge = 0.0f;
            primaryState.AnimDataId = m_currentAnimDataId;

            if (primaryState.BlendStatus != EBlendStatus.Dominant)
                primaryState.BlendStatus = EBlendStatus.Decaying;

            int channelToUse = -1;
            float minBlend = 1.1f;
            int minBlendChannelId = 0;
            for (int i = 0; i < m_animationStates.Length; ++i)
            {
                ref MxMPlayableState curPlayableState = ref m_animationStates[i];

                if (curPlayableState.BlendStatus == EBlendStatus.None)
                {
                    channelToUse = i;
                    break;
                }
                else
                {
                    if(curPlayableState.Weight < minBlend)
                    {
                        minBlend = curPlayableState.Weight;
                        minBlendChannelId = i;
                    }
                }
            }

            if (channelToUse < 0)
                channelToUse = minBlendChannelId;           

            SetupPoseInSlot(ref a_pose, channelToUse, a_speedMod, a_timeOffset);

            m_primaryBlendChannel = channelToUse;
        }

        //============================================================================================
        /**
        *  @brief Checks to see if Trajectory Error Angular Warping can occur this frame.
        *  
        *  return bool - true if trajectory error angular warping can take place, otherwise false.
        *         
        *********************************************************************************************/
        public bool CanPerformTrajectoryErrorAngularWarping()
        {
            if (m_fsm.CurrentStateId != (int)EMxMStates.Matching)
                return false;

            if ((m_curInterpolatedPose.GenericTags & EGenericTags.DisableWarp_TrajLat) == EGenericTags.DisableWarp_TrajLat)
                return false;

            if (p_animator.velocity.sqrMagnitude < (m_angularErrorWarpThreshold * m_angularErrorWarpThreshold))
                return false;

            return true;
        }

        //============================================================================================
        /**
        *  @brief This function updates and applies 'Trajectory Error Angular Warping' to the character
        *  to compensate for trajectory errors. 
        *  
        *  Because motion matching is limited to the animations it has available, not all possible angles
        *  of motion are possible. Therefore, trajectory error angular warping is used to procedurally
        *  rotate the character over time to make up for that error. This function calculates that warping.
        *  
        *  Character motion is not always controlled directly through a transform and sometimes character
        *  controllers take greedy control over a gameObject's transform. Therefore, this function 
        *  returns a quaternion rotations which will be applied to root motion calculations on the 
        *  Animatormove update. See OnAnimatorMove() for more details.
        *  
        *  @return Quaternion - the calculated angular warp required this frame.
        *         
        *********************************************************************************************/
        public Quaternion ApplyTrajectoryErrorAngularWarping(Quaternion a_deltaRot, float a_deltaTime)
        {
            Quaternion errorWarpRot = Quaternion.identity;

            int index = m_desiredGoal.Length - 1;

            Vector3 goalVector = m_desiredGoal[index].Position;
            Vector3 destVector =  Vector3.forward; 

            switch (m_angularWarpMethod)
            {
                case EAngularErrorWarpMethod.TrajectoryHeading:
                    {
                        destVector = m_curInterpolatedPose.Trajectory[index].Position;
                    }
                    break;
                case EAngularErrorWarpMethod.TrajectoryFacing:
                    {
                        float facingAngle = m_desiredGoal[index].FacingAngle * Mathf.Deg2Rad;
                        goalVector = new Vector3(Mathf.Sin(facingAngle), 0f, Mathf.Cos(facingAngle));
                    }
                    break;
            }

            float deltaRotY = a_deltaRot.eulerAngles.y;
            if (deltaRotY > 180f)
                deltaRotY -= 360f;

            LatErrorWarpAngle = Vector3.SignedAngle(destVector, goalVector, Vector3.up) + deltaRotY;
            float absAngle = Mathf.Abs(LatErrorWarpAngle);

            switch (m_angularWarpType)
            {
    
                case EAngularErrorWarp.On:
                case EAngularErrorWarp.External:
                    {
                        if (absAngle < m_angularErrorWarpAngleThreshold && absAngle > m_angularErrorWarpMinAngleThreshold)
                        {
                            if (LatErrorWarpAngle < -Mathf.Epsilon)
                            {
                                errorWarpRot = Quaternion.AngleAxis(Mathf.Max(-m_angularErrorWarpRate *
                                    a_deltaTime * m_playbackSpeed, LatErrorWarpAngle), Vector3.up);
                            }
                            else if (LatErrorWarpAngle > Mathf.Epsilon)
                            {
                                errorWarpRot = Quaternion.AngleAxis(Mathf.Min(m_angularErrorWarpRate *
                                    a_deltaTime * m_playbackSpeed, LatErrorWarpAngle), Vector3.up);
                            }
                        }
                    }
                    break;
            }

            return errorWarpRot;
        }

        //============================================================================================
        /**
        *  @brief Sometimes a trajectory can be longer or shorter than the animations available. 
        *  Input Profiles are used to shape the input so that this never occurs. However, this results
        *  in a clear digital feel to characters (i.e. walk and run with nothing in between).
        *  
        *  Trajectory Error Longitudinal Warping can be used to warp the speed of the animation,
        *  within specified limits, to make the movement feel more analog in nature.
        *         
        *********************************************************************************************/
        private void ApplyTrajectoryErrorLongWarping()
        {
            switch (m_longErrorWarpType)
            {
                case ELongitudinalErrorWarp.Speed:
                    {
                        DesiredPlaybackSpeed = Mathf.Clamp(LongErrorWarpScale, m_speedWarpLimits.x, m_speedWarpLimits.y);
                    }
                    break;
                case ELongitudinalErrorWarp.Stride:
                    {
                        if (m_longErrorWarper == null)
                            return;

                        m_longErrorWarper.ApplySpeedScale(LongErrorWarpScale);
                    }
                    break;
            }
        }

        //============================================================================================
        /**
        *  @brief Resets the entire playable graph to it's default state without layers and with
        *  the starting animation.
        *         
        *********************************************************************************************/
        public void ResetPlayableGraph()
        {
            if (!m_animationLayerMixer.IsNull())
            {
                m_animationLayerMixer.SetInputWeight(0, 1f);
                m_animationLayerMixer.SetInputWeight(m_animControllerLayer, 0f);

                int inputCount = m_animationLayerMixer.GetInputCount();
                for (int i = 1; i < inputCount; ++i)
                {
                    if (i == m_animControllerLayer)
                        continue;

                    Playable playable = m_animationLayerMixer.GetInput(i);

                    if (!playable.IsNull())
                    {
                        m_animationLayerMixer.DisconnectInput(i);
                        playable.Destroy();
                    }
                }

                m_animationLayerMixer.SetInputCount(Mathf.Max(3, m_animControllerLayer + 1));
            }
        }

        //============================================================================================
        /**
        *  @brief Resets the motion (trjectory) of the IMxMTrajectory registered with this MxM Animator
        *         
        *********************************************************************************************/
        public void ResetMotion(bool a_applyToAll=false)
        {
            if (a_applyToAll)
            {
                foreach (IMxMTrajectory trajGen in m_trajectoryGenerators)
                {
                    trajGen.ResetMotion();
                }
            }
            else
            {
                p_trajectoryGenerator.ResetMotion();
            }
        }

        //============================================================================================
        /**
        *  @brief Resets the character pose to the default starting pose of the MxMAnimator.
        *         
        *********************************************************************************************/
        public void ResetPose()
        {
            JumpToPose(ref CurrentAnimData.Poses[CurrentAnimData.StartPoseId]);
        }

        //============================================================================================
        /**
        *  @brief Detects if there is motion based on the desired goal provided by the IMxMTrajectory
        *  
        *  @param [float] a_minMotion - the minimium motion in the desired goal for motion to be detected
        *  @param [float] a_minAngle - the minimum angular motion in the desired goal for motion to be detected
        *         
        *********************************************************************************************/
        public bool DetectMotion(float a_minMotion = 0.25f, float a_minAngle = 10f)
        {
            float futureMotion = 0f;
            for (int i = m_startFutureTrajIndex; i < m_desiredGoalBase.Length; ++i)
            {
                futureMotion += m_desiredGoalBase[i].Position.magnitude;
            }

            futureMotion /= (m_desiredGoalBase.Length - m_startFutureTrajIndex);

            float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(m_animationRoot.rotation.y, m_desiredGoalBase[m_desiredGoalBase.Length - 1].FacingAngle));
            if (futureMotion > a_minMotion || deltaAngle > a_minAngle)
            {
                return true;
            }
            return false;
        }

        //============================================================================================
        /**
        *  @brief Detects if there is movements based on a minimum motion passed parameter
        *  
        *  @param [float] a_minMotion - the minimum motion required to 'detect' motion
        *  
        *  @return bool - true if motion detected otherwise false
        *         
        *********************************************************************************************/
        public bool DetectMovement(float a_minMotion = 0.25f)
        {
            float futureMotion = 0f;

            for (int i = m_startFutureTrajIndex; i < m_desiredGoalBase.Length; ++i)
            {
                futureMotion += m_desiredGoalBase[i].Position.magnitude;
            }

            futureMotion /= (m_desiredGoalBase.Length - m_startFutureTrajIndex);

            if (futureMotion > a_minMotion)
            {
                return true;
            }

            return false;
        }

        //============================================================================================
        /**
        *  @brief Detects if there is angular movement based on a minimum angle passed parameter
        *  
        *  @param [float] a_minAngle - the minimum anglular motion required to 'detect' angular motion
        *  
        *  @return bool - true if angular motion detected otherwise false.
        *         
        *********************************************************************************************/
        public bool DetectAngularMovement(float a_minAngle = 10f)
        {
            float futureAngle = m_desiredGoalBase[m_desiredGoalBase.Length - 1].FacingAngle;

            float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(futureAngle, 0f));

            if (deltaAngle > a_minAngle)
                return true;

            return false;
        }

        //============================================================================================
        /**
        *  @brief Searches through all attached trajectory generators and finds the first one that is
        *  enabled, setting it as the primary trajectory generator
        *         
        *********************************************************************************************/
        private void FetchTrajectoryGenerator()
        {
            foreach (IMxMTrajectory trajGen in m_trajectoryGenerators)
            {
                if (trajGen.IsEnabled())
                {
                    p_trajectoryGenerator = trajGen;
                    break;
                }
            }
        }

        //============================================================================================
        /**
        *  @brief Sets the current trajectory generator to use. This should only be called from a
        *  trajectory generator, preferably in it's 'OnEnable' function. it is up to the user to manage
        *  enabling and disabling trajectory generators.
        *         
        *********************************************************************************************/
        public void SetCurrentTrajectoryGenerator(IMxMTrajectory a_trajectoryGenerator)
        {
            if ((a_trajectoryGenerator == null) || (a_trajectoryGenerator == p_trajectoryGenerator))
                return;

            p_trajectoryGenerator = a_trajectoryGenerator;
        }

        //============================================================================================
        /**
        *  @brief Fetches the correct NativeAnimData package from the current MxMAnimData dependent on
        *  current tags.
        *         
        *********************************************************************************************/
        private void FetchNativeAnimData()
        {
            if (CurrentAnimData.NativeAnimData.TryGetValue(RequiredTags, out var nativeAnimData))
            {
                CurrentNativeAnimData = nativeAnimData;
            }
            else
            {
                BeginIdle();
#if UNITY_EDITOR
                Debug.Log("MxMAnimator - No native anim data to match require tags: " + RequiredTags.ToString() 
                    + " ~ Reverting to idle state");
#endif
            }
        }

        //============================================================================================
        /**
        *  @brief Sets the current calibration data to use via an integer ID
        *  
        *  @param [int] a_calibDataId - the Id of the calibration data to use
        *         
        *********************************************************************************************/
        public void SetCalibrationData(int a_calibDataId)
        {
            if (a_calibDataId < 0)
            {
                Debug.LogWarning("Trying to Set calibration data but the passed calibration id is not valid (negative)");
                return;
            }

            if (m_calibrationOverride != null && m_calibrationOverride.IsCompatibleWith(CurrentAnimData))
            {
                CalibrationData calibData = m_calibrationOverride.GetCalibrationSet(a_calibDataId);
                if (calibData != null)
                {
                    m_curCalibData = calibData;
                }
            }
            else
            {
                if (a_calibDataId < CurrentAnimData.CalibrationSets.Length)
                {
                    m_curCalibData = CurrentAnimData.CalibrationSets[a_calibDataId];
                }
            }
        }

        //============================================================================================
        /**
        *  @brief Sets the current calibration data to use via a string 
        *  
        *  Note: This method is the slowest and will cause garbage allocation if the string is not
        *  cached before using.
        *  
        *  @param [string] a_calibDataName - the name of the calibration data to use. 
        *         
        *********************************************************************************************/
        public void SetCalibrationData(string a_calibDataName)
        {
            if (m_calibrationOverride != null && m_calibrationOverride.IsCompatibleWith(CurrentAnimData))
            {
                CalibrationData calibData = m_calibrationOverride.GetCalibrationSet(a_calibDataName);

                if (calibData != null)
                {
                    m_curCalibData = calibData;
                }
            }
            else
            {
                foreach (CalibrationData calibData in CurrentAnimData.CalibrationSets)
                {
                    if (calibData.CalibrationName == a_calibDataName)
                    {
                        m_curCalibData = calibData;
                        break;
                    }
                }
            }
        }

        //============================================================================================
        /**
        *  @brief Sets the playback speed of the motion matching system
        *  
        *  @param [float] a_speed - the desired playback speed
        *  @param [bool] a_smooth - Should the change in playback speed be smoothed over time? (default - false)
        *         
        *********************************************************************************************/
        public void SetPlaybackSpeed(float a_speed, bool a_smooth = false)
        {
            if(a_smooth)
            {
                DesiredPlaybackSpeed = Mathf.Clamp(a_speed, 0f, float.MaxValue);
            }
            else
            {
                PlaybackSpeed = DesiredPlaybackSpeed = Mathf.Clamp(a_speed, 0f, float.MaxValue);
            }
        }

        //============================================================================================
        /**
        *  @brief Sets all parameters relating to angular error warping.
        *  
        *  Note: These parameters are the same as those set in the MxMAnimator inspector but can be
        *  used at runtime.
        *  
        *  @param [EAngularErrorWarp] a_type - the type of angular error warping to use.
        *  @param [EAngularErrorWarpMethod] a_method - the method to warp with.
        *  @param [float] a_compensationRate - the rate (in degrees per second) that the warp should be
        *  @param [float] a_distThreshold - the minimum trajetory length for the warping to be active
        *  @param [float] a_angleThreshold - the angle discrepancy underwhich it must be for warping to be active
        *         
        *********************************************************************************************/
        public void SetAngularErrorWarping(EAngularErrorWarp a_type, EAngularErrorWarpMethod a_method = EAngularErrorWarpMethod.CurrentHeading,
            float a_compensationRate = 45f, float a_distThreshold = 0.5f, float a_angleThreshold = 45f, float a_minAngleThreshold = 0.05f)
        {
            m_angularWarpType = a_type;

            if (m_angularWarpType > EAngularErrorWarp.Off)
            {
                m_angularWarpMethod = a_method;
                m_angularErrorWarpRate = Mathf.Clamp(a_compensationRate, 0f, float.MaxValue);
                m_angularErrorWarpThreshold = Mathf.Clamp(a_distThreshold, 0f, float.MaxValue);
                m_angularErrorWarpAngleThreshold = Mathf.Clamp(a_angleThreshold, 0f, float.MaxValue);
                m_angularErrorWarpMinAngleThreshold = Mathf.Clamp(a_minAngleThreshold, 0f, float.MaxValue);
            }
        }

        //============================================================================================
        /**
        *  @brief Sets all parameters relating to longitudinal error warping.
        *  
        *  @param [ELongitudinalErrorWarp] a_type - the type of longitudinal error warping
        *  @param [Vector2] a_speedWarpLimits - the minimum (x) and maximum (y) playback speed allowable by warping
        *         
        *********************************************************************************************/
        public void SetLongitudinalErrorWarping(ELongitudinalErrorWarp a_type, Vector2 a_speedWarpLimits)
        {
            m_longErrorWarpType = a_type;
            m_speedWarpLimits = a_speedWarpLimits;
        }

        //============================================================================================
        /**
        *  @brief Sets all parameters relating to Pose favouring
        *  
        *  Note: The favour is a multiplier to the cost of the pose. Therefore, smaller numbers < 1f
        *  make the pose more favourable while numbers > 1f make it less favourable.
        *  
        *  @param [bool] a_favour - should the current pose be favoured?
        *  @param [float] a_favourMultiplier - the favour multiplier for the current pose
        *         
        *********************************************************************************************/
        public void SetFavourCurrentPose(bool a_favour, float a_favourMultiplier)
        {
            m_favourCurrentPose = a_favour;

            if (m_favourCurrentPose)
            {
                m_favourMultiplier = a_favourMultiplier;
            }
        }

        //============================================================================================
        /**
        *  @brief Call this function to swap to a different anim data that has been slotted into the
        *  MxMAnimator. 
        *  
        *  Note: The purpose of swapping AnimData is to have different pose and trajectory configurations. 
        *  Each AnimData must have a fixed configuration. Therefore, to switch to a different movement
        *  type, like climbing, you may make a separate anim data with a different configuration where
        *  the hands are matched instead of the feet. This would require an animData swap.
        *  
        *  Note: AnimData swaps are only ever manual. The MxMAnimator will never automatically swap 
        *  anim data.
        *  
        *  @param [int] a_aimDataId - the Id of the animData to use (id is based on the order in the MxMAnimator inspector
        *  
        *  @param [int] a_startPoseId - the id of the pose in the new anim data to start at. (-1 will start at the default pose)
        *         
        *********************************************************************************************/
        public void SwapAnimData(int a_animDataId, int a_startPoseId = -1)
        {
            if (a_animDataId >= 0 && a_animDataId < m_animData.Length)
            {
                StopJobs();

                MxMAnimData animData = m_animData[a_animDataId];

                if (CurrentAnimData == animData)
                    return;

                if (animData == null)
                {
                    Debug.LogError("Attempting to switch to null animation data. Aborted operation");
                    return;
                }


                CancelAnimDataSwapQueue();

                CurrentAnimData = animData;
                m_currentAnimDataId = a_animDataId;

                for (int i = 0; i < CurrentAnimData.PosePredictionTimes.Length; ++i)
                {
                    if (CurrentAnimData.PosePredictionTimes[i] > 0)
                    {
                        m_startFutureTrajIndex = i;
                        break;
                    }
                }

                if (a_startPoseId < 0)
                    a_startPoseId = CurrentAnimData.StartPoseId;

                a_startPoseId = Mathf.Clamp(a_startPoseId, 0, CurrentAnimData.Poses.Length - 1);

                foreach(IMxMTrajectory trajGen in m_trajectoryGenerators)
                {
                    trajGen.SetGoalRequirements(CurrentAnimData.PosePredictionTimes);
                }

                m_desiredGoalBase = new TrajectoryPoint[CurrentAnimData.PosePredictionTimes.Length];
                m_desiredGoal = new TrajectoryPoint[CurrentAnimData.PosePredictionTimes.Length];
                m_curInterpolatedPose = new PoseData(CurrentAnimData.Poses[a_startPoseId]);
                m_chosenPose = CurrentAnimData.Poses[a_startPoseId];
                m_dominantPose = m_chosenPose;
                m_lastIdleGlobalClipId = m_dominantPose.PrimaryClipId;

                m_timeSinceMotionChosen = 0f;
                m_timeSinceMotionUpdate = CurrentAnimData.PoseInterval;

                SetupJobDelegates();
                FetchNativeAnimData();

                if (m_calibrationOverride != null && m_calibrationOverride.IsCompatibleWith(CurrentAnimData))
                {
                    m_curCalibData = m_calibrationOverride.GetCalibrationSet(0);
                }
                else
                {
                    m_curCalibData = CurrentAnimData.CalibrationSets[0];
                }

                TransitionToPose(ref CurrentAnimData.Poses[a_startPoseId]);
#if UNITY_EDITOR
                DebugData.ResetUsedPoseData();
#endif
            }
        }

        //============================================================================================
        /**
        *  @brief This function queues an anim data swap for the next frame that is in the motion 
        *  matching state. This is generally used to switch anim data following an event. For example,
        *  an event may be used to mount onto a wall from normal locomotion to climbing. First the 
        *  event should be triggered and then immediately an AnimData swap should be queued using
        *  this function to transition to the climbing anim data set immediately after the event
        *  is complete.
        *  
        *  @param [int] a_animDataId - the Id of the anim data to swap to.
        *  @param [int] a_startPoseId - the id of the pose in the new anim data to start at. (-1 will start at the default pose)
        *         
        *********************************************************************************************/
        public void QueueAnimDataSwap(int a_animDataId, int a_startPoseId = -1)
        {
            if (m_fsm.CurrentStateId != (uint)EMxMStates.Event)
            {
                SwapAnimData(a_animDataId, a_startPoseId);
            }

            if (a_animDataId >= 0 && a_animDataId < m_animData.Length)
            {
                MxMAnimData animData = m_animData[a_animDataId];

                if (animData != null)
                {
                    m_queueAnimDataSwapId = a_animDataId;
                    m_queueAnimDataStartPoseId = a_startPoseId;
                }
            }
        }

        //============================================================================================
        /**
        *  @brief Cancels any queued anim data swap.
        *         
        *********************************************************************************************/
        public void CancelAnimDataSwapQueue()
        {
            m_queueAnimDataSwapId = -1;
        }

        //============================================================================================
        /**
        *  @brief Toggles pause state on the MxM Animator
        *         
        *********************************************************************************************/
        public void TogglePause()
        {
            if (IsPaused)
            {
                UnPause();
            }
            else
            {
                Pause();
            }
        }

        //============================================================================================
        /**
        *  @brief Pauses the MxMAnimator and related IMxMTrajectory component
        *         
        *********************************************************************************************/
        public void Pause()
        {
            if (!IsPaused)
            {
                p_trajectoryGenerator.Pause();
                IsPaused = true;
                m_animationMixer.Pause();

                //Stomp root motion on MxM layer
                for (int i = 0; i < m_animationStates.Length; ++i)
                {
                    ref MxMPlayableState state = ref m_animationStates[i];

                    if (state.BlendStatus == EBlendStatus.None)
                        continue;

                    state.TimeX2 = state.Time;
                }
            }
        }

        //============================================================================================
        /**
        *  @brief returns the dominant animation clip in the motion matching blend
        *         
        *********************************************************************************************/
        public AnimationClip GetDominantClip()
        {
            if (m_dominantPose.PrimaryClipId < 0 || m_dominantPose.PrimaryClipId >= CurrentAnimData.Clips.Length)
            {
                return null;
            }
            
            return CurrentAnimData.Clips[m_dominantPose.PrimaryClipId];
        }

        //============================================================================================
        /**
        *  @brief returns the currently chosen clip in the motion matching blend
        *         
        *********************************************************************************************/
        public AnimationClip GetChosenClip()
        {
            if (m_chosenPose.PrimaryClipId < 0 || m_chosenPose.PrimaryClipId >= CurrentAnimData.Clips.Length)
            {
                return null;
            }

            return CurrentAnimData.Clips[m_chosenPose.PrimaryClipId];
        }

        //============================================================================================
        /**
        *  @brief Un-Pauses the MxM Animator and related IMxMTrajectory component
        *         
        *********************************************************************************************/
        public void UnPause()
        {
            if (IsPaused)
            {
                p_trajectoryGenerator.UnPause();
                IsPaused = false;
                m_animationMixer.Play();
#if UNITY_EDITOR
                if (m_debugPreview)
                {
                    m_debugPreview = false;
                }
#endif
            }
        }

        public void SetWarpOverride(WarpModule a_warpModule)
        {
            m_overrideWarpSettings = a_warpModule;

            if(m_overrideWarpSettings != null)
            {
                m_angularWarpType = m_overrideWarpSettings.AngularErrorWarpType;
                m_angularWarpMethod = m_overrideWarpSettings.AngularErrorWarpMethod;
                m_angularErrorWarpRate = m_overrideWarpSettings.WarpRate;
                m_angularErrorWarpThreshold = m_overrideWarpSettings.DistanceThreshold;
                m_angularErrorWarpMinAngleThreshold = m_overrideWarpSettings.AngleRange.x;
                m_angularErrorWarpAngleThreshold = m_overrideWarpSettings.AngleRange.y;
                m_longErrorWarpType = m_overrideWarpSettings.LongErrorWarpType;
                m_speedWarpLimits = m_overrideWarpSettings.LongWarpSpeedRange;
            }
        }

#if UNITY_EDITOR
        //============================================================================================
        /**
        *  @brief Stops warnings for editor only serialized variables used by the MxMInspector
        *         
        *********************************************************************************************/
        private void StopEditorWarnings()
        {
            if (m_generalFoldout)
                m_generalFoldout = true;

            if (m_animDataFoldout)
                m_animDataFoldout = true;

            if (m_optionsFoldout)
                m_optionsFoldout = true;

            if (m_warpingFoldout)
                m_warpingFoldout = true;
            
            if (m_optimisationFoldout)
                m_optimisationFoldout = true;

            if (m_debugFoldout)
                m_debugFoldout = true;

            if (m_callbackFoldout)
                m_callbackFoldout = false;
        }
#endif

    }//End of class: MxMAnimator
}//End of namespace: MxM