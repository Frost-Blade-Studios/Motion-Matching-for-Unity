// Copyright © 2017-2024 Vault Break Studios Pty Ltd

using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace MxM
{
    //============================================================================================
    /**
    *  @brief Contains the generate jobs portion of the MxMAnimator partial class
    *         
    *********************************************************************************************/
    public partial class MxMAnimator : MonoBehaviour
    {
        //Job Data
        private NativeArray<float> m_poseCosts;
        private NativeArray<float> m_trajCosts;
        private NativeArray<int> m_chosenPoseId;

        private delegate JobHandle GenerateTrajectoryJob(int a_numPoses);
        private delegate JobHandle GeneratePoseJob(int a_numPoses);
        private GenerateTrajectoryJob m_trajJobDelegate;
        private GeneratePoseJob m_poseJobDelegate;
        private JobHandle m_poseJobHandle;
        private JobHandle m_trajJobHandle;
        private JobHandle m_minimaJobHandle;

        //============================================================================================
        /**
        *  @brief This function is called on setup to determine which job generator functions to use
        *  dependent on the Motion Matching character configuration. 
        *  
        *  Each of the Generator functions generates the correct job for that configuration. While this
        *  may not seem flexible, it is done for performance. In each of the jobs, the data is packed
        *  as efficiently as possible to make the most of the data passed in and to efficiently use
        *  SIMD operations with the Burst compiler.
        *         
        *********************************************************************************************/
        private void SetupJobDelegates()
        {
            switch (CurrentAnimData.PosePredictionTimes.Length)
            {
                case 1: { m_trajJobDelegate = GenerateTrajectory1Job; } break;
                case 2: { m_trajJobDelegate = GenerateTrajectory2Job; } break;
                case 3: { m_trajJobDelegate = GenerateTrajectory3Job; } break;
                case 4: { m_trajJobDelegate = GenerateTrajectory4Job; } break;
                case 5: { m_trajJobDelegate = GenerateTrajectory5Job; } break;
                case 6: { m_trajJobDelegate = GenerateTrajectory6Job; } break;
                case 7: { m_trajJobDelegate = GenerateTrajectory7Job; } break;
                case 8: { m_trajJobDelegate = GenerateTrajectory8Job; } break;
                case 9: { m_trajJobDelegate = GenerateTrajectory9Job; } break;
                case 10: { m_trajJobDelegate = GenerateTrajectory10Job; } break;
                case 11: { m_trajJobDelegate = GenerateTrajectory11Job; } break;
                case 12: { m_trajJobDelegate = GenerateTrajectory12Job; } break;
            }

            switch (m_poseMatchMethod)
            {
                case EPoseMatchMethod.Basic:
                    {
                        switch (CurrentAnimData.MatchBones.Length)
                        {
                            case 1: { m_poseJobDelegate = GeneratePose1Job; } break;
                            case 2: { m_poseJobDelegate = GeneratePose2Job; } break;
                            case 3: { m_poseJobDelegate = GeneratePose3Job; } break;
                            case 4: { m_poseJobDelegate = GeneratePose4Job; } break;
                            case 5: { m_poseJobDelegate = GeneratePose5Job; } break;
                            case 6: { m_poseJobDelegate = GeneratePose6Job; } break;
                            case 7: { m_poseJobDelegate = GeneratePose7Job; } break;
                            case 8: { m_poseJobDelegate = GeneratePose8Job; } break;
                        }
                    }
                    break;

                case EPoseMatchMethod.VelocityCosting:
                    {
                        switch (CurrentAnimData.MatchBones.Length)
                        {
                            case 1: { m_poseJobDelegate = GeneratePose1Job_VelCost; } break;
                            case 2: { m_poseJobDelegate = GeneratePose2Job_VelCost; } break;
                            case 3: { m_poseJobDelegate = GeneratePose3Job_VelCost; } break;
                            case 4: { m_poseJobDelegate = GeneratePose4Job_VelCost; } break;
                            case 5: { m_poseJobDelegate = GeneratePose5Job_VelCost; } break;
                            case 6: { m_poseJobDelegate = GeneratePose6Job_VelCost; } break;
                            case 7: { m_poseJobDelegate = GeneratePose7Job_VelCost; } break;
                            case 8: { m_poseJobDelegate = GeneratePose8Job_VelCost; } break;
                        }
                    }
                    break;
            }
        }

        //============================================================================================
        /**
        *  @brief Stops any current MxM jobs from running on this animator.
        *         
        *********************************************************************************************/
        private void StopJobs()
        {
            m_poseJobHandle.Complete();
            m_trajJobHandle.Complete();
        }

        //============================================================================================
        /**
        *  @brief Generates the job to compute trajectory costs for a trajectory of 1 point
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a trajectory for
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GenerateTrajectory1Job(int a_numPoses)
        {
            ref readonly TrajectoryPoint point1 = ref m_desiredGoal[0];
            ref readonly Vector3 point1Pos = ref point1.Position;

            float4 goal = new float4(point1.Position, point1.FacingAngle);

            var computeTrajJob = new ComputeTrajectory01Costs()
            {
                InputTrajectories = CurrentNativeAnimData.TrajectoriesPacked_01,
                DesiredTrajectory = goal,
                TrajPosMultiplier = m_curCalibData.TrajPosMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                TrajFAngleMultiplier = m_curCalibData.TrajFAngleMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                GoalCosts = m_trajCosts
            };

            return computeTrajJob.Schedule(a_numPoses, 64, m_trajJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to compute trajectory costs for a trajectory of 2 points
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a trajectory for
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GenerateTrajectory2Job(int a_numPoses)
        {
            float2x4 goal = new float2x4();

            ref readonly TrajectoryPoint point1 = ref m_desiredGoal[0];
            ref readonly TrajectoryPoint point2 = ref m_desiredGoal[1];

            ref readonly Vector3 point1Pos = ref point1.Position;
            ref readonly Vector3 point2Pos = ref point2.Position;

            goal.c0 = new float2(point1Pos.x, point2Pos.x);
            goal.c1 = new float2(point1Pos.y, point2Pos.y);
            goal.c2 = new float2(point1Pos.z, point2Pos.z);
            goal.c3 = new float2(point1.FacingAngle, point2.FacingAngle);

            var computeTrajJob = new ComputeTrajectory02Costs()
            {
                InputTrajectories = CurrentNativeAnimData.TrajectoriesPacked_02,
                DesiredTrajectory = goal,
                TrajPosMultiplier = m_curCalibData.TrajPosMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                TrajFAngleMultiplier = m_curCalibData.TrajFAngleMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                GoalCosts = m_trajCosts
            };

            return computeTrajJob.Schedule(a_numPoses, 64, m_trajJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to compute trajectory costs for a trajectory of 3 points
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a trajectory for
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GenerateTrajectory3Job(int a_numPoses)
        {
            float3x4 goal = new float3x4();

            ref readonly TrajectoryPoint point1 = ref m_desiredGoal[0];
            ref readonly TrajectoryPoint point2 = ref m_desiredGoal[1];
            ref readonly TrajectoryPoint point3 = ref m_desiredGoal[2];

            ref readonly Vector3 point1Pos = ref point1.Position;
            ref readonly Vector3 point2Pos = ref point2.Position;
            ref readonly Vector3 point3Pos = ref point3.Position;

            goal.c0 = new float3(point1Pos.x, point2Pos.x, point3Pos.x);
            goal.c1 = new float3(point1Pos.y, point2Pos.y, point3Pos.y);
            goal.c2 = new float3(point1Pos.z, point2Pos.z, point3Pos.z);
            goal.c3 = new float3(point1.FacingAngle, point2.FacingAngle, point3.FacingAngle);

            var computeTrajJob = new ComputeTrajectory03Costs()
            {
                InputTrajectories = CurrentNativeAnimData.TrajectoriesPacked_03,
                DesiredTrajectory = goal,
                TrajPosMultiplier = m_curCalibData.TrajPosMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                TrajFAngleMultiplier = m_curCalibData.TrajFAngleMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                GoalCosts = m_trajCosts
            };

            return computeTrajJob.Schedule(a_numPoses, 64, m_trajJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to compute trajectory costs for a trajectory of 4 points
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a trajectory for
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GenerateTrajectory4Job(int a_numPoses)
        {
            float4x4 goal = new float4x4();

            ref readonly TrajectoryPoint point1 = ref m_desiredGoal[0];
            ref readonly TrajectoryPoint point2 = ref m_desiredGoal[1];
            ref readonly TrajectoryPoint point3 = ref m_desiredGoal[2];
            ref readonly TrajectoryPoint point4 = ref m_desiredGoal[3];

            ref readonly Vector3 point1Pos = ref point1.Position;
            ref readonly Vector3 point2Pos = ref point2.Position;
            ref readonly Vector3 point3Pos = ref point3.Position;
            ref readonly Vector3 point4Pos = ref point4.Position;

            goal.c0 = new float4(point1Pos.x, point2Pos.x, point3Pos.x, point4Pos.x);
            goal.c1 = new float4(point1Pos.y, point2Pos.y, point3Pos.y, point4Pos.y);
            goal.c2 = new float4(point1Pos.z, point2Pos.z, point3Pos.z, point4Pos.z);
            goal.c3 = new float4(point1.FacingAngle, point2.FacingAngle, point3.FacingAngle, point4.FacingAngle);

            var computeTrajJob = new ComputeTrajectory04Costs()
            {
                InputTrajectories = CurrentNativeAnimData.TrajectoriesPacked_04,
                DesiredTrajectory = goal,
                TrajPosMultiplier = m_curCalibData.TrajPosMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                TrajFAngleMultiplier = m_curCalibData.TrajFAngleMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                GoalCosts = m_trajCosts
            };

            return computeTrajJob.Schedule(a_numPoses, 64, m_trajJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to compute trajectory costs for a trajectory of 5 points
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a trajectory for
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GenerateTrajectory5Job(int a_numPoses)
        {
            Trajectory6 goal = new Trajectory6();

            ref readonly TrajectoryPoint point1 = ref m_desiredGoal[0];
            ref readonly TrajectoryPoint point2 = ref m_desiredGoal[1];
            ref readonly TrajectoryPoint point3 = ref m_desiredGoal[2];
            ref readonly TrajectoryPoint point4 = ref m_desiredGoal[3];
            ref readonly TrajectoryPoint point5 = ref m_desiredGoal[4];

            ref readonly Vector3 point1Pos = ref point1.Position;
            ref readonly Vector3 point2Pos = ref point2.Position;
            ref readonly Vector3 point3Pos = ref point3.Position;
            ref readonly Vector3 point4Pos = ref point4.Position;
            ref readonly Vector3 point5Pos = ref point5.Position;

            goal.A.c0 = new float3(point1Pos.x, point2Pos.x, point3Pos.x);
            goal.A.c1 = new float3(point1Pos.y, point2Pos.y, point3Pos.y);
            goal.A.c2 = new float3(point1Pos.z, point2Pos.z, point3Pos.z);
            goal.A.c3 = new float3(point1.FacingAngle, point2.FacingAngle, point3.FacingAngle);

            goal.B.c0 = new float3(point4Pos.x, point5Pos.x, 0f);
            goal.B.c1 = new float3(point4Pos.y, point5Pos.y, 0f);
            goal.B.c2 = new float3(point4Pos.z, point5Pos.z, 0f);
            goal.B.c3 = new float3(point4.FacingAngle, point5.FacingAngle, 0f);

            var computeTrajJob = new ComputeTrajectory05Costs()
            {
                InputTrajectories = CurrentNativeAnimData.TrajectoriesPacked_06,
                DesiredTrajectory = goal,
                TrajPosMultiplier = m_curCalibData.TrajPosMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                TrajFAngleMultiplier = m_curCalibData.TrajFAngleMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                GoalCosts = m_trajCosts
            };

            return computeTrajJob.Schedule(a_numPoses, 64, m_trajJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to compute trajectory costs for a trajectory of 6 points
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a trajectory for
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GenerateTrajectory6Job(int a_numPoses)
        {
            Trajectory6 goal = new Trajectory6();

            ref readonly TrajectoryPoint point1 = ref m_desiredGoal[0];
            ref readonly TrajectoryPoint point2 = ref m_desiredGoal[1];
            ref readonly TrajectoryPoint point3 = ref m_desiredGoal[2];
            ref readonly TrajectoryPoint point4 = ref m_desiredGoal[3];
            ref readonly TrajectoryPoint point5 = ref m_desiredGoal[4];
            ref readonly TrajectoryPoint point6 = ref m_desiredGoal[5];

            ref readonly Vector3 point1Pos = ref point1.Position;
            ref readonly Vector3 point2Pos = ref point2.Position;
            ref readonly Vector3 point3Pos = ref point3.Position;
            ref readonly Vector3 point4Pos = ref point4.Position;
            ref readonly Vector3 point5Pos = ref point5.Position;
            ref readonly Vector3 point6Pos = ref point6.Position;

            goal.A.c0 = new float3(point1Pos.x, point2Pos.x, point3Pos.x);
            goal.A.c1 = new float3(point1Pos.y, point2Pos.y, point3Pos.y);
            goal.A.c2 = new float3(point1Pos.z, point2Pos.z, point3Pos.z);
            goal.A.c3 = new float3(point1.FacingAngle, point2.FacingAngle, point3.FacingAngle);

            goal.B.c0 = new float3(point4Pos.x, point5Pos.x, point6Pos.x);
            goal.B.c1 = new float3(point4Pos.y, point5Pos.y, point6Pos.y);
            goal.B.c2 = new float3(point4Pos.z, point5Pos.z, point6Pos.z);
            goal.B.c3 = new float3(point4.FacingAngle, point5.FacingAngle, point6.FacingAngle);

            var computeTrajJob = new ComputeTrajectory06Costs()
            {
                InputTrajectories = CurrentNativeAnimData.TrajectoriesPacked_06,
                DesiredTrajectory = goal,
                TrajPosMultiplier = m_curCalibData.TrajPosMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                TrajFAngleMultiplier = m_curCalibData.TrajFAngleMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                GoalCosts = m_trajCosts
            };

            return computeTrajJob.Schedule(a_numPoses, 64, m_trajJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to compute trajectory costs for a trajectory of 7 points
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a trajectory for
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GenerateTrajectory7Job(int a_numPoses)
        {
            Trajectory8 goal = new Trajectory8();

            ref readonly TrajectoryPoint point1 = ref m_desiredGoal[0];
            ref readonly TrajectoryPoint point2 = ref m_desiredGoal[1];
            ref readonly TrajectoryPoint point3 = ref m_desiredGoal[2];
            ref readonly TrajectoryPoint point4 = ref m_desiredGoal[3];
            ref readonly TrajectoryPoint point5 = ref m_desiredGoal[4];
            ref readonly TrajectoryPoint point6 = ref m_desiredGoal[5];
            ref readonly TrajectoryPoint point7 = ref m_desiredGoal[6];

            ref readonly Vector3 point1Pos = ref point1.Position;
            ref readonly Vector3 point2Pos = ref point2.Position;
            ref readonly Vector3 point3Pos = ref point3.Position;
            ref readonly Vector3 point4Pos = ref point4.Position;
            ref readonly Vector3 point5Pos = ref point5.Position;
            ref readonly Vector3 point6Pos = ref point6.Position;
            ref readonly Vector3 point7Pos = ref point7.Position;

            goal.A.c0 = new float4(point1Pos.x, point2Pos.x, point3Pos.x, point4Pos.x);
            goal.A.c1 = new float4(point1Pos.y, point2Pos.y, point3Pos.y, point4Pos.y);
            goal.A.c2 = new float4(point1Pos.z, point2Pos.z, point3Pos.z, point4Pos.z);
            goal.A.c3 = new float4(point1.FacingAngle, point2.FacingAngle, point3.FacingAngle, point4.FacingAngle);

            goal.B.c0 = new float4(point5Pos.x, point6Pos.x, point7Pos.x, 0f);
            goal.B.c1 = new float4(point5Pos.y, point6Pos.y, point7Pos.y, 0f);
            goal.B.c2 = new float4(point5Pos.z, point6Pos.z, point7Pos.z, 0f);
            goal.B.c3 = new float4(point5.FacingAngle, point6.FacingAngle, point7.FacingAngle, 0f);

            var computeTrajJob = new ComputeTrajectory07Costs()
            {
                InputTrajectories = CurrentNativeAnimData.TrajectoriesPacked_08,
                DesiredTrajectory = goal,
                TrajPosMultiplier = m_curCalibData.TrajPosMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                TrajFAngleMultiplier = m_curCalibData.TrajFAngleMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                GoalCosts = m_trajCosts
            };

            return computeTrajJob.Schedule(a_numPoses, 64, m_trajJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to compute trajectory costs for a trajectory of 8 points
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a trajectory for
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GenerateTrajectory8Job(int a_numPoses)
        {
            Trajectory8 goal = new Trajectory8();

            ref readonly TrajectoryPoint point1 = ref m_desiredGoal[0];
            ref readonly TrajectoryPoint point2 = ref m_desiredGoal[1];
            ref readonly TrajectoryPoint point3 = ref m_desiredGoal[2];
            ref readonly TrajectoryPoint point4 = ref m_desiredGoal[3];
            ref readonly TrajectoryPoint point5 = ref m_desiredGoal[4];
            ref readonly TrajectoryPoint point6 = ref m_desiredGoal[5];
            ref readonly TrajectoryPoint point7 = ref m_desiredGoal[6];
            ref readonly TrajectoryPoint point8 = ref m_desiredGoal[7];

            ref readonly Vector3 point1Pos = ref point1.Position;
            ref readonly Vector3 point2Pos = ref point2.Position;
            ref readonly Vector3 point3Pos = ref point3.Position;
            ref readonly Vector3 point4Pos = ref point4.Position;
            ref readonly Vector3 point5Pos = ref point5.Position;
            ref readonly Vector3 point6Pos = ref point6.Position;
            ref readonly Vector3 point7Pos = ref point7.Position;
            ref readonly Vector3 point8Pos = ref point8.Position;

            goal.A.c0 = new float4(point1Pos.x, point2Pos.x, point3Pos.x, point4Pos.x);
            goal.A.c1 = new float4(point1Pos.y, point2Pos.y, point3Pos.y, point4Pos.y);
            goal.A.c2 = new float4(point1Pos.z, point2Pos.z, point3Pos.z, point4Pos.z);
            goal.A.c3 = new float4(point1.FacingAngle, point2.FacingAngle, point3.FacingAngle, point4.FacingAngle);

            goal.B.c0 = new float4(point5Pos.x, point6Pos.x, point7Pos.x, point8Pos.x);
            goal.B.c1 = new float4(point5Pos.y, point6Pos.y, point7Pos.y, point8Pos.y);
            goal.B.c2 = new float4(point5Pos.z, point6Pos.z, point7Pos.z, point8Pos.z);
            goal.B.c3 = new float4(point5.FacingAngle, point6.FacingAngle, point7.FacingAngle, point8.FacingAngle);

            var computeTrajJob = new ComputeTrajectory08Costs()
            {
                InputTrajectories = CurrentNativeAnimData.TrajectoriesPacked_08,
                DesiredTrajectory = goal,
                TrajPosMultiplier = m_curCalibData.TrajPosMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                TrajFAngleMultiplier = m_curCalibData.TrajFAngleMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                GoalCosts = m_trajCosts
            };

            return computeTrajJob.Schedule(a_numPoses, 64, m_trajJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to compute trajectory costs for a trajectory of 9 points
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a trajectory for
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GenerateTrajectory9Job(int a_numPoses)
        {
            Trajectory9 goal = new Trajectory9();

            ref readonly TrajectoryPoint point1 = ref m_desiredGoal[0];
            ref readonly TrajectoryPoint point2 = ref m_desiredGoal[1];
            ref readonly TrajectoryPoint point3 = ref m_desiredGoal[2];
            ref readonly TrajectoryPoint point4 = ref m_desiredGoal[3];
            ref readonly TrajectoryPoint point5 = ref m_desiredGoal[4];
            ref readonly TrajectoryPoint point6 = ref m_desiredGoal[5];
            ref readonly TrajectoryPoint point7 = ref m_desiredGoal[6];
            ref readonly TrajectoryPoint point8 = ref m_desiredGoal[7];
            ref readonly TrajectoryPoint point9 = ref m_desiredGoal[8];

            ref readonly Vector3 point1Pos = ref point1.Position;
            ref readonly Vector3 point2Pos = ref point2.Position;
            ref readonly Vector3 point3Pos = ref point3.Position;
            ref readonly Vector3 point4Pos = ref point4.Position;
            ref readonly Vector3 point5Pos = ref point5.Position;
            ref readonly Vector3 point6Pos = ref point6.Position;
            ref readonly Vector3 point7Pos = ref point7.Position;
            ref readonly Vector3 point8Pos = ref point8.Position;
            ref readonly Vector3 point9Pos = ref point9.Position;

            goal.A.c0 = new float3(point1Pos.x, point2Pos.x, point3Pos.x);
            goal.A.c1 = new float3(point1Pos.y, point2Pos.y, point3Pos.y);
            goal.A.c2 = new float3(point1Pos.z, point2Pos.z, point3Pos.z);
            goal.A.c3 = new float3(point1.FacingAngle, point2.FacingAngle, point3.FacingAngle);

            goal.B.c0 = new float3(point4Pos.x, point5Pos.x, point6Pos.x);
            goal.B.c1 = new float3(point4Pos.y, point5Pos.y, point6Pos.y);
            goal.B.c2 = new float3(point4Pos.z, point5Pos.z, point6Pos.z);
            goal.B.c3 = new float3(point4.FacingAngle, point5.FacingAngle, point6.FacingAngle);

            goal.C.c0 = new float3(point7Pos.x, point8Pos.x, point9Pos.x);
            goal.C.c1 = new float3(point7Pos.y, point8Pos.y, point9Pos.y);
            goal.C.c2 = new float3(point7Pos.z, point8Pos.z, point9Pos.z);
            goal.C.c3 = new float3(point7.FacingAngle, point8.FacingAngle, point9.FacingAngle);

            var computeTrajJob = new ComputeTrajectory09Costs()
            {
                InputTrajectories = CurrentNativeAnimData.TrajectoriesPacked_09,
                DesiredTrajectory = goal,
                TrajPosMultiplier = m_curCalibData.TrajPosMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                TrajFAngleMultiplier = m_curCalibData.TrajFAngleMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                GoalCosts = m_trajCosts
            };

            return computeTrajJob.Schedule(a_numPoses, 64, m_trajJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to compute trajectory costs for a trajectory of 10 points
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a trajectory for
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GenerateTrajectory10Job(int a_numPoses)
        {
            Trajectory12 goal = new Trajectory12();

            ref readonly TrajectoryPoint point1 = ref m_desiredGoal[0];
            ref readonly TrajectoryPoint point2 = ref m_desiredGoal[1];
            ref readonly TrajectoryPoint point3 = ref m_desiredGoal[2];
            ref readonly TrajectoryPoint point4 = ref m_desiredGoal[3];
            ref readonly TrajectoryPoint point5 = ref m_desiredGoal[4];
            ref readonly TrajectoryPoint point6 = ref m_desiredGoal[5];
            ref readonly TrajectoryPoint point7 = ref m_desiredGoal[6];
            ref readonly TrajectoryPoint point8 = ref m_desiredGoal[7];
            ref readonly TrajectoryPoint point9 = ref m_desiredGoal[8];
            ref readonly TrajectoryPoint point10 = ref m_desiredGoal[9];

            ref readonly Vector3 point1Pos = ref point1.Position;
            ref readonly Vector3 point2Pos = ref point2.Position;
            ref readonly Vector3 point3Pos = ref point3.Position;
            ref readonly Vector3 point4Pos = ref point4.Position;
            ref readonly Vector3 point5Pos = ref point5.Position;
            ref readonly Vector3 point6Pos = ref point6.Position;
            ref readonly Vector3 point7Pos = ref point7.Position;
            ref readonly Vector3 point8Pos = ref point8.Position;
            ref readonly Vector3 point9Pos = ref point9.Position;
            ref readonly Vector3 point10Pos = ref point10.Position;

            goal.A.c0 = new float4(point1Pos.x, point2Pos.x, point3Pos.x, point4Pos.x);
            goal.A.c1 = new float4(point1Pos.y, point2Pos.y, point3Pos.y, point4Pos.y);
            goal.A.c2 = new float4(point1Pos.z, point2Pos.z, point3Pos.z, point4Pos.z);
            goal.A.c3 = new float4(point1.FacingAngle, point2.FacingAngle, point3.FacingAngle, point4.FacingAngle);

            goal.B.c0 = new float4(point5Pos.x, point6Pos.x, point7Pos.x, 0f);
            goal.B.c1 = new float4(point5Pos.y, point6Pos.y, point7Pos.y, 0f);
            goal.B.c2 = new float4(point5Pos.z, point6Pos.z, point7Pos.z, 0f);
            goal.B.c3 = new float4(point5.FacingAngle, point6.FacingAngle, point7.FacingAngle, 0f);

            goal.C.c0 = new float4(point8Pos.x, point9Pos.x, point10Pos.x, 0f);
            goal.C.c1 = new float4(point8Pos.y, point9Pos.y, point10Pos.y, 0f);
            goal.C.c2 = new float4(point8Pos.z, point9Pos.z, point10Pos.z, 0f);
            goal.C.c3 = new float4(point8.FacingAngle, point9.FacingAngle, point10.FacingAngle, 0f);

            var computeTrajJob = new ComputeTrajectory12Costs()
            {
                InputTrajectories = CurrentNativeAnimData.TrajectoriesPacked_12,
                DesiredTrajectory = goal,
                TrajPosMultiplier = m_curCalibData.TrajPosMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                TrajFAngleMultiplier = m_curCalibData.TrajFAngleMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                GoalCosts = m_trajCosts
            };

            return computeTrajJob.Schedule(a_numPoses, 64, m_trajJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to compute trajectory costs for a trajectory of 11 points
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a trajectory for
        *  
        *  @return JobHandle - a handle to the scheduled job 
        *         
        *********************************************************************************************/
        private JobHandle GenerateTrajectory11Job(int a_numPoses)
        {
            Trajectory12 goal = new Trajectory12();

            ref readonly TrajectoryPoint point1 = ref m_desiredGoal[0];
            ref readonly TrajectoryPoint point2 = ref m_desiredGoal[1];
            ref readonly TrajectoryPoint point3 = ref m_desiredGoal[2];
            ref readonly TrajectoryPoint point4 = ref m_desiredGoal[3];
            ref readonly TrajectoryPoint point5 = ref m_desiredGoal[4];
            ref readonly TrajectoryPoint point6 = ref m_desiredGoal[5];
            ref readonly TrajectoryPoint point7 = ref m_desiredGoal[6];
            ref readonly TrajectoryPoint point8 = ref m_desiredGoal[7];
            ref readonly TrajectoryPoint point9 = ref m_desiredGoal[8];
            ref readonly TrajectoryPoint point10 = ref m_desiredGoal[9];
            ref readonly TrajectoryPoint point11 = ref m_desiredGoal[10];

            ref readonly Vector3 point1Pos = ref point1.Position;
            ref readonly Vector3 point2Pos = ref point2.Position;
            ref readonly Vector3 point3Pos = ref point3.Position;
            ref readonly Vector3 point4Pos = ref point4.Position;
            ref readonly Vector3 point5Pos = ref point5.Position;
            ref readonly Vector3 point6Pos = ref point6.Position;
            ref readonly Vector3 point7Pos = ref point7.Position;
            ref readonly Vector3 point8Pos = ref point8.Position;
            ref readonly Vector3 point9Pos = ref point9.Position;
            ref readonly Vector3 point10Pos = ref point10.Position;
            ref readonly Vector3 point11Pos = ref point11.Position;

            goal.A.c0 = new float4(point1Pos.x, point2Pos.x, point3Pos.x, point4Pos.x);
            goal.A.c1 = new float4(point1Pos.y, point2Pos.y, point3Pos.y, point4Pos.y);
            goal.A.c2 = new float4(point1Pos.z, point2Pos.z, point3Pos.z, point4Pos.z);
            goal.A.c3 = new float4(point1.FacingAngle, point2.FacingAngle, point3.FacingAngle, point4.FacingAngle);

            goal.B.c0 = new float4(point5Pos.x, point6Pos.x, point7Pos.x, point8Pos.x);
            goal.B.c1 = new float4(point5Pos.y, point6Pos.y, point7Pos.y, point8Pos.y);
            goal.B.c2 = new float4(point5Pos.z, point6Pos.z, point7Pos.z, point8Pos.z);
            goal.B.c3 = new float4(point5.FacingAngle, point6.FacingAngle, point7.FacingAngle, point8.FacingAngle);

            goal.C.c0 = new float4(point9Pos.x, point10Pos.x, point11Pos.x, 0f);
            goal.C.c1 = new float4(point9Pos.y, point10Pos.y, point11Pos.y, 0f);
            goal.C.c2 = new float4(point9Pos.z, point10Pos.z, point11Pos.z, 0f);
            goal.C.c3 = new float4(point9.FacingAngle, point10.FacingAngle, point11.FacingAngle, 0f);

            var computeTrajJob = new ComputeTrajectory12Costs()
            {
                InputTrajectories = CurrentNativeAnimData.TrajectoriesPacked_12,
                DesiredTrajectory = goal,
                TrajPosMultiplier = m_curCalibData.TrajPosMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                TrajFAngleMultiplier = m_curCalibData.TrajFAngleMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                GoalCosts = m_trajCosts
            };

            return computeTrajJob.Schedule(a_numPoses, 64, m_trajJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to compute trajectory costs for a trajectory of 12 points
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a trajectory for
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GenerateTrajectory12Job(int a_numPoses)
        {
            Trajectory12 goal = new Trajectory12();

            ref readonly TrajectoryPoint point1 = ref m_desiredGoal[0];
            ref readonly TrajectoryPoint point2 = ref m_desiredGoal[1];
            ref readonly TrajectoryPoint point3 = ref m_desiredGoal[2];
            ref readonly TrajectoryPoint point4 = ref m_desiredGoal[3];
            ref readonly TrajectoryPoint point5 = ref m_desiredGoal[4];
            ref readonly TrajectoryPoint point6 = ref m_desiredGoal[5];
            ref readonly TrajectoryPoint point7 = ref m_desiredGoal[6];
            ref readonly TrajectoryPoint point8 = ref m_desiredGoal[7];
            ref readonly TrajectoryPoint point9 = ref m_desiredGoal[8];
            ref readonly TrajectoryPoint point10 = ref m_desiredGoal[9];
            ref readonly TrajectoryPoint point11 = ref m_desiredGoal[10];
            ref readonly TrajectoryPoint point12 = ref m_desiredGoal[11];

            ref readonly Vector3 point1Pos = ref point1.Position;
            ref readonly Vector3 point2Pos = ref point2.Position;
            ref readonly Vector3 point3Pos = ref point3.Position;
            ref readonly Vector3 point4Pos = ref point4.Position;
            ref readonly Vector3 point5Pos = ref point5.Position;
            ref readonly Vector3 point6Pos = ref point6.Position;
            ref readonly Vector3 point7Pos = ref point7.Position;
            ref readonly Vector3 point8Pos = ref point8.Position;
            ref readonly Vector3 point9Pos = ref point9.Position;
            ref readonly Vector3 point10Pos = ref point10.Position;
            ref readonly Vector3 point11Pos = ref point11.Position;
            ref readonly Vector3 point12Pos = ref point12.Position;

            goal.A.c0 = new float4(point1Pos.x, point2Pos.x, point3Pos.x, point4Pos.x);
            goal.A.c1 = new float4(point1Pos.y, point2Pos.y, point3Pos.y, point4Pos.y);
            goal.A.c2 = new float4(point1Pos.z, point2Pos.z, point3Pos.z, point4Pos.z);
            goal.A.c3 = new float4(point1.FacingAngle, point2.FacingAngle, point3.FacingAngle, point4.FacingAngle);

            goal.B.c0 = new float4(point5Pos.x, point6Pos.x, point7Pos.x, point8Pos.x);
            goal.B.c1 = new float4(point5Pos.y, point6Pos.y, point7Pos.y, point8Pos.y);
            goal.B.c2 = new float4(point5Pos.z, point6Pos.z, point7Pos.z, point8Pos.z);
            goal.B.c3 = new float4(point5.FacingAngle, point6.FacingAngle, point7.FacingAngle, point8.FacingAngle);

            goal.C.c0 = new float4(point9Pos.x, point10Pos.x, point11Pos.x, point12Pos.x);
            goal.C.c1 = new float4(point9Pos.y, point10Pos.y, point11Pos.y, point12Pos.y);
            goal.C.c2 = new float4(point9Pos.z, point10Pos.z, point11Pos.z, point12Pos.z);
            goal.C.c3 = new float4(point9.FacingAngle, point10.FacingAngle, point11.FacingAngle, point12.FacingAngle);

            var computeTrajJob = new ComputeTrajectory12Costs()
            {
                InputTrajectories = CurrentNativeAnimData.TrajectoriesPacked_12,
                DesiredTrajectory = goal,
                TrajPosMultiplier = m_curCalibData.TrajPosMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                TrajFAngleMultiplier = m_curCalibData.TrajFAngleMultiplier * m_curCalibData.PoseTrajectoryRatio * 2f,
                GoalCosts = m_trajCosts
            };

            return computeTrajJob.Schedule(a_numPoses, 64, m_trajJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 1 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose1Job(int a_numPoses)
        {
            float3x3 desiredPose = new float3x3();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint1Vel = ref joint1.Velocity;


            desiredPose.c0 = new float3(joint1Pos.x, joint1Vel.x, m_curInterpolatedPose.LocalVelocity.x);
            desiredPose.c1 = new float3(joint1Pos.y, joint1Vel.y, m_curInterpolatedPose.LocalVelocity.y);
            desiredPose.c2 = new float3(joint1Pos.z, joint1Vel.z, m_curInterpolatedPose.LocalVelocity.z);

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose01Costs()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_01,
                DesiredPose = desiredPose,

                Weights = new float3(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                     m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                     poseVelMultiplier),

                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 2 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose2Job(int a_numPoses)
        {
            Pose2 desiredPose = new Pose2();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;

            desiredPose.JointPositions.c0 = new float2(joint1Pos.x, joint2Pos.x);
            desiredPose.JointPositions.c1 = new float2(joint1Pos.y, joint2Pos.y);
            desiredPose.JointPositions.c2 = new float2(joint1Pos.z, joint2Pos.z);

            desiredPose.JointVelocities.c0 = new float3(m_curInterpolatedPose.LocalVelocity.x, joint1Vel.x, joint2Vel.x);
            desiredPose.JointVelocities.c1 = new float3(m_curInterpolatedPose.LocalVelocity.y, joint1Vel.y, joint2Vel.y);
            desiredPose.JointVelocities.c2 = new float3(m_curInterpolatedPose.LocalVelocity.z, joint1Vel.z, joint2Vel.z);

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose02Costs()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_02,
                DesiredPose = desiredPose,
                JointVelocityWeights = new float3(poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier),

                JointPositionWeights = new float2(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier),
                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 3 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job 
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose3Job(int a_numPoses)
        {
            Pose3 desiredPose = new Pose3();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];
            ref readonly JointData joint3 = ref m_curInterpolatedPose.JointsData[2];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;
            ref readonly Vector3 joint3Pos = ref joint3.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;
            ref readonly Vector3 joint3Vel = ref joint3.Velocity;

            desiredPose.JointPositions.c0 = new float3(joint1Pos.x, joint2Pos.x, joint3Pos.x);
            desiredPose.JointPositions.c1 = new float3(joint1Pos.y, joint2Pos.y, joint3Pos.y);
            desiredPose.JointPositions.c2 = new float3(joint1Pos.z, joint2Pos.z, joint3Pos.z);

            desiredPose.JointVelocities.c0 = new float4(m_curInterpolatedPose.LocalVelocity.x, joint1Vel.x, joint2Vel.x, joint3Vel.x);
            desiredPose.JointVelocities.c1 = new float4(m_curInterpolatedPose.LocalVelocity.y, joint1Vel.y, joint2Vel.y, joint3Vel.y);
            desiredPose.JointVelocities.c2 = new float4(m_curInterpolatedPose.LocalVelocity.z, joint1Vel.z, joint2Vel.z, joint3Vel.z);

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose03Costs()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_03,
                DesiredPose = desiredPose,
                JointVelocityWeights = new float4(poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[2] * poseVelMultiplier),

                JointPositionWeights = new float3(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[2] * poseJointMultiplier),
                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 4 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose4Job(int a_numPoses)
        {
            Pose4 desiredPose = new Pose4();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];
            ref readonly JointData joint3 = ref m_curInterpolatedPose.JointsData[2];
            ref readonly JointData joint4 = ref m_curInterpolatedPose.JointsData[3];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;
            ref readonly Vector3 joint3Pos = ref joint3.Position;
            ref readonly Vector3 joint4Pos = ref joint4.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;
            ref readonly Vector3 joint3Vel = ref joint3.Velocity;
            ref readonly Vector3 joint4Vel = ref joint4.Velocity;

            desiredPose.JointPositions.c0 = new float4(joint1Pos.x, joint2Pos.x, joint3Pos.x, joint4Pos.x);
            desiredPose.JointPositions.c1 = new float4(joint1Pos.y, joint2Pos.y, joint3Pos.y, joint4Pos.y);
            desiredPose.JointPositions.c2 = new float4(joint1Pos.z, joint2Pos.z, joint3Pos.z, joint4Pos.z);

            desiredPose.JointVelocities.c0 = new float4(joint1Vel.x, joint2Vel.x, joint3Vel.x, joint4Vel.x);
            desiredPose.JointVelocities.c1 = new float4(joint1Vel.y, joint2Vel.y, joint3Vel.y, joint4Vel.y);
            desiredPose.JointVelocities.c2 = new float4(joint1Vel.z, joint2Vel.z, joint3Vel.z, joint4Vel.z);

            desiredPose.BodyVelocity = m_curInterpolatedPose.LocalVelocity;

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose04Costs()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_04,
                DesiredPose = desiredPose,
                JointVelocityWeights = new float4(m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[2] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[3] * poseVelMultiplier),

                JointPositionWeights = new float4(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[2] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[3] * poseJointMultiplier),

                BodyVelocityWeight = poseVelMultiplier,

                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 5 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose5Job(int a_numPoses)
        {
            Pose5 desiredPose = new Pose5();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];
            ref readonly JointData joint3 = ref m_curInterpolatedPose.JointsData[2];
            ref readonly JointData joint4 = ref m_curInterpolatedPose.JointsData[3];
            ref readonly JointData joint5 = ref m_curInterpolatedPose.JointsData[4];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;
            ref readonly Vector3 joint3Pos = ref joint3.Position;
            ref readonly Vector3 joint4Pos = ref joint4.Position;
            ref readonly Vector3 joint5Pos = ref joint5.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;
            ref readonly Vector3 joint3Vel = ref joint3.Velocity;
            ref readonly Vector3 joint4Vel = ref joint4.Velocity;
            ref readonly Vector3 joint5Vel = ref joint5.Velocity;

            desiredPose.JointPositionsA.c0 = new float3(joint1Pos.x, joint2Pos.x, joint3Pos.x);
            desiredPose.JointPositionsA.c1 = new float3(joint1Pos.y, joint2Pos.y, joint3Pos.y);
            desiredPose.JointPositionsA.c2 = new float3(joint1Pos.z, joint2Pos.z, joint3Pos.z);
            desiredPose.JointPositionsB.c0 = new float2(joint4Pos.x, joint5Pos.x);
            desiredPose.JointPositionsB.c1 = new float2(joint4Pos.y, joint5Pos.y);
            desiredPose.JointPositionsB.c2 = new float2(joint4Pos.z, joint5Pos.z);

            desiredPose.JointVelocitiesA.c0 = new float3(m_curInterpolatedPose.LocalVelocity.x, joint1Vel.x, joint2Vel.x);
            desiredPose.JointVelocitiesA.c1 = new float3(m_curInterpolatedPose.LocalVelocity.y, joint1Vel.y, joint2Vel.y);
            desiredPose.JointVelocitiesA.c2 = new float3(m_curInterpolatedPose.LocalVelocity.z, joint1Vel.z, joint2Vel.z);
            desiredPose.JointVelocitiesB.c0 = new float3(joint3Vel.x, joint4Vel.x, joint5Vel.x);
            desiredPose.JointVelocitiesB.c1 = new float3(joint3Vel.y, joint4Vel.y, joint5Vel.y);
            desiredPose.JointVelocitiesB.c2 = new float3(joint3Vel.z, joint4Vel.z, joint5Vel.z);

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose05Costs()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_05,
                DesiredPose = desiredPose,
                JointVelocityWeightsA = new float3(poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier),

                JointVelocityWeightsB = new float3(m_curCalibData.JointVelocityWeights[2] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[3] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[4] * poseVelMultiplier),

                JointPositionWeightsA = new float3(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[2] * poseJointMultiplier),

                JointPositionWeightsB = new float2(m_curCalibData.JointPositionWeights[3] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[4] * poseJointMultiplier),

                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 6 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose6Job(int a_numPoses)
        {
            Pose6 desiredPose = new Pose6();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];
            ref readonly JointData joint3 = ref m_curInterpolatedPose.JointsData[2];
            ref readonly JointData joint4 = ref m_curInterpolatedPose.JointsData[3];
            ref readonly JointData joint5 = ref m_curInterpolatedPose.JointsData[4];
            ref readonly JointData joint6 = ref m_curInterpolatedPose.JointsData[5];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;
            ref readonly Vector3 joint3Pos = ref joint3.Position;
            ref readonly Vector3 joint4Pos = ref joint4.Position;
            ref readonly Vector3 joint5Pos = ref joint5.Position;
            ref readonly Vector3 joint6Pos = ref joint6.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;
            ref readonly Vector3 joint3Vel = ref joint3.Velocity;
            ref readonly Vector3 joint4Vel = ref joint4.Velocity;
            ref readonly Vector3 joint5Vel = ref joint5.Velocity;
            ref readonly Vector3 joint6Vel = ref joint6.Velocity;

            desiredPose.JointPositionsA.c0 = new float3(joint1Pos.x, joint2Pos.x, joint3Pos.x);
            desiredPose.JointPositionsA.c1 = new float3(joint1Pos.y, joint2Pos.y, joint3Pos.y);
            desiredPose.JointPositionsA.c2 = new float3(joint1Pos.z, joint2Pos.z, joint3Pos.z);
            desiredPose.JointPositionsB.c0 = new float3(joint4Pos.x, joint5Pos.x, joint6Pos.x);
            desiredPose.JointPositionsB.c1 = new float3(joint4Pos.y, joint5Pos.y, joint6Pos.y);
            desiredPose.JointPositionsB.c2 = new float3(joint4Pos.z, joint5Pos.z, joint6Pos.z);

            desiredPose.JointVelocitiesA.c0 = new float3(joint1Vel.x, joint2Vel.x, joint3Vel.x);
            desiredPose.JointVelocitiesA.c1 = new float3(joint1Vel.y, joint2Vel.y, joint3Vel.y);
            desiredPose.JointVelocitiesA.c2 = new float3(joint1Vel.z, joint2Vel.z, joint3Vel.z);
            desiredPose.JointVelocitiesB.c0 = new float3(joint4Vel.x, joint5Vel.x, joint6Vel.x);
            desiredPose.JointVelocitiesB.c1 = new float3(joint4Vel.y, joint5Vel.y, joint6Vel.y);
            desiredPose.JointVelocitiesB.c2 = new float3(joint4Vel.z, joint5Vel.z, joint6Vel.z);
            desiredPose.BodyVelocity = m_curInterpolatedPose.LocalVelocity;

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose06Costs()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_06,
                DesiredPose = desiredPose,
                JointVelocityWeightsA = new float3(m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[2] * poseVelMultiplier),

                JointVelocityWeightsB = new float3(m_curCalibData.JointVelocityWeights[3] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[4] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[5] * poseVelMultiplier),

                JointPositionWeightsA = new float3(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[2] * poseJointMultiplier),

                JointPositionWeightsB = new float3(m_curCalibData.JointPositionWeights[3] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[4] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[5] * poseJointMultiplier),

                BodyVelocityWeight = poseVelMultiplier,

                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 7 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose7Job(int a_numPoses)
        {
            Pose7 desiredPose = new Pose7();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];
            ref readonly JointData joint3 = ref m_curInterpolatedPose.JointsData[2];
            ref readonly JointData joint4 = ref m_curInterpolatedPose.JointsData[3];
            ref readonly JointData joint5 = ref m_curInterpolatedPose.JointsData[4];
            ref readonly JointData joint6 = ref m_curInterpolatedPose.JointsData[5];
            ref readonly JointData joint7 = ref m_curInterpolatedPose.JointsData[6];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;
            ref readonly Vector3 joint3Pos = ref joint3.Position;
            ref readonly Vector3 joint4Pos = ref joint4.Position;
            ref readonly Vector3 joint5Pos = ref joint5.Position;
            ref readonly Vector3 joint6Pos = ref joint6.Position;
            ref readonly Vector3 joint7Pos = ref joint7.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;
            ref readonly Vector3 joint3Vel = ref joint3.Velocity;
            ref readonly Vector3 joint4Vel = ref joint4.Velocity;
            ref readonly Vector3 joint5Vel = ref joint5.Velocity;
            ref readonly Vector3 joint6Vel = ref joint6.Velocity;
            ref readonly Vector3 joint7Vel = ref joint7.Velocity;

            desiredPose.JointPositionsA.c0 = new float4(joint1Pos.x, joint2Pos.x, joint3Pos.x, joint4Pos.x);
            desiredPose.JointPositionsA.c1 = new float4(joint1Pos.y, joint2Pos.y, joint3Pos.y, joint4Pos.y);
            desiredPose.JointPositionsA.c2 = new float4(joint1Pos.z, joint2Pos.z, joint3Pos.z, joint4Pos.z);
            desiredPose.JointPositionsB.c0 = new float3(joint5Pos.x, joint6Pos.x, joint7Pos.x);
            desiredPose.JointPositionsB.c1 = new float3(joint5Pos.y, joint6Pos.y, joint7Pos.y);
            desiredPose.JointPositionsB.c2 = new float3(joint5Pos.z, joint6Pos.z, joint7Pos.z);

            desiredPose.JointVelocitiesA.c0 = new float4(m_curInterpolatedPose.LocalVelocity.x, joint1Vel.x, joint2Vel.x, joint3Vel.x);
            desiredPose.JointVelocitiesA.c1 = new float4(m_curInterpolatedPose.LocalVelocity.y, joint1Vel.y, joint2Vel.y, joint3Vel.y);
            desiredPose.JointVelocitiesA.c2 = new float4(m_curInterpolatedPose.LocalVelocity.z, joint1Vel.z, joint2Vel.z, joint3Vel.z);
            desiredPose.JointVelocitiesB.c0 = new float4(joint4Vel.x, joint5Vel.x, joint6Vel.x, joint7Vel.x);
            desiredPose.JointVelocitiesB.c1 = new float4(joint4Vel.y, joint5Vel.y, joint6Vel.y, joint7Vel.y);
            desiredPose.JointVelocitiesB.c2 = new float4(joint4Vel.z, joint5Vel.z, joint6Vel.z, joint7Vel.z);

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose07Costs()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_07,
                DesiredPose = desiredPose,
                JointVelocityWeightsA = new float4(poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[2] * poseVelMultiplier),

                JointVelocityWeightsB = new float4(m_curCalibData.JointVelocityWeights[3] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[4] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[5] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[6] * poseVelMultiplier),

                JointPositionWeightsA = new float4(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[2] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[3] * poseJointMultiplier),

                JointPositionWeightsB = new float3(m_curCalibData.JointPositionWeights[4] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[5] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[6] * poseJointMultiplier),

                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 8 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose8Job(int a_numPoses)
        {
            Pose8 desiredPose = new Pose8();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];
            ref readonly JointData joint3 = ref m_curInterpolatedPose.JointsData[2];
            ref readonly JointData joint4 = ref m_curInterpolatedPose.JointsData[3];
            ref readonly JointData joint5 = ref m_curInterpolatedPose.JointsData[4];
            ref readonly JointData joint6 = ref m_curInterpolatedPose.JointsData[5];
            ref readonly JointData joint7 = ref m_curInterpolatedPose.JointsData[6];
            ref readonly JointData joint8 = ref m_curInterpolatedPose.JointsData[7];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;
            ref readonly Vector3 joint3Pos = ref joint3.Position;
            ref readonly Vector3 joint4Pos = ref joint4.Position;
            ref readonly Vector3 joint5Pos = ref joint5.Position;
            ref readonly Vector3 joint6Pos = ref joint6.Position;
            ref readonly Vector3 joint7Pos = ref joint7.Position;
            ref readonly Vector3 joint8Pos = ref joint8.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;
            ref readonly Vector3 joint3Vel = ref joint3.Velocity;
            ref readonly Vector3 joint4Vel = ref joint4.Velocity;
            ref readonly Vector3 joint5Vel = ref joint5.Velocity;
            ref readonly Vector3 joint6Vel = ref joint6.Velocity;
            ref readonly Vector3 joint7Vel = ref joint7.Velocity;
            ref readonly Vector3 joint8Vel = ref joint8.Velocity;

            desiredPose.JointPositionsA.c0 = new float4(joint1Pos.x, joint2Pos.x, joint3Pos.x, joint4Pos.x);
            desiredPose.JointPositionsA.c1 = new float4(joint1Pos.y, joint2Pos.y, joint3Pos.y, joint4Pos.y);
            desiredPose.JointPositionsA.c2 = new float4(joint1Pos.z, joint2Pos.z, joint3Pos.z, joint4Pos.z);
            desiredPose.JointPositionsB.c0 = new float4(joint5Pos.x, joint6Pos.x, joint7Pos.x, joint8Pos.x);
            desiredPose.JointPositionsB.c1 = new float4(joint5Pos.y, joint6Pos.y, joint7Pos.y, joint8Pos.y);
            desiredPose.JointPositionsB.c2 = new float4(joint5Pos.z, joint6Pos.z, joint7Pos.z, joint8Pos.z);

            desiredPose.JointVelocitiesA.c0 = new float4(joint1Vel.x, joint2Vel.x, joint3Vel.x, joint4Vel.x);
            desiredPose.JointVelocitiesA.c1 = new float4(joint1Vel.y, joint2Vel.y, joint3Vel.y, joint4Vel.y);
            desiredPose.JointVelocitiesA.c2 = new float4(joint1Vel.z, joint2Vel.z, joint3Vel.z, joint4Vel.z);
            desiredPose.JointVelocitiesB.c0 = new float4(joint5Vel.x, joint6Vel.x, joint7Vel.x, joint8Vel.x);
            desiredPose.JointVelocitiesB.c1 = new float4(joint5Vel.y, joint6Vel.y, joint7Vel.y, joint8Vel.y);
            desiredPose.JointVelocitiesB.c2 = new float4(joint5Vel.z, joint6Vel.z, joint7Vel.z, joint8Vel.z);

            desiredPose.BodyVelocity = m_curInterpolatedPose.LocalVelocity;

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose08Costs()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_08,
                DesiredPose = desiredPose,
                JointVelocityWeightsA = new float4(m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[2] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[3] * poseVelMultiplier),

                JointVelocityWeightsB = new float4(m_curCalibData.JointVelocityWeights[4] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[5] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[6] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[7] * poseVelMultiplier),

                JointPositionWeightsA = new float4(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[2] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[3] * poseJointMultiplier),

                JointPositionWeightsB = new float4(m_curCalibData.JointPositionWeights[4] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[5] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[6] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[7] * poseJointMultiplier),

                BodyVelocityWeight = poseVelMultiplier,

                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 1 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose1Job_VelCost(int a_numPoses)
        {
            float3x3 desiredPose = new float3x3();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint1Vel = ref joint1.Velocity;

            desiredPose.c0 = new float3(joint1Pos.x, joint1Vel.x, m_curInterpolatedPose.LocalVelocity.x);
            desiredPose.c1 = new float3(joint1Pos.y, joint1Vel.y, m_curInterpolatedPose.LocalVelocity.y);
            desiredPose.c2 = new float3(joint1Pos.z, joint1Vel.z, m_curInterpolatedPose.LocalVelocity.z);

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose01Costs_VelCost()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_01,
                DesiredPose = desiredPose,
                PoseInterval = CurrentAnimData.PoseInterval,
                ResultantVelocityWeight = m_curCalibData.PoseResultantVelocityMultiplier,
                Weights = new float3(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                     m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                     poseVelMultiplier),

                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 2 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose2Job_VelCost(int a_numPoses)
        {
            Pose2 desiredPose = new Pose2();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;

            desiredPose.JointPositions.c0 = new float2(joint1Pos.x, joint2Pos.x);
            desiredPose.JointPositions.c1 = new float2(joint1Pos.y, joint2Pos.y);
            desiredPose.JointPositions.c2 = new float2(joint1Pos.z, joint2Pos.z);

            desiredPose.JointVelocities.c0 = new float3(m_curInterpolatedPose.LocalVelocity.x, joint1Vel.x, joint2Vel.x);
            desiredPose.JointVelocities.c1 = new float3(m_curInterpolatedPose.LocalVelocity.y, joint1Vel.y, joint2Vel.y);
            desiredPose.JointVelocities.c2 = new float3(m_curInterpolatedPose.LocalVelocity.z, joint1Vel.z, joint2Vel.z);

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose02Costs_VelCost()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_02,
                DesiredPose = desiredPose,
                PoseInterval = CurrentAnimData.PoseInterval,
                ResultantVelocityWeight = m_curCalibData.PoseResultantVelocityMultiplier,
        JointVelocityWeights = new float3(poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier),

                JointPositionWeights = new float2(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier),
                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 3 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose3Job_VelCost(int a_numPoses)
        {
            Pose3 desiredPose = new Pose3();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];
            ref readonly JointData joint3 = ref m_curInterpolatedPose.JointsData[2];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;
            ref readonly Vector3 joint3Pos = ref joint3.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;
            ref readonly Vector3 joint3Vel = ref joint3.Velocity;

            desiredPose.JointPositions.c0 = new float3(joint1Pos.x, joint2Pos.x, joint3Pos.x);
            desiredPose.JointPositions.c1 = new float3(joint1Pos.y, joint2Pos.y, joint3Pos.y);
            desiredPose.JointPositions.c2 = new float3(joint1Pos.z, joint2Pos.z, joint3Pos.z);

            desiredPose.JointVelocities.c0 = new float4(m_curInterpolatedPose.LocalVelocity.x, joint1Vel.x, joint2Vel.x, joint3Vel.x);
            desiredPose.JointVelocities.c1 = new float4(m_curInterpolatedPose.LocalVelocity.y, joint1Vel.y, joint2Vel.y, joint3Vel.y);
            desiredPose.JointVelocities.c2 = new float4(m_curInterpolatedPose.LocalVelocity.z, joint1Vel.z, joint2Vel.z, joint3Vel.z);

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose03Costs_VelCost()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_03,
                DesiredPose = desiredPose,
                PoseInterval = CurrentAnimData.PoseInterval,
                ResultantVelocityWeight = m_curCalibData.PoseResultantVelocityMultiplier,
                JointVelocityWeights = new float4(poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[2] * poseVelMultiplier),

                JointPositionWeights = new float3(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[2] * poseJointMultiplier),
                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 4 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose4Job_VelCost(int a_numPoses)
        {
            Pose4 desiredPose = new Pose4();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];
            ref readonly JointData joint3 = ref m_curInterpolatedPose.JointsData[2];
            ref readonly JointData joint4 = ref m_curInterpolatedPose.JointsData[3];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;
            ref readonly Vector3 joint3Pos = ref joint3.Position;
            ref readonly Vector3 joint4Pos = ref joint4.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;
            ref readonly Vector3 joint3Vel = ref joint3.Velocity;
            ref readonly Vector3 joint4Vel = ref joint4.Velocity;

            desiredPose.JointPositions.c0 = new float4(joint1Pos.x, joint2Pos.x, joint3Pos.x, joint4Pos.x);
            desiredPose.JointPositions.c1 = new float4(joint1Pos.y, joint2Pos.y, joint3Pos.y, joint4Pos.y);
            desiredPose.JointPositions.c2 = new float4(joint1Pos.z, joint2Pos.z, joint3Pos.z, joint4Pos.z);

            desiredPose.JointVelocities.c0 = new float4(joint1Vel.x, joint2Vel.x, joint3Vel.x, joint4Vel.x);
            desiredPose.JointVelocities.c1 = new float4(joint1Vel.y, joint2Vel.y, joint3Vel.y, joint4Vel.y);
            desiredPose.JointVelocities.c2 = new float4(joint1Vel.z, joint2Vel.z, joint3Vel.z, joint4Vel.z);

            desiredPose.BodyVelocity = m_curInterpolatedPose.LocalVelocity;

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose04Costs_VelCost()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_04,
                DesiredPose = desiredPose,
                PoseInterval = CurrentAnimData.PoseInterval,
                ResultantVelocityWeight = m_curCalibData.PoseResultantVelocityMultiplier,
                JointVelocityWeights = new float4(m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[2] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[3] * poseVelMultiplier),

                JointPositionWeights = new float4(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[2] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[3] * poseJointMultiplier),

                BodyVelocityWeight = poseVelMultiplier,

                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 5 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose5Job_VelCost(int a_numPoses)
        {
            Pose5 desiredPose = new Pose5();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];
            ref readonly JointData joint3 = ref m_curInterpolatedPose.JointsData[2];
            ref readonly JointData joint4 = ref m_curInterpolatedPose.JointsData[3];
            ref readonly JointData joint5 = ref m_curInterpolatedPose.JointsData[4];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;
            ref readonly Vector3 joint3Pos = ref joint3.Position;
            ref readonly Vector3 joint4Pos = ref joint4.Position;
            ref readonly Vector3 joint5Pos = ref joint5.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;
            ref readonly Vector3 joint3Vel = ref joint3.Velocity;
            ref readonly Vector3 joint4Vel = ref joint4.Velocity;
            ref readonly Vector3 joint5Vel = ref joint5.Velocity;

            desiredPose.JointPositionsA.c0 = new float3(joint1Pos.x, joint2Pos.x, joint3Pos.x);
            desiredPose.JointPositionsA.c1 = new float3(joint1Pos.y, joint2Pos.y, joint3Pos.y);
            desiredPose.JointPositionsA.c2 = new float3(joint1Pos.z, joint2Pos.z, joint3Pos.z);
            desiredPose.JointPositionsB.c0 = new float2(joint4Pos.x, joint5Pos.x);
            desiredPose.JointPositionsB.c1 = new float2(joint4Pos.y, joint5Pos.y);
            desiredPose.JointPositionsB.c2 = new float2(joint4Pos.z, joint5Pos.z);

            desiredPose.JointVelocitiesA.c0 = new float3(m_curInterpolatedPose.LocalVelocity.x, joint1Vel.x, joint2Vel.x);
            desiredPose.JointVelocitiesA.c1 = new float3(m_curInterpolatedPose.LocalVelocity.y, joint1Vel.y, joint2Vel.y);
            desiredPose.JointVelocitiesA.c2 = new float3(m_curInterpolatedPose.LocalVelocity.z, joint1Vel.z, joint2Vel.z);
            desiredPose.JointVelocitiesB.c0 = new float3(joint3Vel.x, joint4Vel.x, joint5Vel.x);
            desiredPose.JointVelocitiesB.c1 = new float3(joint3Vel.y, joint4Vel.y, joint5Vel.y);
            desiredPose.JointVelocitiesB.c2 = new float3(joint3Vel.z, joint4Vel.z, joint5Vel.z);

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose05Costs_VelCost()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_05,
                DesiredPose = desiredPose,
                PoseInterval = CurrentAnimData.PoseInterval,
                ResultantVelocityWeight = m_curCalibData.PoseResultantVelocityMultiplier,
                JointVelocityWeightsA = new float3(poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier),

                JointVelocityWeightsB = new float3(m_curCalibData.JointVelocityWeights[2] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[3] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[4] * poseVelMultiplier),

                JointPositionWeightsA = new float3(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[2] * poseJointMultiplier),

                JointPositionWeightsB = new float2(m_curCalibData.JointPositionWeights[3] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[4] * poseJointMultiplier),

                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 6 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose6Job_VelCost(int a_numPoses)
        {
            Pose6 desiredPose = new Pose6();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];
            ref readonly JointData joint3 = ref m_curInterpolatedPose.JointsData[2];
            ref readonly JointData joint4 = ref m_curInterpolatedPose.JointsData[3];
            ref readonly JointData joint5 = ref m_curInterpolatedPose.JointsData[4];
            ref readonly JointData joint6 = ref m_curInterpolatedPose.JointsData[5];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;
            ref readonly Vector3 joint3Pos = ref joint3.Position;
            ref readonly Vector3 joint4Pos = ref joint4.Position;
            ref readonly Vector3 joint5Pos = ref joint5.Position;
            ref readonly Vector3 joint6Pos = ref joint6.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;
            ref readonly Vector3 joint3Vel = ref joint3.Velocity;
            ref readonly Vector3 joint4Vel = ref joint4.Velocity;
            ref readonly Vector3 joint5Vel = ref joint5.Velocity;
            ref readonly Vector3 joint6Vel = ref joint6.Velocity;

            desiredPose.JointPositionsA.c0 = new float3(joint1Pos.x, joint2Pos.x, joint3Pos.x);
            desiredPose.JointPositionsA.c1 = new float3(joint1Pos.y, joint2Pos.y, joint3Pos.y);
            desiredPose.JointPositionsA.c2 = new float3(joint1Pos.z, joint2Pos.z, joint3Pos.z);
            desiredPose.JointPositionsB.c0 = new float3(joint4Pos.x, joint5Pos.x, joint6Pos.x);
            desiredPose.JointPositionsB.c1 = new float3(joint4Pos.y, joint5Pos.y, joint6Pos.y);
            desiredPose.JointPositionsB.c2 = new float3(joint4Pos.z, joint5Pos.z, joint6Pos.z);

            desiredPose.JointVelocitiesA.c0 = new float3(joint1Vel.x, joint2Vel.x, joint3Vel.x);
            desiredPose.JointVelocitiesA.c1 = new float3(joint1Vel.y, joint2Vel.y, joint3Vel.y);
            desiredPose.JointVelocitiesA.c2 = new float3(joint1Vel.z, joint2Vel.z, joint3Vel.z);
            desiredPose.JointVelocitiesB.c0 = new float3(joint4Vel.x, joint5Vel.x, joint6Vel.x);
            desiredPose.JointVelocitiesB.c1 = new float3(joint4Vel.y, joint5Vel.y, joint6Vel.y);
            desiredPose.JointVelocitiesB.c2 = new float3(joint4Vel.z, joint5Vel.z, joint6Vel.z);
            desiredPose.BodyVelocity = m_curInterpolatedPose.LocalVelocity;

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose06Costs_VelCost()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_06,
                DesiredPose = desiredPose,
                PoseInterval = CurrentAnimData.PoseInterval,
                ResultantVelocityWeight = m_curCalibData.PoseResultantVelocityMultiplier,
                JointVelocityWeightsA = new float3(m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[2] * poseVelMultiplier),

                JointVelocityWeightsB = new float3(m_curCalibData.JointVelocityWeights[3] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[4] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[5] * poseVelMultiplier),

                JointPositionWeightsA = new float3(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[2] * poseJointMultiplier),

                JointPositionWeightsB = new float3(m_curCalibData.JointPositionWeights[3] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[4] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[5] * poseJointMultiplier),

                BodyVelocityWeight = poseVelMultiplier,

                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 7 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose7Job_VelCost(int a_numPoses)
        {
            Pose7 desiredPose = new Pose7();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];
            ref readonly JointData joint3 = ref m_curInterpolatedPose.JointsData[2];
            ref readonly JointData joint4 = ref m_curInterpolatedPose.JointsData[3];
            ref readonly JointData joint5 = ref m_curInterpolatedPose.JointsData[4];
            ref readonly JointData joint6 = ref m_curInterpolatedPose.JointsData[5];
            ref readonly JointData joint7 = ref m_curInterpolatedPose.JointsData[6];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;
            ref readonly Vector3 joint3Pos = ref joint3.Position;
            ref readonly Vector3 joint4Pos = ref joint4.Position;
            ref readonly Vector3 joint5Pos = ref joint5.Position;
            ref readonly Vector3 joint6Pos = ref joint6.Position;
            ref readonly Vector3 joint7Pos = ref joint7.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;
            ref readonly Vector3 joint3Vel = ref joint3.Velocity;
            ref readonly Vector3 joint4Vel = ref joint4.Velocity;
            ref readonly Vector3 joint5Vel = ref joint5.Velocity;
            ref readonly Vector3 joint6Vel = ref joint6.Velocity;
            ref readonly Vector3 joint7Vel = ref joint7.Velocity;

            desiredPose.JointPositionsA.c0 = new float4(joint1Pos.x, joint2Pos.x, joint3Pos.x, joint4Pos.x);
            desiredPose.JointPositionsA.c1 = new float4(joint1Pos.y, joint2Pos.y, joint3Pos.y, joint4Pos.y);
            desiredPose.JointPositionsA.c2 = new float4(joint1Pos.z, joint2Pos.z, joint3Pos.z, joint4Pos.z);
            desiredPose.JointPositionsB.c0 = new float3(joint5Pos.x, joint6Pos.x, joint7Pos.x);
            desiredPose.JointPositionsB.c1 = new float3(joint5Pos.y, joint6Pos.y, joint7Pos.y);
            desiredPose.JointPositionsB.c2 = new float3(joint5Pos.z, joint6Pos.z, joint7Pos.z);

            desiredPose.JointVelocitiesA.c0 = new float4(m_curInterpolatedPose.LocalVelocity.x, joint1Vel.x, joint2Vel.x, joint3Vel.x);
            desiredPose.JointVelocitiesA.c1 = new float4(m_curInterpolatedPose.LocalVelocity.y, joint1Vel.y, joint2Vel.y, joint3Vel.y);
            desiredPose.JointVelocitiesA.c2 = new float4(m_curInterpolatedPose.LocalVelocity.z, joint1Vel.z, joint2Vel.z, joint3Vel.z);
            desiredPose.JointVelocitiesB.c0 = new float4(joint4Vel.x, joint5Vel.x, joint6Vel.x, joint7Vel.x);
            desiredPose.JointVelocitiesB.c1 = new float4(joint4Vel.y, joint5Vel.y, joint6Vel.y, joint7Vel.y);
            desiredPose.JointVelocitiesB.c2 = new float4(joint4Vel.z, joint5Vel.z, joint6Vel.z, joint7Vel.z);

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose07Costs_VelCost()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_07,
                DesiredPose = desiredPose,
                PoseInterval = CurrentAnimData.PoseInterval,
                ResultantVelocityWeight = m_curCalibData.PoseResultantVelocityMultiplier,
                JointVelocityWeightsA = new float4(poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[2] * poseVelMultiplier),

                JointVelocityWeightsB = new float4(m_curCalibData.JointVelocityWeights[3] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[4] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[5] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[6] * poseVelMultiplier),

                JointPositionWeightsA = new float4(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[2] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[3] * poseJointMultiplier),

                JointPositionWeightsB = new float3(m_curCalibData.JointPositionWeights[4] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[5] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[6] * poseJointMultiplier),

                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the job to comput epose costs for a pose of 8 joint
        *  
        *  @param [int] a_numPoses - the nuber of poses to generate a pose job for.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private JobHandle GeneratePose8Job_VelCost(int a_numPoses)
        {
            Pose8 desiredPose = new Pose8();

            ref readonly JointData joint1 = ref m_curInterpolatedPose.JointsData[0];
            ref readonly JointData joint2 = ref m_curInterpolatedPose.JointsData[1];
            ref readonly JointData joint3 = ref m_curInterpolatedPose.JointsData[2];
            ref readonly JointData joint4 = ref m_curInterpolatedPose.JointsData[3];
            ref readonly JointData joint5 = ref m_curInterpolatedPose.JointsData[4];
            ref readonly JointData joint6 = ref m_curInterpolatedPose.JointsData[5];
            ref readonly JointData joint7 = ref m_curInterpolatedPose.JointsData[6];
            ref readonly JointData joint8 = ref m_curInterpolatedPose.JointsData[7];

            ref readonly Vector3 joint1Pos = ref joint1.Position;
            ref readonly Vector3 joint2Pos = ref joint2.Position;
            ref readonly Vector3 joint3Pos = ref joint3.Position;
            ref readonly Vector3 joint4Pos = ref joint4.Position;
            ref readonly Vector3 joint5Pos = ref joint5.Position;
            ref readonly Vector3 joint6Pos = ref joint6.Position;
            ref readonly Vector3 joint7Pos = ref joint7.Position;
            ref readonly Vector3 joint8Pos = ref joint8.Position;

            ref readonly Vector3 joint1Vel = ref joint1.Velocity;
            ref readonly Vector3 joint2Vel = ref joint2.Velocity;
            ref readonly Vector3 joint3Vel = ref joint3.Velocity;
            ref readonly Vector3 joint4Vel = ref joint4.Velocity;
            ref readonly Vector3 joint5Vel = ref joint5.Velocity;
            ref readonly Vector3 joint6Vel = ref joint6.Velocity;
            ref readonly Vector3 joint7Vel = ref joint7.Velocity;
            ref readonly Vector3 joint8Vel = ref joint8.Velocity;

            desiredPose.JointPositionsA.c0 = new float4(joint1Pos.x, joint2Pos.x, joint3Pos.x, joint4Pos.x);
            desiredPose.JointPositionsA.c1 = new float4(joint1Pos.y, joint2Pos.y, joint3Pos.y, joint4Pos.y);
            desiredPose.JointPositionsA.c2 = new float4(joint1Pos.z, joint2Pos.z, joint3Pos.z, joint4Pos.z);
            desiredPose.JointPositionsB.c0 = new float4(joint5Pos.x, joint6Pos.x, joint7Pos.x, joint8Pos.x);
            desiredPose.JointPositionsB.c1 = new float4(joint5Pos.y, joint6Pos.y, joint7Pos.y, joint8Pos.y);
            desiredPose.JointPositionsB.c2 = new float4(joint5Pos.z, joint6Pos.z, joint7Pos.z, joint8Pos.z);

            desiredPose.JointVelocitiesA.c0 = new float4(joint1Vel.x, joint2Vel.x, joint3Vel.x, joint4Vel.x);
            desiredPose.JointVelocitiesA.c1 = new float4(joint1Vel.y, joint2Vel.y, joint3Vel.y, joint4Vel.y);
            desiredPose.JointVelocitiesA.c2 = new float4(joint1Vel.z, joint2Vel.z, joint3Vel.z, joint4Vel.z);
            desiredPose.JointVelocitiesB.c0 = new float4(joint5Vel.x, joint6Vel.x, joint7Vel.x, joint8Vel.x);
            desiredPose.JointVelocitiesB.c1 = new float4(joint5Vel.y, joint6Vel.y, joint7Vel.y, joint8Vel.y);
            desiredPose.JointVelocitiesB.c2 = new float4(joint5Vel.z, joint6Vel.z, joint7Vel.z, joint8Vel.z);

            desiredPose.BodyVelocity = m_curInterpolatedPose.LocalVelocity;

            //Weight Adjustments
            float poseJointMultiplier = m_curCalibData.PoseAspectMultiplier * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;
            float poseVelMultiplier = m_curCalibData.PoseVelocityWeight * (1 - m_curCalibData.PoseTrajectoryRatio) * 2f;

            var computePoseJob = new ComputePose08Costs_VelCost()
            {
                InputPoses = CurrentNativeAnimData.PosesPacked_08,
                DesiredPose = desiredPose,
                PoseInterval = CurrentAnimData.PoseInterval,
                ResultantVelocityWeight = m_curCalibData.PoseResultantVelocityMultiplier,
                JointVelocityWeightsA = new float4(m_curCalibData.JointVelocityWeights[0] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[1] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[2] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[3] * poseVelMultiplier),

                JointVelocityWeightsB = new float4(m_curCalibData.JointVelocityWeights[4] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[5] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[6] * poseVelMultiplier,
                                                  m_curCalibData.JointVelocityWeights[7] * poseVelMultiplier),

                JointPositionWeightsA = new float4(m_curCalibData.JointPositionWeights[0] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[1] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[2] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[3] * poseJointMultiplier),

                JointPositionWeightsB = new float4(m_curCalibData.JointPositionWeights[4] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[5] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[6] * poseJointMultiplier,
                                                  m_curCalibData.JointPositionWeights[7] * poseJointMultiplier),

                BodyVelocityWeight = poseVelMultiplier,

                GoalCosts = m_poseCosts
            };

            return computePoseJob.Schedule(a_numPoses, 64, m_poseJobHandle);
        }

        //============================================================================================
        /**
        *  @brief Generates the appropriate minima job
        *  
        *  @param [int] a_currentPoseId - the id of the current pose of the character
        *  @param [bool] a_enforceClipChange - if the current clip must change
        *  @param [int] a_numPoses - the number of poses to visit.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private int ComputeMinimaJob(int a_currentPoseId, bool a_enforceClipChange, int a_numPoses)
        {
            if (a_enforceClipChange)
            {
                if (m_favourCurrentPose)
                {
                    if (CurrentNativeAnimData.UsedPoseIdMap.TryGetValue(a_currentPoseId, out var usedPoseId))
                    {
                        m_poseCosts[usedPoseId] *= m_currentPoseFavour;
                        m_trajCosts[usedPoseId] *= m_currentPoseFavour;
                    }
                }

                if (FavourTags == ETags.None)
                {
                    var minimaJob = new FindMinima_EnforceClipChange()
                    {
                        PoseCosts = m_poseCosts,
                        TrajCosts = m_trajCosts,
                        PoseFavour = CurrentNativeAnimData.FavourPacked,
                        PoseClipIds = CurrentNativeAnimData.ClipIdsPacked,
                        CurrentClipId = m_chosenPose.PrimaryClipId,
                        ChosenPoseId = m_chosenPoseId
                    };

                    minimaJob.Run();
                    return m_chosenPoseId[0];

                }
                else
                {
                    switch (m_favourTagMethod)
                    {
                        default:
                        case EFavourTagMethod.Exclusive:
                            {
                                var minimaJob = new FindMinima_FavourExclusive_EnforceClipChange()
                                {
                                    PoseCosts = m_poseCosts,
                                    TrajCosts = m_trajCosts,
                                    PoseFavour = CurrentNativeAnimData.FavourPacked,
                                    PoseFavourTags = CurrentNativeAnimData.FavourTagsPacked,
                                    PoseClipIds = CurrentNativeAnimData.ClipIdsPacked,
                                    CurrentClipId = m_chosenPose.PrimaryClipId,
                                    FavourTags = FavourTags,
                                    FavourMultiplier = m_favourMultiplier,
                                    ChosenPoseId = m_chosenPoseId
                                };

                                minimaJob.Run();
                                return m_chosenPoseId[0];
                            }
                        case EFavourTagMethod.Inclusive:
                            {
                                var minimaJob = new FindMinima_FavourInclusive_EnforceClipChange()
                                {
                                    PoseCosts = m_poseCosts,
                                    TrajCosts = m_trajCosts,
                                    PoseFavour = CurrentNativeAnimData.FavourPacked,
                                    PoseFavourTags = CurrentNativeAnimData.FavourTagsPacked,
                                    PoseClipIds = CurrentNativeAnimData.ClipIdsPacked,
                                    CurrentClipId = m_chosenPose.PrimaryClipId,
                                    FavourTags = FavourTags,
                                    FavourMultiplier = m_favourMultiplier,
                                    ChosenPoseId = m_chosenPoseId
                                };
                                
                                minimaJob.Run();
                                return m_chosenPoseId[0];
                            }
                        case EFavourTagMethod.Stacking:
                            {
                                var minimaJob = new FindMinima_FavourStacking_EnforceClipChange()
                                {
                                    PoseCosts = m_poseCosts,
                                    TrajCosts = m_trajCosts,
                                    PoseFavour = CurrentNativeAnimData.FavourPacked,
                                    PoseFavourTags = CurrentNativeAnimData.FavourTagsPacked,
                                    PoseClipIds = CurrentNativeAnimData.ClipIdsPacked,
                                    CurrentClipId = m_chosenPose.PrimaryClipId,
                                    FavourTags = FavourTags,
                                    FavourMultiplier = m_favourMultiplier,
                                    ChosenPoseId = m_chosenPoseId
                                };
                                
                                minimaJob.Run();
                                return m_chosenPoseId[0];
                            }
                    }
                }
            }
            else
            {
                if (CurrentNativeAnimData.UsedPoseIdMap.TryGetValue(a_currentPoseId, out var usedPoseId))
                {
                    m_poseCosts[usedPoseId] *= m_currentPoseFavour;
                }

                if (FavourTags == 0)
                {
                    var minimaJob = new FindMinima()
                    {
                        PoseCosts = m_poseCosts,
                        TrajCosts = m_trajCosts,
                        PoseFavour = CurrentNativeAnimData.FavourPacked,
                        ChosenPoseId = m_chosenPoseId
                    };
                    
                    minimaJob.Run();
                    return m_chosenPoseId[0];
                }
                else
                {
                    switch (m_favourTagMethod)
                    {
                        default:
                        case EFavourTagMethod.Exclusive:
                            {
                                var minimaJob = new FindMinima_FavourExclusive()
                                {
                                    PoseCosts = m_poseCosts,
                                    TrajCosts = m_trajCosts,
                                    PoseFavour = CurrentNativeAnimData.FavourPacked,
                                    PoseFavourTags = CurrentNativeAnimData.FavourTagsPacked,
                                    FavourTags = FavourTags,
                                    FavourMultiplier = m_favourMultiplier,
                                    ChosenPoseId = m_chosenPoseId
                                };

                                minimaJob.Run();
                                return m_chosenPoseId[0];
                            }
                        case EFavourTagMethod.Inclusive:
                            {
                                var minimaJob = new FindMinima_FavourInclusive()
                                {
                                    PoseCosts = m_poseCosts,
                                    TrajCosts = m_trajCosts,
                                    PoseFavour = CurrentNativeAnimData.FavourPacked,
                                    PoseFavourTags = CurrentNativeAnimData.FavourTagsPacked,
                                    FavourTags = FavourTags,
                                    FavourMultiplier = m_favourMultiplier,
                                    ChosenPoseId = m_chosenPoseId
                                };

                                minimaJob.Run();
                                return m_chosenPoseId[0];
                            }
                        case EFavourTagMethod.Stacking:
                            {
                                var minimaJob = new FindMinima_FavourStacking()
                                {
                                    PoseCosts = m_poseCosts,
                                    TrajCosts = m_trajCosts,
                                    PoseFavour = CurrentNativeAnimData.FavourPacked,
                                    PoseFavourTags = CurrentNativeAnimData.FavourTagsPacked,
                                    FavourTags = FavourTags,
                                    FavourMultiplier = m_favourMultiplier,
                                    ChosenPoseId = m_chosenPoseId
                                };

                                minimaJob.Run();
                                return m_chosenPoseId[0];
                            }
                    }
                }
            }
        }
        
                //============================================================================================
        /**
        *  @brief Generates the appropriate minima job
        *  
        *  @param [int] a_currentPoseId - the id of the current pose of the character
        *  @param [bool] a_enforceClipChange - if the current clip must change
        *  @param [int] a_numPoses - the number of poses to visit.
        *  
        *  @return JobHandle - a handle to the scheduled job
        *         
        *********************************************************************************************/
        private void GenerateMinimaJob(int a_currentPoseId, bool a_enforceClipChange, int a_numPoses)
        {
            if (a_enforceClipChange)
            {
                if (m_favourCurrentPose)
                {
                    if (CurrentNativeAnimData.UsedPoseIdMap.TryGetValue(a_currentPoseId, out var usedPoseId))
                    {
                        m_poseCosts[usedPoseId] *= m_currentPoseFavour;
                        m_trajCosts[usedPoseId] *= m_currentPoseFavour;
                    }
                }

                if (FavourTags == ETags.None)
                {
                    var minimaJob = new FindMinima_EnforceClipChange()
                    {
                        PoseCosts = m_poseCosts,
                        TrajCosts = m_trajCosts,
                        PoseFavour = CurrentNativeAnimData.FavourPacked,
                        PoseClipIds = CurrentNativeAnimData.ClipIdsPacked,
                        CurrentClipId = m_chosenPose.PrimaryClipId,
                        ChosenPoseId = m_chosenPoseId
                    };

                    m_minimaJobHandle = minimaJob.Schedule();

                }
                else
                {
                    switch (m_favourTagMethod)
                    {
                        default:
                        case EFavourTagMethod.Exclusive:
                            {
                                var minimaJob = new FindMinima_FavourExclusive_EnforceClipChange()
                                {
                                    PoseCosts = m_poseCosts,
                                    TrajCosts = m_trajCosts,
                                    PoseFavour = CurrentNativeAnimData.FavourPacked,
                                    PoseFavourTags = CurrentNativeAnimData.FavourTagsPacked,
                                    PoseClipIds = CurrentNativeAnimData.ClipIdsPacked,
                                    CurrentClipId = m_chosenPose.PrimaryClipId,
                                    FavourTags = FavourTags,
                                    FavourMultiplier = m_favourMultiplier,
                                    ChosenPoseId = m_chosenPoseId
                                };

                                m_minimaJobHandle = minimaJob.Schedule();
                                break;
                            }
                        case EFavourTagMethod.Inclusive:
                            {
                                var minimaJob = new FindMinima_FavourInclusive_EnforceClipChange()
                                {
                                    PoseCosts = m_poseCosts,
                                    TrajCosts = m_trajCosts,
                                    PoseFavour = CurrentNativeAnimData.FavourPacked,
                                    PoseFavourTags = CurrentNativeAnimData.FavourTagsPacked,
                                    PoseClipIds = CurrentNativeAnimData.ClipIdsPacked,
                                    CurrentClipId = m_chosenPose.PrimaryClipId,
                                    FavourTags = FavourTags,
                                    FavourMultiplier = m_favourMultiplier,
                                    ChosenPoseId = m_chosenPoseId
                                };
                                
                                m_minimaJobHandle = minimaJob.Schedule();
                                break;
                            }
                        case EFavourTagMethod.Stacking:
                            {
                                var minimaJob = new FindMinima_FavourStacking_EnforceClipChange()
                                {
                                    PoseCosts = m_poseCosts,
                                    TrajCosts = m_trajCosts,
                                    PoseFavour = CurrentNativeAnimData.FavourPacked,
                                    PoseFavourTags = CurrentNativeAnimData.FavourTagsPacked,
                                    PoseClipIds = CurrentNativeAnimData.ClipIdsPacked,
                                    CurrentClipId = m_chosenPose.PrimaryClipId,
                                    FavourTags = FavourTags,
                                    FavourMultiplier = m_favourMultiplier,
                                    ChosenPoseId = m_chosenPoseId
                                };
                                
                                m_minimaJobHandle = minimaJob.Schedule();
                                break;
                            }
                    }
                }
            }
            else
            {
                if (CurrentNativeAnimData.UsedPoseIdMap.TryGetValue(a_currentPoseId, out var usedPoseId))
                {
                    m_poseCosts[usedPoseId] *= m_currentPoseFavour;
                }

                if (FavourTags == 0)
                {
                    var minimaJob = new FindMinima()
                    {
                        PoseCosts = m_poseCosts,
                        TrajCosts = m_trajCosts,
                        PoseFavour = CurrentNativeAnimData.FavourPacked,
                        ChosenPoseId = m_chosenPoseId
                    };
                    
                    m_minimaJobHandle = minimaJob.Schedule();
                }
                else
                {
                    switch (m_favourTagMethod)
                    {
                        default:
                        case EFavourTagMethod.Exclusive:
                            {
                                var minimaJob = new FindMinima_FavourExclusive()
                                {
                                    PoseCosts = m_poseCosts,
                                    TrajCosts = m_trajCosts,
                                    PoseFavour = CurrentNativeAnimData.FavourPacked,
                                    PoseFavourTags = CurrentNativeAnimData.FavourTagsPacked,
                                    FavourTags = FavourTags,
                                    FavourMultiplier = m_favourMultiplier,
                                    ChosenPoseId = m_chosenPoseId
                                };

                                m_minimaJobHandle = minimaJob.Schedule();
                                break;
                            }
                        case EFavourTagMethod.Inclusive:
                            {
                                var minimaJob = new FindMinima_FavourInclusive()
                                {
                                    PoseCosts = m_poseCosts,
                                    TrajCosts = m_trajCosts,
                                    PoseFavour = CurrentNativeAnimData.FavourPacked,
                                    PoseFavourTags = CurrentNativeAnimData.FavourTagsPacked,
                                    FavourTags = FavourTags,
                                    FavourMultiplier = m_favourMultiplier,
                                    ChosenPoseId = m_chosenPoseId
                                };

                                m_minimaJobHandle = minimaJob.Schedule();
                                break;
                            }
                        case EFavourTagMethod.Stacking:
                            {
                                var minimaJob = new FindMinima_FavourStacking()
                                {
                                    PoseCosts = m_poseCosts,
                                    TrajCosts = m_trajCosts,
                                    PoseFavour = CurrentNativeAnimData.FavourPacked,
                                    PoseFavourTags = CurrentNativeAnimData.FavourTagsPacked,
                                    FavourTags = FavourTags,
                                    FavourMultiplier = m_favourMultiplier,
                                    ChosenPoseId = m_chosenPoseId
                                };

                                m_minimaJobHandle = minimaJob.Schedule();
                                break;
                            }
                    }
                }
            }
        }


    }//End of partial class: MxMAnimator
} //End of namespace: MxMAnimator_Jobs