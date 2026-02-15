# User Manual

Complete reference documentation for Motion Matching for Unity (MxM).

---

## Table of Contents

1. [System Overview](#system-overview)
2. [Core Components](#core-components)
3. [Anim Data Configuration](#anim-data-configuration)
4. [Runtime Components](#runtime-components)
5. [Input and Trajectory](#input-and-trajectory)
6. [Event System](#event-system)
7. [Advanced Features](#advanced-features)
8. [Scripting API](#scripting-api)
9. [Performance Optimization](#performance-optimization)
10. [Integration Patterns](#integration-patterns)

---

## System Overview

### Architecture

Motion Matching for Unity (MxM) consists of two main phases:

**Pre-Processing (Editor Time):**
- Animation clips → Processed into searchable database
- Stored as **Anim Data** ScriptableObject
- One-time process per animation library

**Runtime (Play Mode):**
- Real-time pose search and matching
- Uses Burst-compiled Jobs for performance
- Automatic blend and playback

### Data Flow

```
┌──────────────────────────────────────────────────────────┐
│                     PRE-PROCESSING                       │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  Animation Clips (.anim)                                 │
│         ↓                                                │
│  Sample Poses at Interval (e.g., every 0.1s)             │
│         ↓                                                │
│  Calculate Trajectories for each pose                    │
│         ↓                                                │
│  Build Feature Vectors                                   │
│         ↓                                                │
│  Create KD-Tree acceleration structure                   │
│         ↓                                                │
│  Anim Data Asset (ready for runtime)                     │
│                                                          │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│                        RUNTIME                           │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  Player Input                                            │
│         ↓                                                │
│  Trajectory Generator (desired path)                     │
│         ↓                                                │
│  MxMAnimator                                             │
│    ├── Current Pose                                      │
│    ├── Desired Trajectory                                │
│    ├── Active Tags                                       │
│    └──→ Search Anim Data (Burst Job)                     │
│              ↓                                           │
│         Best Match Frame                                 │
│              ↓                                           │
│         Blend & Apply to Character                       │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

---

## Core Components

### Anim Data (ScriptableObject)

The central asset containing your processed animation database.

**Location:** Created via `Create → MxM → Anim Data`

**Purpose:**
- Stores all processed pose data
- Contains trajectory information
- Holds skeleton configuration
- Includes tag definitions
- Reference by MxMAnimator at runtime

**File Size:** Can be 50MB-500MB+ depending on clip count and settings

---

### MxMAnimator (Component)

Runtime component that drives character animation.

**Location:** Add to character GameObject via `Add Component → MxM → MxMAnimator`

**Requirements:**
- Character must have `Animator` component
- Anim Data asset must be assigned
- Character must have rigged skeleton

**Purpose:**
- Performs real-time pose searches
- Manages animation blending
- Controls playback
- Interfaces with Animator for final pose application

---

### MxMTrajectoryGenerator (Component)

Converts player input into trajectory predictions.

**Location:** Add to character GameObject via `Add Component → MxM → MxMTrajectoryGenerator`

**Requirements:**
- Input Profile asset
- Reference to MxMAnimator

**Purpose:**
- Reads player/AI input
- Calculates desired movement path
- Provides trajectory to MxMAnimator for matching

---

## Anim Data Configuration

### Creating Anim Data

1. **Right-click** in Project window
2. **Create → MxM → Anim Data**
3. Name descriptively (e.g., `PlayerLocomotion_AnimData`)

### General Settings

#### Pose Interval

**Parameter:** `Pose Interval`  
**Type:** Float (seconds)  
**Default:** 0.1

**Description:** How frequently to sample poses from animation clips.

| Value | Poses (per second) | Quality | Performance |
|-------|-------------------|---------|-------------|
| 0.05s | 20 poses | Highest | Slowest |
| 0.1s | 10 poses | High | Good |
| 0.15s | 6.67 poses | Medium | Better |
| 0.2s | 5 poses | Lower | Best |

**Recommendation:** Start with 0.1s, adjust based on needs.

#### Blending Mode

**Parameter:** `Blending Mode`  
**Type:** Enum  
**Options:**
- `Inertial Blending` - Smooth, natural transitions (default)
- `Linear Blending` - Simpler, faster but less natural

**Recommendation:** Use Inertial Blending unless performance critical.

### Skeleton Configuration

Defines which bones are used for pose matching.

**Steps to configure:**

1. Click **"Configure Skeleton"** button
2. Add joints from character's rig
3. Set weights for each joint

#### Joint Selection

**Essential joints (minimum setup):**
- Hips (root)
- Left Foot
- Right Foot

**Recommended joints (better quality):**
- Hips (root)
- Left Foot / Right Foot
- Left Hand / Right Hand
- Spine or Chest
- Head (optional)

**Advanced joints (high fidelity):**
- All above + Knees, Elbows, Shoulders

#### Joint Weights

**Parameter:** Per-joint weight multiplier  
**Range:** 0.0 - 2.0  
**Default:** 1.0

**Purpose:** Prioritize certain joints during matching.

**Recommended weights:**

| Joint | Weight | Reason |
|-------|--------|--------|
| Feet | 1.0-1.5 | Critical for ground contact |
| Hips | 0.8-1.0 | Important for overall pose |
| Hands | 0.6-1.0 | Varies by importance |
| Head | 0.3-0.6 | Less critical usually |
| Spine | 0.5-0.8 | Moderate importance |

### Trajectory Configuration

Defines how far ahead the system predicts movement.

**Parameters:**

#### Trajectory Points

Add multiple prediction points at different time offsets.

**Example configuration:**

| Point | Time Offset | Weight | Purpose |
|-------|------------|--------|---------|
| 1 | 0.2s | 1.0 | Near-term (critical) |
| 2 | 0.4s | 0.8 | Mid-term |
| 3 | 0.6s | 0.6 | Long-term planning |

**More points = Better prediction but slower search**

**Recommendation:** 3-4 points for most characters.

### Composites

Composites are logical groups of animation clips.

#### Creating a Composite

1. In Anim Data, click **"Add Composite"**
2. Name it (e.g., "Locomotion", "Combat", "Idles")
3. Select composite type

#### Composite Types

**Standard Composite:**
- For continuous, repeating animations
- Locomotion, idles, patrols
- Can loop or transition freely

**Event Composite:**
- For discrete actions
- Attacks, jumps, interactions
- Requires event configuration

**Clip Composite:**
- For one-off animations
- Cutscene clips
- Special animations

#### Adding Clips to Composite

1. Select composite
2. Click **"Add Clips"**
3. Multi-select animation clips from project
4. Click **"Add"**

#### Clip Settings

Per-clip configuration:

**Favor (Multiplier):**
- Increases likelihood of this clip being selected
- Range: 0.1 - 2.0
- Default: 1.0
- Higher = More likely to be chosen

**Pose Favor (Multiplier):**
- Per-pose favor (overrides clip favor)
- Curve-based: favor different parts of clip differently

**Tags:**
- Add metadata tags to clip
- Used for runtime filtering
- Click **"+"** to add tags
- Example tags: "Run", "Walk", "Combat", "Idle"

**Don't Use Tag:**
- Special tag that excludes clip from search
- Used for event animations primarily

### Pre-Processing

After configuration, you must pre-process to generate the database.

#### Starting Pre-Process

1. Scroll to bottom of Anim Data inspector
2. Click **"Pre-Process"** button
3. Wait for completion (progress bar shown)

**Processing time:** 5-45 minutes depending on:
- Number of clips
- Total clip duration
- Pose interval (lower = slower)
- Joint count
- CPU performance

#### Verification

After processing:

**Green clips:** Successfully processed ✓  
**Red clips:** Errors - check console ✗

**Check pose count:** Should show reasonable total (e.g., 5000-50000 poses)

---

## Runtime Components

### MxMAnimator Component

#### Inspector Parameters

##### References

**Anim Data:**
- Drag processed Anim Data asset here
- Required for runtime

**Animator:**
- Reference to character's Animator component
- Usually auto-assigned

**Transform:**
- Root transform of character
- Usually auto-assigned

##### Update Settings

**Update Mode:**
- `Normal` - Updates in Update()
- `AnimatePhysics` - Updates in FixedUpdate()
- `UnscaledTime` - Ignores Time.timeScale

**Recommendation:** Use `Normal` for most characters, `AnimatePhysics` if using physics.

##### Blending

**Blend Time:**
- Duration of transitions between clips
- Range: 0.0 - 1.0 seconds
- Default: 0.3s

**Effects:**
- Lower (0.1-0.2s): Snappier, may jitter
- Higher (0.4-0.6s): Smoother, may feel sluggish

**Playback Speed:**
- Global animation speed multiplier
- Range: 0.1 - 2.0
- Default: 1.0
- Does NOT affect trajectory matching

##### Search Settings

**Search Frequency:**
- How often to search for new match
- Options:
  - `Every Frame` - Best quality
  - `Every 2 Frames` - Good balance
  - `Every 3 Frames` - Performance mode

**Pose Quality:**
- Trade-off between quality and performance
- `Highest`, `High`, `Medium`, `Low`

##### Debug Settings

**Show Debug Info:**
- Displays runtime information in Scene view
- Trajectory visualization
- Current pose info
- Search statistics

**Debug Color:**
- Color for debug visualization

#### Runtime API

Common MxMAnimator methods:

```csharp
// Get the component
MxMAnimator mxmAnim = GetComponent<MxMAnimator>();

// Tag control
mxmAnim.EnableTag("Combat");
mxmAnim.DisableTag("Run");
mxmAnim.SetRequiredTag("Crouch");
mxmAnim.ClearRequiredTags();

// Events
mxmAnim.BeginEvent(eventDefinition);
mxmAnim.StopCurrentEvent();

// State queries
bool isInEvent = mxmAnim.IsEventActive;
int currentPoseId = mxmAnim.CurrentPoseId;
float currentClipTime = mxmAnim.CurrentClipTime;

// Control
mxmAnim.Pause();
mxmAnim.Resume();
mxmAnim.SetPlaybackSpeed(1.5f);
```

---

### MxMTrajectoryGenerator Component

#### Inspector Parameters

##### References

**Input Profile:**
- Drag Input Profile asset here
- Defines input source and configuration

**MxM Animator:**
- Reference to character's MxMAnimator
- Usually auto-assigned

##### Trajectory Settings

**Max Speed:**
- Maximum movement speed (m/s)
- Should match your fastest animation speed
- Default: 5.0

**Acceleration:**
- How quickly speed ramps up
- Higher = more responsive
- Default: 10.0

**Responsiveness:**
- How quickly trajectory adjusts to input changes
- Range: 0.0 - 1.0
- Lower = smoother path, higher = snappier response
- Default: 0.8

**Strafe Enabled:**
- Allow movement in any direction without rotation
- True = character faces forward while strafing
- False = character rotates to face movement direction

#### Debug Visualization

**Show Trajectory:**
- Visualizes predicted path in Scene view
- Useful for debugging input/movement

**Trajectory Color:**
- Color for trajectory line

---

## Input and Trajectory

### Input Profiles

Input Profiles define how input is read and processed.

#### Creating Input Profile

1. **Right-click** in Project window
2. **Create → MxM → Input Profile**
3. Name it (e.g., `PlayerInput`)

#### Input Types

**Unity Input System (New):**
- Requires Input System package
- Most flexible and modern
- Supports rebinding

**Legacy Input Manager:**
- Uses classic Input.GetAxis()
- Simple setup
- Works out of the box

**Custom:**
- Scriptable input
- Full control via code
- Advanced users only

#### Configuration (Legacy Input Example)

**Horizontal Axis:**
- Input axis name for left/right
- Default: `"Horizontal"`
- Maps to A/D or Left/Right arrows

**Vertical Axis:**
- Input axis name for forward/back
- Default: `"Vertical"`
- Maps to W/S or Up/Down arrows

**Sprint Button:**
- Button name for sprint
- Default: `"Fire3"` (Left Shift)

**Crouch Button:**
- Button name for crouch
- Default: `"Fire1"` (Left Ctrl)

### Trajectory Calculation

The trajectory generator creates a predicted movement path.

**Process:**

1. Read input (WASD, joystick, etc.)
2. Apply acceleration and max speed
3. Calculate future positions at trajectory points
4. Smooth path based on responsiveness
5. Apply character rotation (if not strafing)
6. Send to MxMAnimator for matching

**Result:** Array of future positions and facing directions that MxM uses to find best matching animation.

---

## Event System

Events allow discrete actions like attacks, jumps, or interactions.

### Event Workflow

1. Create event animations (wind-up, action, recovery)
2. Add to Event Composite in Anim Data
3. Tag as "Do Not Use" (prevents locomotion matching)
4. Configure event timeline (phases)
5. Create Event Definition asset
6. Trigger via code: `mxmAnimator.BeginEvent(definition)`

### Event Phases

Every event animation has 4 phases:

**Wind-Up (Blend In):**
- System blends from current pose into event
- Can start from any locomotion pose
- Duration: Set via timeline markers

**Action (Mandatory):**
- Core action that must play to completion
- Cannot be interrupted
- Contains the "hit" or critical action

**Follow-Through (Buffer):**
- Energy dissipation, recovery beginning
- Still mandatory
- Transition buffer

**Recovery (Can Exit):**
- Return to locomotion-ready pose
- Can be interrupted early
- Allows early exit to responsiveness

### Event Definition Asset

#### Creating Event Definition

1. **Right-click** in Project window
2. **Create → MxM → Event Definition**
3. Name it (e.g., `Attack_Heavy`)

#### Parameters

**Priority:**
- Can this event interrupt others?
- Higher priority = Can interrupt lower

**Exit with Motion:**
- What to do after event completes
- Options: `Idle`, `Walk`, `Run`, `Match Trajectory`

**Contact Warping:**
- Use motion warping for precision
- Target point (e.g., ledge, button)
- Scales root motion to hit exact position

**Blend In Time:**
- Override blend time for this event
- Useful for instant actions vs slow wind-ups

**Trajectory Offset:**
- Adjust character position/rotation during event
- Advanced feature

### Triggering Events

```csharp
// Simple trigger
mxmAnimator.BeginEvent(attackDefinition);

// Check if event is active
if (mxmAnimator.IsEventActive)
{
    // Can't trigger new event
}

// Stop current event
mxmAnimator.StopCurrentEvent();

// Event callbacks
mxmAnimator.OnEventStart += HandleEventStart;
mxmAnimator.OnEventEnd += HandleEventEnd;
mxmAnimator.OnEventContact += HandleEventContact;
```

---

## Advanced Features

### Tag System

Tags are metadata labels for filtering animations at runtime.

#### Assigning Tags (Editor)

In Anim Data, for each clip:
- Click **"+ Add Tag"**
- Enter tag name (e.g., "Combat", "Run", "Weapon")
- Can add multiple tags per clip

#### Using Tags (Runtime)

```csharp
// Enable specific tagged clips
mxmAnimator.EnableTag("Combat");
// Now only clips tagged "Combat" can be matched

// Disable tags
mxmAnimator.DisableTag("Run");
// "Run" tagged clips excluded from matching

// Require tag (all clips MUST have this tag)
mxmAnimator.SetRequiredTag("Crouch");
// Only "Crouch" clips available

// Clear all tag requirements
mxmAnimator.ClearRequiredTags();

// Multiple tags
mxmAnimator.EnableTag("Combat");
mxmAnimator.EnableTag("Weapon");
// Clips need ANY enabled tag (OR logic)

mxmAnimator.SetRequiredTag("Combat");
mxmAnimator.SetRequiredTag("Weapon");
// Clips need ALL required tags (AND logic)
```

#### Tag Strategies

**By Movement Type:**
- `"Idle"`, `"Walk"`, `"Run"`, `"Sprint"`

**By Context:**
- `"Combat"`, `"Exploration"`, `"Stealth"`

**By Equipment:**
- `"Unarmed"`, `"OneHanded"`, `"TwoHanded"`

**By Terrain:**
- `"Flat"`, `"Stairs"`, `"Slopes"`

### Layer System

NOT to be confused with Unity's Animation Layers.

**MxM Layers** allow multiple MxMAnimators to control different body parts:

**Example:**
- Layer 0 (Full Body): MxMAnimator for locomotion
- Layer 1 (Upper Body): MxMAnimator for aiming/shooting

**Setup:**
1. Add multiple MxMAnimator components
2. Set different layer masks on Animator
3. Each MxMAnimator uses different Anim Data
4. Blend between layers as needed

**Use case:** Shoot while moving, gesture while walking.

### Calibration Sets

Advanced feature for blending between different animation sets.

**Example:**
- Default locomotion
- Carrying heavy object locomotion
- Injured/limping locomotion

**Setup:**
1. Create multiple Anim Data assets
2. Use same skeleton configuration
3. Swap at runtime or blend between them

---

## Scripting API

### Common Use Cases

#### Basic Control

```csharp
using MxM;

public class CharacterController : MonoBehaviour
{
    private MxMAnimator mxmAnimator;
    
    void Start()
    {
        mxmAnimator = GetComponent<MxMAnimator>();
    }
    
    void EnterCombat()
    {
        mxmAnimator.EnableTag("Combat");
    }
    
    void ExitCombat()
    {
        mxmAnimator.DisableTag("Combat");
    }
    
    void Attack()
    {
        if (!mxmAnimator.IsEventActive)
        {
            mxmAnimator.BeginEvent(attackEvent);
        }
    }
}
```

#### Responding to Events

```csharp
void OnEnable()
{
    mxmAnimator.OnEventStart += HandleEventStart;
    mxmAnimator.OnEventContact += HandleEventContact;
    mxmAnimator.OnEventEnd += HandleEventEnd;
}

void OnDisable()
{
    mxmAnimator.OnEventStart -= HandleEventStart;
    mxmAnimator.OnEventContact -= HandleEventContact;
    mxmAnimator.OnEventEnd -= HandleEventEnd;
}

void HandleEventStart(EventDefinition evt)
{
    Debug.Log($"Event started: {evt.name}");
    // Play sound effect, particle effect, etc.
}

void HandleEventContact(EventDefinition evt)
{
    // Contact point hit (for motion warping)
    // Deal damage, spawn effects, etc.
}

void HandleEventEnd(EventDefinition evt)
{
    // Event completed
    // Re-enable controls, etc.
}
```

#### Custom Trajectory

```csharp
public class CustomTrajectory : MonoBehaviour
{
    private MxMTrajectoryGenerator trajGen;
    
    void Start()
    {
        trajGen = GetComponent<MxMTrajectoryGenerator>();
    }
    
    void SetDesiredVelocity(Vector3 velocity)
    {
        trajGen.SetDesiredVelocity(velocity);
    }
    
    void SetDesiredRotation(float angle)
    {
        trajGen.SetDesiredRotation(angle);
    }
}
```

---

## Performance Optimization

### Pre-Processing Optimizations

**Reduce Pose Count:**
- Increase Pose Interval (0.1s → 0.15s)
- Fewer clips
- Shorter clips

**Reduce Complexity:**
- Fewer skeleton joints
- Fewer trajectory points
- Simpler composites

### Runtime Optimizations

**Search Frequency:**
- Change from `Every Frame` to `Every 2-3 Frames`
- Still smooth, significantly faster

**Quality Settings:**
- Lower Pose Quality setting
- Acceptable quality loss for performance gain

**LOD System:**
- Disable MxM for distant characters
- Use simpler animation for background NPCs
- Distance-based quality scaling

### Memory Optimizations

**Anim Data Size:**
- Each Anim Data can be 100s of MB
- Share Anim Data between similar characters
- Use multiple smaller Anim Data for variety

**Unload Unused:**
- Unload Anim Data when not needed
- Use Addressables for large animation sets
- Stream animations for large open worlds

### Profiling

**Unity Profiler Integration:**

Check these markers:
- `MxMAnimator.Update` - Total update time
- `PoseSearch Job` - Burst-compiled search
- `Blending` - Blend calculation time

**Target Performance:**
- Desktop: < 2ms per character
- Console: < 1.5ms per character
- Mobile: < 5ms per character

---

## Integration Patterns

### With Character Controllers

```csharp
public class CharacterMovement : MonoBehaviour
{
    private MxMAnimator mxmAnimator;
    private CharacterController controller;
    
    void Start()
    {
        mxmAnimator = GetComponent<MxMAnimator>();
        controller = GetComponent<CharacterController>();
        
        // Subscribe to movement delta
        mxmAnimator.OnUpdateMove += HandleMovement;
    }
    
    void HandleMovement(Vector3 delta, Quaternion rotation)
    {
        // Apply movement from animation
        controller.Move(delta);
        transform.rotation = rotation;
    }
}
```

### With AI

```csharp
public class AIController : MonoBehaviour
{
    private MxMTrajectoryGenerator trajGen;
    private NavMeshAgent agent;
    
    void Start()
    {
        trajGen = GetComponent<MxMTrajectoryGenerator>();
        agent = GetComponent<NavMeshAgent>();
        
        // Disable agent's rotation (MxM handles it)
        agent.updateRotation = false;
    }
    
    void Update()
    {
        // Feed NavMesh desired velocity to trajectory
        Vector3 desiredVelocity = agent.desiredVelocity;
        trajGen.SetDesiredVelocity(desiredVelocity);
    }
}
```

### With IK

```csharp
public class FootIK : MonoBehaviour
{
    private MxMAnimator mxmAnimator;
    private Animator animator;
    
    void Start()
    {
        mxmAnimator = GetComponent<MxMAnimator>();
        animator = GetComponent<Animator>();
    }
    
    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex == 0)
        {
            // MxM updates pose first
            // Now apply IK corrections
            
            ApplyFootIK(AvatarIKGoal.LeftFoot);
            ApplyFootIK(AvatarIKGoal.RightFoot);
        }
    }
    
    void ApplyFootIK(AvatarIKGoal foot)
    {
        // Raycast to ground
        // Set IK position/rotation
        // Adjust for terrain
    }
}
```

---

## Summary

This manual covers the complete MxM system. For specific workflows:

- **Setup:** See [Getting Started](getting-started.md)
- **Quick implementation:** See [Quick Start](quick-start.md)
- **Animation creation:** See [Animation Guide](animation-guide.md)
- **Troubleshooting:** See [FAQ](faq.md)
- **Theory and architecture:** See [Technical Reference](technical-reference.md)

---

## Quick Reference Tables

### Component Checklist

| Component | Required | Purpose |
|-----------|----------|---------|
| Animator | Yes | Applies final pose |
| MxMAnimator | Yes | Motion matching brain |
| MxMTrajectoryGenerator | Yes | Input → trajectory |
| Character Controller | Recommended | Movement |
| Anim Data Asset | Yes | Animation database |
| Input Profile Asset | Yes | Input configuration |

### Recommended Settings by Platform

#### Desktop/Console

```
Pose Interval: 0.08 - 0.1s
Search Frequency: Every Frame
Joints: 8-12
Trajectory Points: 4-5
Pose Quality: Highest
```

#### Mobile (High-End)

```
Pose Interval: 0.12 - 0.15s
Search Frequency: Every 2 Frames
Joints: 6-8
Trajectory Points: 3
Pose Quality: High
```

#### Mobile (Low-End)

```
Pose Interval: 0.15 - 0.2s
Search Frequency: Every 3 Frames
Joints: 4-6
Trajectory Points: 2-3
Pose Quality: Medium
```

---

!!! tip "Need More Help?"
    - [Technical Reference](technical-reference.md) for deep understanding
    - [FAQ](faq.md) for common questions
    - [GitHub Discussions](https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity/discussions) for community support
