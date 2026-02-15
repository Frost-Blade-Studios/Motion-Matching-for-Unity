# Animation Authoring Guide

A comprehensive guide for animators and technical artists preparing animation content for Motion Matching.

---

## Introduction

Motion Matching fundamentally changes how animation content is authored and utilized. Unlike traditional state machines that require carefully timed loops and precise transitions, Motion Matching treats your animation library as a searchable database.

### Key Differences from Traditional Animation

| Traditional FSM | Motion Matching |
|----------------|-----------------|
| Perfect loops required | Loops helpful but not required |
| Exact transition timing critical | System finds transitions automatically |
| Limited reusability | High reusability of poses |
| State-based organization | Data-based organization |
| Manual transition crafting | Automatic pose selection |

!!! success "The Animation Quality Principle"
    In Motion Matching, animation **quality and variety** matter more than **organization and timing**. Focus on natural, varied movement rather than perfect loops.

---

## Animation Requirements

### Technical Specifications

#### Rigging Requirements

**Humanoid Characters:**
- Unity Humanoid rig with proper T-pose
- Consistent bone hierarchy across all clips
- Proper hip/root bone setup for root motion

**Generic Characters:**
- Consistent skeleton structure
- Clearly defined root bone
- Bone naming consistency across clips

#### Frame Rate

**Recommended:**
- 30 FPS or 60 FPS (consistent across all clips)
- Avoid mixing frame rates in the same database

**Acceptable:**
- Any frame rate works, but consistency is key
- MxM samples at its own rate during pre-processing

#### Clip Length

**No strict requirements**, but practical guidelines:

| Clip Type | Typical Length | Notes |
|-----------|---------------|-------|
| **Locomotion loops** | 1-3 seconds | Shorter loops = more transition points |
| **Idle variations** | 2-5 seconds | Can be non-looping |
| **Actions/Events** | 0.5-2 seconds | Depends on action complexity |
| **Turns** | 0.3-1 second | Quick directional changes |

---

## Motion Capture vs Hand-Keyed Animation

### Motion Capture (Recommended)

**Advantages:**
- Natural weight and timing
- Realistic foot contact and momentum
- Subtle variations add richness
- High pose diversity

**Best practices:**
- Capture multiple takes of the same action
- Encourage performer variation
- Capture transitions between states
- Include "connective tissue" movements

**Common mocap cleanup:**
1. Remove foot sliding
2. Stabilize ground contact
3. Clean up finger/hand jitter
4. Ensure root motion continuity

### Hand-Keyed Animation

**Can work**, but requires different approach:

**Do:**
- Create natural timing with realistic acceleration
- Add subtle secondary motion
- Vary your animations (avoid copy-paste)
- Think in terms of "movement phrases" not "states"

**Don't:**
- Over-polish to perfection (variation is good)
- Create identical mirrored clips (slight variation helps)
- Force exact looping (MxM finds transitions)
- Use overly stylized timing (unless intentional)

---

## Root Motion Configuration

Root motion is **critical** for Motion Matching to work correctly.

### Unity Import Settings

For **each animation clip**, configure:

#### Animation Tab

```
Root Transform Rotation:
    ✓ Bake Into Pose
    Based Upon: Original
    Offset: 0

Root Transform Position (Y):
    ✓ Bake Into Pose
    Based Upon: Original
    Offset: 0

Root Transform Position (XZ):
    □ Bake Into Pose  (LEAVE UNCHECKED)
    Based Upon: Original
```

### Why These Settings?

**Bake Rotation:**
- Character's facing direction comes from root motion
- MxM controls rotation through trajectory

**Bake Y Position:**
- Vertical motion (jumping, crouching) in animation
- Prevents height drift

**Don't Bake XZ:**
- Horizontal movement drives character forward
- MxM reads this to calculate velocity
- Critical for matching trajectory

### Verifying Root Motion

**Quick test:**
1. Create empty scene
2. Add character with Animator
3. Play clip with root motion enabled
4. Character should move naturally across the floor
5. No sliding, skating, or floating

---

## Animation Content Planning

### Locomotion Set (Essential)

A complete locomotion set for Motion Matching:

#### Minimum Viable Set

