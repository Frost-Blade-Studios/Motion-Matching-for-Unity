# Getting Started

This guide will walk you through installing Motion Matching for Unity (MxM) and setting up your first motion matching character.

---

## Requirements

### Unity Version

- **Minimum**: Unity 2020.3 LTS
- **Recommended**: Unity 2022.3 LTS or newer
- **Tested**: Unity 2022.3+, Unity 6

!!! warning "Unity 2022+ Compatibility"
    This community fork has been updated for Unity 2022+ compatibility. If you encounter issues with newer Unity versions, please [report them on GitHub](https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity/issues).

### Dependencies

MxM integrates with Unity's built-in systems:

- **Unity Animation System** (Mecanim)
- **Unity Physics** (for trajectory prediction)
- **.NET Standard 2.1** or newer

### Project Requirements

- **Animation clips** - Humanoid or generic animated characters
- **Recommended**: Motion capture data for best results
- **Storage**: Motion databases can be large (100MB+ for full character sets)

---

## Installation

### Option 1: Install via Git URL (Recommended)

1. Open your Unity project
2. Go to **Window > Package Manager**
3. Click the **+** button in the top-left corner
4. Select **Add package from git URL**
5. Enter the following URL:

```
https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity.git
```

6. Click **Add**
7. Unity will download and install MxM

!!! tip "Keep Updated"
    To update to the latest version, simply remove and re-add the package using the same Git URL.

### Option 2: Manual Installation

1. Download or clone this repository:

```bash
git clone https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity.git
```

2. Copy the `MxM` folder to your Unity project's `Assets` directory

3. Unity will automatically import the package

### Option 3: Unity Package Manager (Local)

1. Clone this repository to your local machine
2. In Unity, open **Window > Package Manager**
3. Click **+** > **Add package from disk**
4. Navigate to the cloned repository
5. Select the `package.json` file (if available)

---

## Verifying Installation

After installation, verify MxM is working:

1. **Check the MxM menu**: You should see **MxM** in the Unity menu bar
2. **Check for components**: Search for "MxM" in the Add Component menu
3. **Open example scenes**: If included, navigate to `MxM/Examples` to see demo scenes

---

## Project Setup

### Step 1: Prepare Your Character

MxM works with humanoid or generic animated characters. You'll need:

1. **Character model** with a rig
2. **Animation clips** (minimum 10-20 clips recommended)
3. **Animator Controller** (can be basic or empty)

!!! example "Animation Requirements"
    For a basic locomotion setup, you'll need:
    
    - Idle animations (1-2 clips)
    - Walk cycle (multiple directions if possible)
    - Run cycle (multiple directions if possible)
    - Turn animations (optional but recommended)
    - Start/stop animations (optional but recommended)

### Step 2: Create an MxM Animator

1. **Select your character** in the Hierarchy
2. **Add the MxMAnimator component**:
   - Click **Add Component**
   - Search for "MxMAnimator"
   - Click to add

3. **Configure the component**:
   - Assign your **Animator** reference
   - Set the **Update Mode** (typically "Normal")
   - Configure **Trajectory** settings

### Step 3: Create an Anim Data Asset

The **Anim Data** asset is the heart of MxM - it stores your processed animation database.

1. **Create the asset**:
   - Right-click in Project window
   - **Create > MxM > Anim Data**
   - Name it (e.g., "CharacterAnimData")

2. **Add animation clips**:
   - Select your Anim Data asset
   - In the Inspector, click **"Add Clips"**
   - Select all your animation clips

3. **Configure poses**:
   - Set **Pose Interval** (default: 0.1s works well)
   - Configure **Trajectory Points** for prediction
   - Set **Joint Weights** (hands, feet usually higher priority)

4. **Process the data**:
   - Click **"Pre-Process"**
   - Wait for the process to complete (can take several minutes)
   - A progress bar will show status

!!! warning "Processing Time"
    Pre-processing can take 5-30 minutes depending on:
    
    - Number of animation clips
    - Length of animations
    - Pose interval settings
    - Your computer's performance

### Step 4: Assign Anim Data to MxMAnimator

1. Select your character in the Hierarchy
2. Find the **MxMAnimator** component
3. Drag your processed **Anim Data** asset to the **Anim Data** field

### Step 5: Set Up Input & Trajectory

MxM needs to know where your character wants to go:

1. **Create an Input Profile**:
   - Right-click in Project window
   - **Create > MxM > Input Profile**
   - Configure input bindings (WASD, gamepad, etc.)

2. **Assign to MxMAnimator**:
   - Drag the Input Profile to the MxMAnimator
   - Configure **Trajectory Generator** settings

3. **Test movement**:
   - Enter Play mode
   - Use input to move your character
   - MxM should select appropriate animations automatically

---

## Unity 2022+ Migration Notes

If you're upgrading a project from an older Unity version:

### Breaking Changes

- **API Updates**: Some deprecated Unity APIs have been updated
- **Serialization**: Re-process your Anim Data assets after upgrading
- **Physics**: Trajectory prediction may need recalibration

### Recommended Steps

1. **Backup your project** before upgrading Unity
2. **Upgrade Unity** to 2022.3 LTS or newer
3. **Re-import MxM** from this fork
4. **Re-process all Anim Data** assets
5. **Test thoroughly** in Play mode
6. **Recalibrate** trajectory and timing settings if needed

---

## Troubleshooting

### "MxMAnimator not found"

- Ensure the package is installed correctly
- Check for compilation errors in the Console
- Try reimporting the package

### "No valid pose found"

- Check that Anim Data is assigned to MxMAnimator
- Verify Anim Data has been pre-processed
- Ensure animation clips are compatible with your character rig

### Performance Issues

- Reduce **Pose Interval** (larger intervals = fewer poses = faster)
- Decrease **Trajectory Points**
- Lower **Search Budget** in MxMAnimator settings
- Use **LOD** system for distant characters

### Animations Look Jerky

- Decrease **Pose Interval** (more poses = smoother)
- Increase **Blend Time** in MxMAnimator
- Check that **Update Mode** is set appropriately
- Verify **Frame Rate** of source animations

---

## Next Steps

Now that MxM is installed and set up:

1. **Explore the examples** (if included in the package)
2. **Read the [Technical Reference](technical-reference.md)** for deep understanding
3. **Check the [Original PDFs](resources.md)** for detailed workflows
4. **Experiment** with different settings and animation sets
5. **Join the community** and share your results!

---

## Additional Resources

- **GitHub Repository**: [Frost-Blade-Studios/Motion-Matching-for-Unity](https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity)
- **Report Issues**: [GitHub Issues](https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity/issues)
- **Community Discussions**: [GitHub Discussions](https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity/discussions)

---

!!! success "Ready to Go!"
    Your MxM setup is complete! Try creating a simple character with basic locomotion animations to see motion matching in action.
