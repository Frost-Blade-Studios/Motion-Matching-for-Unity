# Frequently Asked Questions

Common questions and answers about Motion Matching for Unity.

---

## General Questions

### What is Motion Matching?

Motion Matching is a data-driven animation technique that selects the best animation frame from a large database in real-time based on the character's current state and desired trajectory. Unlike traditional state machines, it doesn't require manually setting up transitions between animations.

**Key benefits:**
- No manual transition setup
- Naturally responsive to player input
- Scales automatically with more animation content
- Handles complex locomotion smoothly

### How is MxM different from Unity's Mecanim?

| Feature | Mecanim (FSM) | MxM (Motion Matching) |
|---------|---------------|----------------------|
| **Setup** | Manual state graph | Automated database |
| **Transitions** | Explicitly defined | Automatically found |
| **Scalability** | Becomes complex with many states | Improves with more data |
| **Responsiveness** | Limited by transition logic | Instant frame-accurate matching |
| **Authoring** | Animation + logic work | Primarily animation work |

### Is MxM suitable for my project?

**MxM excels for:**
- Third-person action games with complex movement
- Games requiring realistic human locomotion
- Projects with substantial animation budgets
- Characters with varied terrain navigation

**Consider alternatives for:**
- Simple 2D platformers
- Projects with very limited animation sets (< 10 clips)
- Stylized/exaggerated animation where timing is critical
- Extremely performance-constrained platforms (low-end mobile)

### What's the performance cost?

MxM uses the Burst compiler and Jobs system for performance:

**Typical performance (single character):**
- Desktop/Console: 0.5-2ms per frame
- Mobile (high-end): 1-3ms per frame
- Mobile (low-end): 3-6ms per frame

**Optimization factors:**
- Pose database size (more poses = slower)
- Skeleton joint count (more joints = slower)
- Search frequency (every frame vs every 3 frames)
- Number of active characters

---

## Setup and Installation

### What Unity versions are supported?

- **Minimum**: Unity 2020.3 LTS
- **Recommended**: Unity 2022.3 LTS or newer
- **Tested**: Unity 6 (2023.2+)

This community fork maintains compatibility with modern Unity versions.

### Do I need the Burst compiler or Jobs system?

Yes, MxM relies on:
- **Burst Compiler** for high-performance native code compilation
- **C# Jobs System** for multi-threaded pose searching

Both are included by default in modern Unity versions.

### Can I use MxM with the new Input System?

Yes! MxM supports:
- **Unity's new Input System** (recommended)
- **Legacy Input Manager**
- **Custom input** via scripting

Configure your preference in the Input Profile asset.

### How much animation data do I need?

**Minimum viable:**
- 10-15 clips for basic locomotion
- ~30 seconds of unique animation

**Recommended:**
- 30-50 clips for quality locomotion
- 2-5 minutes of motion capture data

**Professional quality:**
- 100+ clips covering all scenarios
- 10+ minutes of varied mocap

!!! tip "Quality vs Quantity"
    10 high-quality, varied clips beat 50 similar ones. Diversity in movement is key!

---

## Animation and Workflow

### What animation formats work best?

**Best:**
- Motion capture (mocap) data
- Keyframe animation with realistic timing
- Animations with natural variation

**Compatible but challenging:**
- Stylized/exaggerated animations
- Hand-keyed animations with tight timing
- Looping animations with exact cycle requirements

### Do animations need to be looping?

**Not required!** Unlike state machines, MxM doesn't rely on perfect loops.

**However:**
- Looping clips provide more usable poses
- Non-looping clips are fine (motion matching finds transitions anywhere)
- Idle variations work great without loops

### How do I handle root motion?

Root motion should be **baked into your animation clips**.

**Import settings for each clip:**
1. Set Rig to Humanoid or Generic
2. **Root Transform Rotation**: Bake Into Pose
3. **Root Transform Position (Y)**: Bake Into Pose
4. **Root Transform Position (XZ)**: Based Upon → Original

MxM reads root motion from the clips during pre-processing.

### Can I use Generic rigs or only Humanoid?

Both work!

**Humanoid:**
- Easier retargeting between characters
- Better for realistic human characters
- Required for some IK features

**Generic:**
- More flexibility for non-human characters
- Can use custom bone structures
- May perform slightly better

### My animations are sliding or floating. Help!

**Common causes:**

1. **Root motion not configured correctly**
   - Check animation import settings
   - Verify "Bake Into Pose" settings

2. **Trajectory speed mismatch**
   - Trajectory Generator Max Speed doesn't match animation speeds
   - Adjust Max Speed in Input Profile

3. **Ground contact issues**
   - Foot joints not weighted properly in skeleton setup
   - Increase foot joint weights to 1.0

