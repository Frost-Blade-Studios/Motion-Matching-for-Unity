# Quick Start Guide

Get your first Motion Matching character up and running in 30 minutes.

---

## Prerequisites

Before starting, ensure you have:

- **Unity 2020.3 LTS or newer** (2022.3 LTS recommended)
- **MxM installed** - See [Installation Guide](getting-started.md#installation)
- **Humanoid or Generic rigged character** with animation clips
- **10-20 animation clips minimum** (locomotion recommended for first test)

!!! tip "First Time with Motion Matching?"
    Motion Matching works differently than traditional animators. Instead of setting up state machines and transitions, you'll be creating an animation **database** that the system searches in real-time.

---

## Step 1: Prepare Your Animation Clips

### What You Need

For a basic locomotion setup, gather these animation types:

| Animation Type | Quantity | Examples |
|----------------|----------|----------|
| **Idle** | 1-2 clips | Standing idle, breathing |
| **Walk** | 2-4 clips | Walk forward, walk strafe left/right |
| **Run** | 2-4 clips | Run forward, run strafe left/right |
| **Turns** | 2-4 clips (optional) | Turn 90°, turn 180° |
| **Starts/Stops** | 2-4 clips (optional) | Start from idle, stop to idle |

!!! warning "Animation Requirements"
    - All clips must use the **same rig/skeleton**
    - Clips should have **consistent FPS** (30 or 60 recommended)
    - **Root motion** should be baked into the animations
    - Motion capture data produces the best results

### Import Settings

For each animation clip:

1. Select the animation file in Unity
2. In the Inspector, set:
   - **Rig**: Humanoid (or Generic if appropriate)
   - **Root Transform Rotation**: Bake Into Pose
   - **Root Transform Position (Y)**: Bake Into Pose
   - **Root Transform Position (XZ)**: Based Upon → Original
3. Click **Apply**

---

## Step 2: Create the MxM Anim Data Asset

The **Anim Data** asset is the heart of MxM - it's your pre-processed animation database.

### Create the Asset

1. **Right-click** in your Project window
2. **Create → MxM → Anim Data**
3. Name it descriptively (e.g., `PlayerLocomotion_AnimData`)

### Configure Basic Settings

Select your new Anim Data asset and configure:

#### General Settings

| Setting | Value | Notes |
|---------|-------|-------|
| **Pose Interval** | 0.1 seconds | Lower = more poses = smoother but slower |
| **Blending Mode** | Inertial Blending | Standard for most characters |

#### Skeleton Configuration

1. Click **"Configure Skeleton"**
2. Select key joints for matching:

**Minimum Setup:**
- Hips (root)
- Left Foot
- Right Foot

**Recommended Setup (better quality):**
- Hips (root)
- Left Foot / Right Foot
- Left Hand / Right Hand
- Head (optional)

3. Set **Joint Weights**:
   - Feet: **1.0** (critical for foot placement)
   - Hands: **0.7** (important for upper body)
   - Hips: **0.8** (critical for root motion)
   - Head: **0.5** (less critical)

#### Trajectory Configuration

Configure how far the system looks ahead:

| Trajectory Point | Time Offset | Recommended Weight |
|------------------|-------------|-------------------|
| **Point 1** | 0.2s ahead | 1.0 |
| **Point 2** | 0.4s ahead | 0.8 |
| **Point 3** | 0.6s ahead | 0.6 |

!!! tip "Trajectory Points"
    More trajectory points = better prediction but slower search. 3 points is a good starting balance.

---

## Step 3: Add Animation Clips

Now add your animation clips to the database:

### Create a Composite

**Composites** group related animations together.

1. In your Anim Data asset, click **"Add Composite"**
2. Name it (e.g., "Locomotion")
3. Set **Type**: Standard Composite

### Add Clips to Composite

1. Click **"Add Clips"** in the composite section
2. Select all your locomotion animation clips
3. Click **"Add"**

### Configure Clips

For each clip in the list:

1. **Review settings** - Most defaults are fine
2. **Tag clips** (optional but recommended):
   - Add tag `"Walk"` to walk clips
   - Add tag `"Run"` to run clips
   - Add tag `"Idle"` to idle clips

!!! info "Tags are powerful"
    Tags let you control which animations are available at runtime. For example, you can disable "Run" animations when the character is crouching.

---

## Step 4: Pre-Process the Database

This is where MxM analyzes your animations and builds the searchable database.

### Start Processing

1. Scroll to the bottom of your Anim Data inspector
2. Click **"Pre-Process"**
3. **Wait** - This can take 5-30 minutes depending on:
   - Number of clips
   - Clip lengths
   - Pose interval setting
   - Your computer's performance

### What's Happening

During pre-processing, MxM:

1. **Samples poses** at the specified interval (every 0.1s)
2. **Calculates trajectories** for each pose
3. **Builds feature vectors** for fast searching
4. **Creates acceleration structures** (KD-trees)

!!! warning "Processing Time"
    - 10 clips @ 0.1s interval: ~5 minutes
    - 50 clips @ 0.1s interval: ~15-20 minutes
    - 100 clips @ 0.05s interval: ~30+ minutes
    
    Go grab coffee - this is a one-time setup!

### Verify Success

After processing:

- **Green clips**: Successfully processed
- **Red clips**: Errors (check console for details)
- **Pose count**: Should show total poses extracted

---

## Step 5: Set Up Your Character

Now let's apply MxM to your character in the scene.

### Add the Character

1. **Drag your character** into the scene
2. Ensure it has an **Animator** component
3. The Animator **Controller** can be empty or basic

### Add MxMAnimator Component

1. **Select your character** in the Hierarchy
2. Click **Add Component**
3. Search for **"MxMAnimator"**
4. Click to add

### Configure MxMAnimator

In the MxMAnimator component:

#### References
1. **Anim Data**: Drag your `PlayerLocomotion_AnimData` asset here
2. **Animator**: Auto-assigned (or drag your Animator)
3. **Transform**: Auto-assigned (root transform)

#### Runtime Settings

| Setting | Recommended Value | Description |
|---------|------------------|-------------|
| **Update Mode** | Normal | Standard update timing |
| **Blend Time** | 0.3 seconds | Transition smoothness |
| **Playback Speed** | 1.0 | Normal speed |
| **Search Frequency** | Every Frame | Best quality (can optimize later) |

---

## Step 6: Set Up Input and Trajectory

MxM needs to know where the character wants to go.

### Add Trajectory Generator

1. **Select your character**
2. Click **Add Component**
3. Search for **"MxMTrajectoryGenerator"**
4. Click to add

### Create Input Profile

1. **Right-click** in Project window
2. **Create → MxM → Input Profile**
3. Name it (e.g., `PlayerInputProfile`)

### Configure Input Profile

Select your Input Profile:

1. **Input Type**: Choose based on your project:
   - **Unity Input System** (new Input System)
   - **Legacy Input Manager** (old Input.GetAxis)
   - **Custom** (scripted input)

2. **Movement Bindings**:
   - Horizontal Axis: `"Horizontal"` (A/D or Left/Right)
   - Vertical Axis: `"Vertical"` (W/S or Up/Down)

3. **Trajectory Settings**:
   
   | Setting | Value | Description |
   |---------|-------|-------------|
   | **Max Speed** | 5.0 | Maximum movement speed |
   | **Acceleration** | 10.0 | How quickly speed ramps up |
   | **Responsiveness** | 0.8 | Path prediction smoothness |

### Link Input to Trajectory Generator

1. **Select your character**
2. In **MxMTrajectoryGenerator**:
   - **Input Profile**: Drag your `PlayerInputProfile`
   - **MxM Animator**: Drag the MxMAnimator (or auto-link)

---

## Step 7: Test Your Character

Time to see motion matching in action!

### Enter Play Mode

1. Click **Play** in Unity
2. **Use WASD** or arrow keys to move

### What to Expect

**If working correctly:**
- Character smoothly blends between animations
- Movement feels responsive to input
- Transitions happen automatically without setup
- Foot placement looks natural

**If something's wrong:**
- Character frozen: Check Anim Data is assigned
- Jerky movement: Increase Blend Time
- No response to input: Verify Input Profile settings
- Sliding feet: Re-check root motion settings on clips

---

## Troubleshooting Common Issues

### Character Doesn't Move

**Check:**
- Is Anim Data assigned to MxMAnimator?
- Is Anim Data fully pre-processed (no red clips)?
- Is Input Profile assigned to Trajectory Generator?
- Do animation clips have root motion enabled?

### Animations Look Jerky

**Solutions:**
- Increase **Blend Time** (try 0.4-0.5s)
- Decrease **Pose Interval** (more poses = smoother)
- Check animation FPS consistency

### Character Slides/Doesn't Follow Trajectory

**Check:**
- Root motion settings on animation clips
- Trajectory Generator **Max Speed** matches animation speeds
- Character Controller or Rigidbody isn't conflicting

### Poor Performance / Low FPS

**Optimize:**
- Increase **Pose Interval** (fewer poses = faster)
- Reduce **Trajectory Points**
- Lower **Search Frequency** (every 2-3 frames)
- Reduce number of joints in skeleton setup

---

## Next Steps

### Expand Your Setup

Now that you have basic locomotion working:

1. **Add more animations** - Jumps, attacks, interactions
2. **Use Tags** - Create contextual animation sets
3. **Create Events** - Trigger gameplay events from animations
4. **Tune quality** - Adjust weights and timings

### Learn More

- **[Technical Reference](technical-reference.md)** - Deep dive into how MxM works
- **[Animation Authoring Guide](resources.md#animation-authoring-guide)** - Optimize your animation content
- **[User Manual](resources.md#user-manual)** - Complete feature reference

---

## Quick Reference Checklist

Use this checklist for future characters:

- [ ] Import animation clips with correct settings
- [ ] Create Anim Data asset
- [ ] Configure skeleton (joints + weights)
- [ ] Configure trajectory points
- [ ] Add clips to composite
- [ ] Pre-process database (wait for completion)
- [ ] Add MxMAnimator to character
- [ ] Assign Anim Data reference
- [ ] Add MxMTrajectoryGenerator
- [ ] Create and assign Input Profile
- [ ] Test in Play mode
- [ ] Tune blend time and responsiveness

---

## Performance Tips

### For Desktop/Console Games

- **Pose Interval**: 0.05 - 0.1 seconds
- **Search Frequency**: Every frame
- **Trajectory Points**: 4-5 points
- **Joints**: 8-12 joints (full body)

### For Mobile Games

- **Pose Interval**: 0.15 - 0.2 seconds
- **Search Frequency**: Every 2-3 frames
- **Trajectory Points**: 2-3 points
- **Joints**: 4-6 joints (lower body focus)

### For VR Games

- **Pose Interval**: 0.08 - 0.12 seconds
- **Search Frequency**: Every frame (90fps target)
- **Trajectory Points**: 3-4 points
- **Blend Time**: 0.2s (snappier for responsiveness)

---

!!! success "You're Ready!"
    You now have a working motion matching character! Experiment with different settings, add more animations, and explore the advanced features in the [Technical Reference](technical-reference.md).

---

## Additional Resources

- **[Getting Started Guide](getting-started.md)** - Installation and project setup
- **[Technical Reference](technical-reference.md)** - Architecture and theory
- **[Original PDFs](resources.md)** - Complete original documentation
- **[GitHub Issues](https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity/issues)** - Report problems or get help
