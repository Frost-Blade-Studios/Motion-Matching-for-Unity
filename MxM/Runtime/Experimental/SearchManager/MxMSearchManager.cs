// Copyright © 2017-2024 Vault Break Studios Pty Ltd

using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Serialization;


namespace MxM
{
    public class MxMSearchManager : MonoBehaviour
    {
        [FormerlySerializedAs("m_maxUpdatesPerFrame")] [SerializeField] private int m_maxSearchesPerFrame = 1;
        [SerializeField] private float m_maxAllowableDelay = 1f;
        [SerializeField] private int m_expectedAnimatorCount = 3;
        [SerializeField] private int m_expectedPhysicsAnimatorCount = 0;

        private static MxMSearchManager m_instance = null;
        public static MxMSearchManager Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = FindObjectOfType<MxMSearchManager>();

                    if (m_instance == null)
                    {
                        GameObject singletonObject = new GameObject("MxMSearchManager");
                        m_instance = singletonObject.AddComponent<MxMSearchManager>();
                        
                        Debug.Log("Could not find instance of MxMSearchManager in scene, instance created programmatically.");
                    }

                    if (m_instance)
                    {
                        m_instance.Initialize();
                    }
                }

                return m_instance;
            }
            
            private set { m_instance = value;  }
        }

        public static bool DoesInstanceExist
        {
            get { return m_instance; }
        }

        private List<MxMAnimator> m_mxmAnimators;
        private List<MxMAnimator> m_fixedUpdateMxMAnimators;

        private int m_searchesThisFrame = 0;
        private int m_animatorIndex = 0;
        private int m_fixedAnimatorIndex = 0;

        void IncrementAnimatorIndex()
        {
            ++m_animatorIndex;
            if (m_animatorIndex >= m_mxmAnimators.Count)
            {
                m_animatorIndex = 0;
            }
        }

        void IncrementFixedUpdateAnimatorIndex()
        {
            ++m_fixedAnimatorIndex;
            if (m_fixedAnimatorIndex >= m_fixedUpdateMxMAnimators.Count)
            {
                m_fixedAnimatorIndex = 0;
            }
        }

        void Initialize()
        {
            m_mxmAnimators = new List<MxMAnimator>(m_expectedAnimatorCount);
            m_fixedUpdateMxMAnimators = new List<MxMAnimator>(m_expectedPhysicsAnimatorCount);
        }

        private void Awake()
        {
            if (!m_instance)
            {
                m_instance = this;
                Initialize();
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (m_mxmAnimators.Count == 0)
                return;
            
            int startIndex = Mathf.Clamp(m_animatorIndex, 0, Mathf.Max(0, m_mxmAnimators.Count-1));
            for(int i = 0; i < m_mxmAnimators.Count; ++i)
            {
                int thisIndex = (startIndex + i) % m_mxmAnimators.Count;
                
                MxMAnimator mxmAnimator = m_mxmAnimators[thisIndex];
                if (mxmAnimator)
                {
                    if (mxmAnimator.CanUpdate)
                    {
#if UNITY_2019_1_OR_NEWER
                        mxmAnimator.CacheRiggingIntegration();
#endif
                        mxmAnimator.MxMUpdate_Phase1(Time.deltaTime);
                    }
                }
                else
                {
                    //Unregister any null mxmAnimators
                    m_mxmAnimators.RemoveAt(thisIndex);
                    --i;
                }
            }
            
            //Only Schedule the jobs in batch once all animators have updated
            JobHandle.ScheduleBatchedJobs();
        }

        void FixedUpdate()
        {
            if (m_fixedUpdateMxMAnimators.Count == 0)
                return;
            
            int startIndex = Mathf.Clamp(m_fixedAnimatorIndex, 0, Mathf.Max(0, m_fixedUpdateMxMAnimators.Count-1));
            for(int i = 0; i < m_fixedUpdateMxMAnimators.Count; ++i)
            {
                int thisIndex = (startIndex + i) % m_fixedUpdateMxMAnimators.Count;
                
                MxMAnimator mxmAnimator = m_fixedUpdateMxMAnimators[thisIndex];
                if (mxmAnimator)
                {
                    if ( mxmAnimator.CanUpdate)
                    {
#if UNITY_2019_1_OR_NEWER
                        mxmAnimator.CacheRiggingIntegration();
#endif
                        mxmAnimator.MxMUpdate_Phase1(Time.fixedDeltaTime);
                    }
                }
                else
                {
                    m_fixedUpdateMxMAnimators.RemoveAt(thisIndex);
                    --i;
                }
            }
            
            //Only Schedule the jobs in batch once all animators have updated
            JobHandle.ScheduleBatchedJobs();
        }

        void UpdatePhase2()
        {
            
        }

        void LateUpdate()
        {
            foreach (MxMAnimator mxmAnimator in m_mxmAnimators)
            {
                if (mxmAnimator && mxmAnimator.CanUpdate)
                {
                        mxmAnimator.MxMLateUpdate();
                }
            }

            foreach (MxMAnimator mxmAnimator in m_fixedUpdateMxMAnimators)
            {
                if (mxmAnimator && mxmAnimator.CanUpdate)
                {
                    mxmAnimator.MxMLateUpdate();
                }
            }
            
            m_searchesThisFrame = 0;
            
        }

        public void RegisterMxMAnimator(MxMAnimator a_mxmAnimator)
        {
            if (!a_mxmAnimator)
                return;

#if UNITY_6_1_OR_NEWER
            if (a_mxmAnimator.UpdateMode == AnimatorUpdateMode.Fixed)
#else
            if (a_mxmAnimator.UpdateMode == AnimatorUpdateMode.AnimatePhysics)
#endif
            {
                m_fixedUpdateMxMAnimators.Add(a_mxmAnimator);
            }
            else
            {
                m_mxmAnimators.Add(a_mxmAnimator);
            }
        }

        public void UnRegisterMxMAnimator(MxMAnimator a_mxmAnimator)
        {
            if (!a_mxmAnimator)
                return;
            
#if UNITY_6_1_OR_NEWER
            if (a_mxmAnimator.UpdateMode == AnimatorUpdateMode.Fixed)
#else
            if (a_mxmAnimator.UpdateMode == AnimatorUpdateMode.AnimatePhysics)
#endif
            {
                m_fixedUpdateMxMAnimators.Remove(a_mxmAnimator);
                m_fixedAnimatorIndex = Mathf.Clamp(m_fixedAnimatorIndex, 0, Mathf.Max(0, m_fixedUpdateMxMAnimators.Count - 1));
            }
            else
            {
                m_mxmAnimators.Remove(a_mxmAnimator);
                m_animatorIndex = Mathf.Clamp(m_animatorIndex, 0, Mathf.Max(0, m_mxmAnimators.Count-1));
            }
        }

        public bool RequestPoseSearch(MxMAnimator a_mxmAnimator, float a_searchDelay, bool a_forceSearch)
        {
            if (!a_mxmAnimator)
                return false;
            
            if (a_forceSearch //Force Search If Requested
                || a_mxmAnimator.PriorityUpdate //Force Search If Priority Animator
                || m_searchesThisFrame < m_maxSearchesPerFrame //Allow Search If max has not been reached
                || a_searchDelay > Mathf.Min(m_maxAllowableDelay,a_mxmAnimator.MaxUpdateDelay)) //Allow search if max delay has been exceeded
            {
    #if UNITY_6_1_OR_NEWER
            if (a_mxmAnimator.UpdateMode == AnimatorUpdateMode.Fixed)
#else
            if (a_mxmAnimator.UpdateMode == AnimatorUpdateMode.AnimatePhysics)
#endif
                {
                    IncrementFixedUpdateAnimatorIndex();
                }
                else
                {
                    IncrementAnimatorIndex();
                }
                
                ++m_searchesThisFrame;
                return true;
            }
            
            return false;
        }
    }
}