| Animation | Variations | Priority |
|-----------|-----------|----------|
| **Idle** | 1-2 clips | Essential |
| **Walk Forward** | 1 clip | Essential |
| **Run Forward** | 1 clip | Essential |
| **Walk Strafe Left/Right** | 2 clips | High |
| **Run Strafe Left/Right** | 2 clips | High |
| **180° Turn** | 1-2 clips | Medium |
| **Walk to Stop** | 1-2 clips | Medium |
| **Idle to Walk** | 1-2 clips | Medium |

**Total: 10-15 clips for basic locomotion**

#### Professional Quality Set

Expand with:

- **8-directional movement** (forward, back, strafe L/R, diagonal)
- **Speed variations** (slow walk, jog, sprint)
- **Starts and stops** (multiple variations)
- **Turns** (45°, 90°, 135°, 180° both directions)
- **Transitional shuffles** (weight shifting, readjustments)
- **Idle variations** (breathing, looking around, fidgeting)

**Total: 40-80 clips for rich locomotion**

### Action/Event Animations

For gameplay actions (attacks, interactions, etc.):

#### Structure

Every action animation should have clear phases:

```
┌─────────────┬───────────┬──────────────┬────────────┐
│   WIND-UP   │  ACTION   │ FOLLOW-THRU  │  RECOVERY  │
├─────────────┼───────────┼──────────────┼────────────┤
│             │           │              │            │
│  Blend in   │ Mandatory │  Buffer      │ Can exit   │
│  from any   │ playback  │  zone        │ early      │
│  pose       │           │              │            │
└─────────────┴───────────┴──────────────┴────────────┘
```

**Wind-Up (20-30% of clip):**
- Anticipation phase
- System blends into this from locomotion
- Doesn't need to start from specific pose

**Action (30-40% of clip):**
- Core action (sword swing, button press, etc.)
- Plays to completion without interruption
- Most visually important section

**Follow-Through (20-30% of clip):**
- Energy dissipation
- Weight settles
- Brief buffer before exiting

**Recovery (10-20% of clip):**
- Return toward neutral/locomotion-ready pose
- Can be interrupted early
- Blends back to locomotion

#### Event Animation Best Practices

**Do:**
- Create natural wind-up that works from various poses
- Include recovery phase to return to locomotion
- Add variation (multiple attack animations, not just one)
- Consider directional variants (left/right hand, forward/backward)

**Don't:**
- Require exact starting pose
- End abruptly without recovery
- Create "moon walking" (feet sliding during action)
- Ignore momentum and weight

---

## Animation Variety and Coverage

### The Diversity Principle

**10 varied clips > 50 similar clips**

Motion Matching thrives on **diversity** in your animation set:

#### Velocity Coverage

Ensure you have animations at different speeds:

- **0 m/s**: Idle, standing
- **1-2 m/s**: Slow walk, cautious movement
- **3-4 m/s**: Normal walk
- **5-6 m/s**: Jog, fast walk
- **7-8 m/s**: Run
- **9+ m/s**: Sprint

**Why?** MxM matches velocity as part of trajectory. Gaps in speed coverage = poor matching.

#### Directional Coverage

Cover all movement directions:

```
      Forward (0°)
         ↑
         │
Left ←───┼───→ Right
  (270°) │  (90°)
         ↓
    Backward (180°)
    
Plus diagonals:
    45°, 135°, 225°, 315°
```

**Minimum:** Forward, back, left, right (4 directions)  
**Better:** + 4 diagonals (8 directions)  
**Best:** Full 16-direction coverage with speed variations

#### Pose Variation

Even for similar actions, add variation:

**Example: Idle Animations**
- Basic breathing idle
- Look left/right idle
- Shift weight idle
- Fidget idle
- Ready/alert idle

**Why?** Prevents repetitive-looking movement, adds life.

### Avoiding Gaps

**Common coverage gaps:**

1. **Transition zones**
   - Walk to run transitions
   - Turn transitions
   - Start/stop transitions
   
   **Solution:** Add specific transition clips or ensure locomotion loops cover these naturally

2. **Speed gaps**
   - Having walk (3 m/s) and run (7 m/s) but nothing in between
   
   **Solution:** Add jog or fast walk clips

3. **Directional gaps**
   - Forward/backward only, no strafing
   
   **Solution:** Add strafe animations or accept limitation

---

## Animation Cleanup and Optimization

### Foot Sliding Prevention

**Cause:** Root motion doesn't match foot contact

**Solutions:**

1. **Lock feet during contact**
   - Use foot IK constraints
   - Ensure planted foot is truly stationary