4. **Character controller interference**
   - Character Controller or Rigidbody fighting with MxM
   - Disable physics-based movement when using MxM

---

## Pre-Processing

### How long does pre-processing take?

**Typical times:**
- 10 clips, 0.1s interval: ~5 minutes
- 50 clips, 0.1s interval: ~20 minutes
- 100 clips, 0.05s interval: ~45+ minutes

**Factors:**
- Number of clips
- Total clip duration
- Pose interval (lower = more poses = longer)
- Number of joints in skeleton
- CPU performance

### Do I need to re-process after every change?

**Yes, re-process when:**
- Animation clips are modified
- Clips are added or removed
- Skeleton configuration changes
- Pose interval or trajectory settings change

**No re-processing needed when:**
- Changing runtime settings (blend time, etc.)
- Modifying Input Profiles
- Adjusting tags at runtime
- Changing character controller settings

### Can I process in the background?

Unfortunately no. Unity editor will be unresponsive during processing.

**Best practices:**
- Process during lunch breaks or end of day
- Start with subset of clips for testing
- Only do full processing when needed
- Consider multiple Anim Data assets for iteration

### What does "Red Clip" mean after processing?

Red clips indicate errors:

**Common causes:**
1. **Looping discontinuity** - Start/end frames don't match
2. **Missing root motion** - No movement data found
3. **Skeleton mismatch** - Joints specified don't exist
4. **Corrupted animation data** - Re-import the clip

Check the **Console** for specific error messages.

---

## Runtime and Performance

### Character isn't moving when I press input

**Checklist:**
- [ ] Anim Data is assigned to MxMAnimator
- [ ] Anim Data is fully pre-processed (no errors)
- [ ] Input Profile is assigned to Trajectory Generator
- [ ] Input Profile has correct input bindings
- [ ] Animations have root motion
- [ ] Character has MxMAnimator and Trajectory Generator components

### Movement feels sluggish or delayed

**Solutions:**
1. **Increase Responsiveness** in Trajectory Generator (try 0.9)
2. **Decrease Blend Time** in MxMAnimator (try 0.2s)
3. **Ensure Search Frequency** is "Every Frame"
4. **Check animation speeds** match desired movement speed

### Animations look jerky or stuttery

**Solutions:**
1. **Increase Blend Time** (try 0.4-0.5s)
2. **Decrease Pose Interval** (more poses = smoother)
3. **Add more animation variations**
4. **Ensure consistent clip FPS** (all 30fps or all 60fps)

### Can I use MxM with multiple characters?

Yes! Each character needs:
- Its own MxMAnimator component
- Its own Trajectory Generator
- Can share the same Anim Data asset

**Performance note:** Each character adds processing cost. Test on target hardware with max expected character count.

### How do I optimize for mobile?

**Optimization strategies:**

1. **Increase Pose Interval** to 0.15-0.2s
2. **Reduce Search Frequency** to every 2-3 frames
3. **Fewer trajectory points** (2-3 instead of 4-5)
4. **Fewer skeleton joints** (4-6 instead of 10+)
5. **Smaller animation database** (30-40 clips max)
6. **LOD system** - Disable MxM for distant characters

---

## Advanced Features

### How do Tags work?

**Tags** are labels you assign to animation clips that can be enabled/disabled at runtime.

**Example:**
```csharp
// Enable "Combat" tagged animations
mxmAnimator.EnableTag("Combat");

// Disable "Run" tagged animations (force walk)
mxmAnimator.DisableTag("Run");

// Only allow "Crouch" animations
mxmAnimator.SetRequiredTag("Crouch");
```

**Use cases:**
- Switch between combat/exploration movement
- Force walking in interiors
- Contextual animations based on game state

### Can I blend between different Anim Data assets?

Not directly. Each MxMAnimator uses one Anim Data at a time.

**Workarounds:**
- Create one large Anim Data with all clips
- Use Tags to filter which clips are active
- Swap Anim Data assets at runtime (causes brief pause)

### How do I trigger specific animations?

MxM finds animations automatically, but you can guide it:

**Option 1: Events System**
- Create Event composite with specific animations
- Trigger event via code: `mxmAnimator.TriggerEvent("Attack");`

**Option 2: Required Tags**
- Tag specific clips
- Require that tag: `mxmAnimator.SetRequiredTag("SpecialMove");`

**Option 3: Trajectory Manipulation**
- Shape the desired trajectory to match specific clips
- Works best for locomotion variants

### Can I use MxM with IK (Inverse Kinematics)?

Yes! MxM works with Unity's IK system.

**Setup:**
1. Enable IK on your Animator
2. Implement `OnAnimatorIK()` in your script
3. Apply IK after MxM updates the pose

