// Copyright © 2017-2024 Vault Break Studios Pty Ltd

using UnityEngine;

namespace MxM
{
    //============================================================================================
    /**
    *  @brief Contains the states portion of the MxMAnimator partial class
    *         
    *********************************************************************************************/
    public partial class MxMAnimator : MonoBehaviour
    {
        //============================================================================================
        /**
        *  @brief State for handling the standard matching policy for MxM
        *         
        *********************************************************************************************/
        private class StateMatching : FsmState
        {
            private MxMAnimator m_animator;

            public StateMatching(MxMAnimator a_animator)
            {
                m_animator = a_animator;
            }

            public override void DoEnter()
            {
                m_animator.m_timeSinceMotionUpdate = m_animator.m_updateInterval + 0.0001f;
            }

            public override void Update_Phase1()
            {
                m_animator.UpdateMatching_Phase1();
            }

            public override void Update_Phase2()
            {
                m_animator.UpdateMatching_Phase2();
            }

            public override void DoExit()
            {
                //A pose search scheduled in this state is only finalized while in this state. Drop it here so a
                //search can't outlive the state: left pending, MxMLateUpdate finalized it on the next return to
                //Matching, against whatever pose list the required tags had swapped to in the meantime.
                m_animator.StopJobs();
            }

        }//End of class: StateMatching

        //============================================================================================
        /**
        *  @brief State for handling the event matching policy for MxM
        *         
        *********************************************************************************************/
        private class StateEvent : FsmState
        {
            private MxMAnimator m_animator;

            public StateEvent(MxMAnimator _animator)
            {
                m_animator = _animator;
            }

            public override void DoEnter()
            {
                m_animator.LatErrorWarpAngle = 0f;
                m_animator.LongErrorWarpScale = 1f;
               // m_animator.m_playbackSpeed = 1f;

                if (m_animator.PostEventTrajectoryMode == EPostEventTrajectoryMode.Pause)
                    m_animator.p_trajectoryGenerator.Pause();

                m_animator.m_timeSinceMotionUpdate = m_animator.m_updateInterval;
            }

            public override void Update_Phase1() { }

            public override void Update_Phase2()
            {
                m_animator.UpdateEvent();
                m_animator.UpdatePlaybackSpeed();
            }

            public override void DoExit()
            {
                m_animator.m_eventType = EMxMEventType.Standard;
                m_animator.WarpType = EEventWarpType.None;
                m_animator.RotWarpType = EEventWarpType.None;
                m_animator.TimeWarpType = EEventWarpType.None;

               // m_animator.DesiredPlaybackSpeed = 1f;

                if (m_animator.m_queueAnimDataSwapId >= 0)
                {
                    m_animator.SwapAnimData(m_animator.m_queueAnimDataSwapId, m_animator.m_queueAnimDataStartPoseId);
                    m_animator.m_queueAnimDataSwapId = -1;
                }
            }

        }//End of class: StateEvent

        //============================================================================================
        /**
        *  @brief State for handling the event matching policy for MxM
        *         
        *********************************************************************************************/
        private class StateIdle : FsmState
        {
            private MxMAnimator m_animator;

            public StateIdle(MxMAnimator _animator)
            {
                m_animator = _animator;
            }

            public override void DoEnter()
            {
                m_animator.LatErrorWarpAngle = 0f;
                m_animator.LongErrorWarpScale = 1f;

                m_animator.m_timeSinceMotionUpdate = m_animator.m_updateInterval;
            }

            public override void Update_Phase1() { }

            public override void Update_Phase2()
            {
                m_animator.UpdateIdle();
            }

            public override void DoExit()
            {
                m_animator.m_timeSinceMotionChosen = m_animator.m_updateInterval + 0.00001f;
                m_animator.OnIdleEnd.Invoke();
            }
        }

        //============================================================================================
        /**
        *  @brief State for handling the event matching policy for MxM
        *         
        *********************************************************************************************/
        private class StateLoopBlend : FsmState
        {
            private MxMAnimator m_animator;

            public StateLoopBlend(MxMAnimator a_animator)
            {
                m_animator = a_animator;
            }

            public override void DoEnter()
            {
                m_animator.LatErrorWarpAngle = 0f;
                m_animator.m_timeSinceMotionUpdate = m_animator.m_updateInterval;
            }

            public override void Update_Phase1() { }

            public override void Update_Phase2()
            {
                m_animator.UpdateLoopBlend();
            }

            public override void DoExit() { }

        }//End of class: StateLoopBlend

    }//End of partial class: MxMAnimator
}//End of namespace: MxM