2. **Match root velocity to feet**
   - Calculate speed from foot movement
   - Adjust root motion to match

3. **In Unity:**
   - Use foot IK in MxM settings
   - Increase foot joint weights in skeleton config

### Loop Continuity

While perfect loops aren't required, **clean loops are better**:

**For looping clips:**

1. **Match first/last frames:**
   - Root position
   - Root rotation
   - Limb positions
   - Velocity

2. **Test the loop:**
   - Play 3+ cycles
   - Watch for pops or discontinuities
   - Check foot contact

**Red flags:**
- Visible pop at loop point
- Root position jump
- Velocity change
- Floating or ground penetration

### Motion Blur and Timing

**Fast movements:**
- Use proper motion blur in mocap
- Don't overcorrect natural blur
- Preserve impact timing

**Slow movements:**
- Ease in/out feels natural
- Avoid linear timing
- Add weight and momentum

---

## Tagging Strategy

Tags are **metadata** you assign to clips for runtime filtering.

### Recommended Tag Hierarchy

#### Movement Speed Tags
- `"Idle"`
- `"Walk"`
- `"Run"`
- `"Sprint"`

#### Movement Style Tags
- `"Combat"` - combat-ready stance
- `"Casual"` - relaxed movement
- `"Stealth"` - crouched/sneaking
- `"Injured"` - limping

#### Directional Tags
- `"Forward"`
- `"Backward"`
- `"Strafe"`

#### Contextual Tags
- `"Indoor"` - tight spaces
- `"Outdoor"` - open areas
- `"Stairs"` - vertical navigation
- `"Weapon"` - armed animations

### Tag Usage Example

```
Clip: "Run_Forward_Combat.fbx"
Tags: ["Run", "Forward", "Combat", "Weapon"]

Runtime:
    - Player enters combat → Enable "Combat" tag
    - MxM now only uses combat-ready runs
    - Exit combat → Disable "Combat" tag
    - Smooth transition to casual runs
```

**Best practices:**
- Use multiple tags per clip (3-5 typical)
- Create tag hierarchy (broad to specific)
- Plan tags before animation starts
- Document tag meanings for team

---

## Quality Checklist

Before exporting animations to Unity:

### Pre-Export Checklist

- [ ] **Skeleton matches reference rig**
- [ ] **Root bone properly defined**
- [ ] **No unnecessary bones** (keep hierarchy clean)
- [ ] **Foot IK solved** (no sliding)
- [ ] **Loops are clean** (if looping)
- [ ] **Root motion velocity is natural**
- [ ] **Proper frame rate** (30 or 60 FPS)
- [ ] **Named descriptively** (RunForward_Combat_01.fbx)

### Post-Import Checklist (Unity)

- [ ] **Rig type set** (Humanoid/Generic)
- [ ] **Root transform settings correct**
- [ ] **Animation compresses appropriately**
- [ ] **Clip plays correctly in Animator**
- [ ] **Root motion works** (character moves naturally)
- [ ] **No warnings in console**

### Post-Processing Checklist (MxM)

- [ ] **Clip processes without errors** (no red clips)
- [ ] **Pose count reasonable** (check total poses)
- [ ] **Tags applied correctly**
- [ ] **Blends look natural** (test in play mode)
- [ ] **No popping or artifacts**

---

## Common Animation Pitfalls

### 1. "Perfect" Mirrored Clips

**Problem:**
```
Walk_Left.fbx  ← Exact mirror of Walk_Right
Walk_Right.fbx
```

**Issue:** Identical mirrored clips reduce diversity.

**Solution:** Add subtle variation even in mirrors, or use asymmetric movement naturally.

### 2. T-Pose Contamination

**Problem:** Clips include T-pose frames at start/end.

**Issue:** MxM includes these in database, can match to T-pose!

**Solution:** Clean clips, remove setup frames before export.

### 3. Inconsistent Scale

**Problem:** Character scale differs between clips.

**Issue:** Root motion calculations are wrong.

**Solution:** Lock character scale at export (usually 1.0), verify in Unity.

### 4. Over-Polished Loops

**Problem:** Spent days perfecting a single loop.

**Issue:** Motion Matching benefits more from variety than perfection.

**Solution:** Time is better spent creating 3 "good enough" clips than 1 perfect clip.

### 5. Missing Connective Tissue

**Problem:** Only have clean loops: Idle, Walk, Run.

**Issue:** No natural transitions between states.