**Common use cases:**
- Foot placement on uneven terrain
- Hand placement on objects
- Head look-at targets

### Does MxM support animation events?

Yes, through the **Event System**.

**Setup:**
1. Define events in your Anim Data
2. Configure event conditions (tags, windows)
3. Subscribe to event callbacks in code

**Use for:**
- Footstep sounds
- Particle effects
- Gameplay triggers
- Combat hit detection

---

## Troubleshooting

### "No valid pose found" error

**Causes:**
- No animations match current requirements
- All matching clips filtered by tags
- Trajectory too extreme for available animations

**Solutions:**
- Add more animation variety
- Check tag requirements aren't too restrictive
- Reduce trajectory extremes
- Verify clips are processed correctly

### Character "pops" or teleports

**Causes:**
- Frame discontinuity in animation clips
- Blend time too short
- Missing transition coverage

**Solutions:**
- Increase Blend Time
- Add more clips covering transitions
- Check for corrupted animation data
- Verify pose interval isn't too high

### Feet not planted properly

**Solutions:**
1. **Increase foot joint weights** to 1.0
2. **Use IK foot placement** for terrain adaptation
3. **Add more grounded animations**
4. **Check root motion settings**

### Compile errors after importing

**For Unity 2022+:**
- This fork is updated for modern Unity
- Pull latest version from repository
- Check for conflicting packages

**For older Unity:**
- Consider using older MxM version
- Or help update compatibility (PRs welcome!)

---

## Workflow and Best Practices

### What's the recommended workflow for adding new animations?

1. **Import new clips** with correct settings
2. **Add to existing composite** in Anim Data
3. **Tag appropriately** for organization
4. **Re-process** the Anim Data
5. **Test in Play mode**
6. **Iterate** - adjust tags, weights, settings

### Should I create multiple Anim Data assets or one large one?

**Separate Anim Data assets when:**
- Different characters with unique skeletons
- Completely different animation sets (human vs creature)
- Want to swap entire animation sets at runtime

**Single Anim Data when:**
- Same character with many animations
- Want seamless blending across all animations
- Tag system can handle filtering

### How do I test changes without full re-processing?

**Iteration strategies:**
1. **Create test Anim Data** with subset of clips
2. **Test with smaller clip set** first
3. **Use low Pose Interval** initially (0.2s) for fast processing
4. **Do final processing** with full quality settings once happy

### Can I version control the processed Anim Data?

**Anim Data assets are binary and large (100MB+)**

**Recommendations:**
- **Git LFS** for tracking binary assets
- **Or** exclude from version control and re-process per machine
- **Document** exact processing settings for reproducibility

---

## Migration and Compatibility

### Can I migrate from Mecanim to MxM?

Yes! Many projects have migrated successfully.

**Migration steps:**
1. Keep existing Animator component
2. Add MxMAnimator alongside
3. MxM will override Animator playback
4. Original Animator Controller becomes unused

**Benefits:**
- Can switch back easily
- Preserve existing animation clip organization
- Gradual migration path

### Can I use both MxM and traditional animation?

Yes! You can mix:
- **MxM for locomotion** (complex movement)
- **Animator for cutscenes** (precise timing)
- **Timeline for cinematics** (exact choreography)

Toggle MxMAnimator component on/off to switch control.

### Will this work with Unity 6?

Yes, this community fork is tested with Unity 6 (2023.2+).

If you encounter issues, please [report them on GitHub](https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity/issues).

---

## Community and Support

### Where can I get help?

- **Documentation**: [Full docs site](https://frost-blade-studios.github.io/Motion-Matching-for-Unity/)
- **GitHub Issues**: [Report bugs](https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity/issues)
- **GitHub Discussions**: [Ask questions](https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity/discussions)

### Can I contribute to this fork?

Absolutely! Contributions welcome:
- Bug fixes
- Documentation improvements
- Example projects
- Unity version compatibility updates

See the [GitHub repository](https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity) for contribution guidelines.

### Is there a Discord or forum?

Currently, the primary community space is:
- **GitHub Discussions** for Q&A and community interaction

If you'd like to help establish a Discord server, please reach out via GitHub!

---

## Still Have Questions?

- Check the **[Technical Reference](technical-reference.md)** for deep dives
- Review the **[Quick Start Guide](quick-start.md)** for hands-on tutorial
- Browse the **[Original PDFs](resources.md)** for comprehensive docs
- **[Open a Discussion](https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity/discussions)** on GitHub

---

!!! tip "Can't find your question?"
    If your question isn't answered here, please [start a discussion on GitHub](https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity/discussions) - it might become a new FAQ entry!