**Solution:** Capture transitions: Idle→Walk, Walk→Run, turning, stopping.

### 6. Unrealistic Foot Speed

**Problem:** Feet move too slow/fast for body velocity.

**Issue:** Creates sliding even with root motion.

**Solution:** Match foot speed to root speed exactly.

---

## Advanced Techniques

### Procedural Variation

**Technique:** Slightly modify duplicate clips procedurally.

**Methods:**
- Time offset (start at different frames)
- Speed variation (±5% playback speed)
- Noise injection (subtle joint perturbation)

**Use case:** Create more variation from limited mocap.

### Stitching and Splicing

**Technique:** Cut and combine sections of different clips.

**Example:**
```
Attack_WindUp_01 + Attack_Action_02 + Attack_Recovery_03
= New unique attack variant
```

**Use case:** Multiply action variations from existing library.

### Layered Animation

**Technique:** Combine locomotion with layered upper body.

**Setup:**
- Full body locomotion in MxM
- Upper body layer for actions (shooting, gesturing)
- Use Unity's Animation Layers

**Use case:** Shoot while moving, gesture while walking.

---

## Performance Considerations for Animators

Your animation choices affect runtime performance:

### Clip Length Impact

**Longer clips:**
- More poses in database
- Slower pre-processing
- Larger memory footprint
- Better coverage (can be worth it)

**Recommendation:** 2-5 seconds per clip is sweet spot.

### Pose Density

Controlled by **Pose Interval** setting (e.g., 0.1s):

- Lower interval (0.05s) = More poses = Smoother but slower
- Higher interval (0.2s) = Fewer poses = Faster but less smooth

**Animator consideration:** Dense, varied clips benefit from low intervals. Sparse clips can use higher intervals.

### Joint Count

More joints in skeleton config = slower matching.

**Recommendation:**
- Start with 4-6 critical joints (hips, feet, hands)
- Add more only if quality demands it
- Prioritize joints that matter most (feet for locomotion)

---

## Workflow Recommendations

### Iterative Approach

1. **Phase 1: Proof of Concept**
   - 10-15 basic locomotion clips
   - Quick pre-process test
   - Verify movement works

2. **Phase 2: Expand Coverage**
   - Add directional variants
   - Add speed variations
   - Test for gaps

3. **Phase 3: Polish and Variation**
   - Add idle variations
   - Add transition clips
   - Refine tagging

4. **Phase 4: Actions and Events**
   - Add gameplay actions
   - Configure event system
   - Integrate with game systems

### Team Communication

**For animation teams:**

**Document:**
- Naming conventions
- Tag definitions
- Required coverage (directions, speeds)
- Clip specifications (length, FPS, format)

**Share:**
- Coverage matrix (spreadsheet of what's needed)
- Quality examples (reference clips)
- Performance budgets (max clip count, sizes)

**Iterate:**
- Regular playtest with real gameplay
- Animator sees in-game, not just DCC tool
- Quick feedback loop

---

## Tools and Resources

### Recommended DCC Tools

**Motion Capture:**
- MotionBuilder - Industry standard
- Blender - Free alternative
- Maya - Full animation pipeline

**Hand-Keyed Animation:**
- Maya
- Blender
- 3ds Max

### Unity Tools

**Built-in:**
- Animation window - Clip preview
- Animator - Testing clips
- Timeline - Sequencing

**Third-Party:**
- Final IK - Foot IK solutions
- Animation Baker - Root motion tools

### Analysis Tools

**In Unity:**
- Check root motion velocity (Animator window)
- Preview in scene view
- Use profiler to check pose counts

---

## Summary

### Key Takeaways

1. **Variety > Perfection** - Diverse movement is more valuable than perfect loops
2. **Root motion is critical** - Configure import settings correctly
3. **Coverage matters** - Fill speed and directional gaps
4. **Natural movement wins** - Realistic timing and weight transfer
5. **Tag strategically** - Plan metadata for runtime flexibility
6. **Iterate quickly** - Test early, test often

### Next Steps

- **[Quick Start Guide](quick-start.md)** - Implement animations in MxM
- **[Technical Reference](technical-reference.md)** - Understand the system
- **[FAQ](faq.md)** - Troubleshoot common issues

---

!!! success "You're Ready to Animate!"
    Armed with this guide, you can create animation content that takes full advantage of Motion Matching. Focus on natural, varied movement and let the system handle the transitions!
