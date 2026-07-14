<p align="center">
  <img src="Runtime/Images/HonamiAnimationSystemLogoCropped.png" alt="Honami Animation System" width="480"/>
</p>

<p align="center">
  <a href="https://docs.unity3d.com/6000.0/Documentation/Manual/WhatsNewUnity6.html">
    <img src="https://img.shields.io/badge/Unity-6000.0%2B-26c6da?style=for-the-badge&logo=unity&logoColor=white" alt="Unity Version"/>
  </a>
  <img src="https://img.shields.io/badge/status-beta-ff7043?style=for-the-badge" alt="Beta"/>
  <img src="https://img.shields.io/badge/license-MIT-66bb6a?style=for-the-badge" alt="License"/>
</p>

<p align="center">
  <b>Animate anything in Unity the way you edit video - visually, in the UI, with almost no code.</b><br/>
  A UI-first animation system for Unity 6 · Node graph · Timeline editors · Rig-agnostic · Zero-allocation runtime
</p>

## Overview

**Honami** replaces Unity's Animator Controller with a set of visual editors. You author states, transitions, blends, masks, events, clips and sequences in dedicated tools that feel like a video editor - drop things on tracks, scrub, preview, tweak. Under the hood it builds and drives its own `PlayableGraph` (the standard Unity `Animator` stays on the object with its Controller slot empty), with an allocation-free evaluation loop.

It drives **anything with an Animator** - characters and creatures, but also weapons, vehicles, machines, doors, cameras and props. Characters are the hard case it's built for; a door or a turret is trivial next to that.

In practice the animator does the graph and the programmer writes gameplay code. That's the whole division of labour.

## Table of contents

- [Minimal code, by design](#minimal-code-by-design)
- [The Honami Timeline](#the-honami-timeline)
- [The node graph](#the-node-graph)
- [Sub-Nodes](#sub-nodes)
- [Transitions](#transitions)
- [Parameters](#parameters)
- [Weighted, per-bone masks](#weighted-per-bone-masks)
- [The event system](#the-event-system)
- [Reuse: override controllers & inheritance](#reuse-override-controllers--inheritance)
- [The Linked Brain](#the-linked-brain)
- [Built-in rig system](#built-in-rig-system)
- [Performance](#performance)
- [Scripting API](#scripting-api)
- [Honami vs. the built-in Animator](#honami-vs-the-built-in-animator)
- [Honest scope](#honest-scope)
- [Migrating from Mecanim](#migrating-from-mecanim)
- [Roadmap](#roadmap)

## Minimal code, by design

Almost everything lives in the editors, no scripting required:

- **States, transitions and layers** - drawn in the node graph.
- **Blend trees** - built in a visual editor with live per-motion weights and a skeleton preview.
- **Masks** - painted per bone with 0-1 weights in the mask editor.
- **Events** - wired straight to `UnityEvent`s in the inspector.
- **Parameter changes** - states can set parameters on enter/exit, right in the graph.
- **Clips & sequences** - keyed and arranged in the Timeline.

Code shows up in one place: gameplay. You push values in, and you handle a few callbacks:

```csharp
using HonamiAnimationSystem.Runtime.Core;
using UnityEngine;

public sealed class PlayerAnimation : MonoBehaviour
{
    private HonamiAnimator _honami;
    private static readonly int Speed = HonamiAnimator.StringToHash("Speed");

    private void Awake() => TryGetComponent(out _honami);

    private void OnEnable()  => _honami.OnStateFinished += OnFinished;
    private void OnDisable() => _honami.OnStateFinished -= OnFinished;

    private void Update()
    {
        // Everything above was authored in the UI. This is the code you actually write:
        _honami.SetFloat(Speed, _velocity.magnitude);           // drive a blend tree
        if (_jumpPressed) _honami.PlayState("Jump");            // trigger a state
        if (_firePressed) _honami.PlayState("Fire", layer: 1);  // upper-body layer
    }

    private void OnFinished(string state)
    {
        if (state == "Reload") _weapon.FillMagazine();
    }
}
```

Most integrations aren't much bigger than this. Cache the animator, push a few parameters, handle a couple of callbacks. You never end up re-implementing the state machine in C#.

## The Honami Timeline

Everything is authored the way you'd edit video: a **tabbed timeline window** with a frame ruler, tracks, a scrub head and a transport bar. Open as many tabs as you want - a state here, a clip there, a whole sequence beside them - and switch between them like browser tabs. Each tab keeps its own selection and view. It does three things:

### State mode - author and preview a state
Drop clips on the animation track, place **Local** and **Global** event markers exactly where they should fire, and tune Loop, Speed and Reversed in the properties panel. The track adapts to the node type - a single clip, a random set, a sequencer's segments, or a blend tree's motions.

Press **Preview** and it plays right in the Scene view, with no Play Mode and no compile wait. This isn't a rough clip sample: avatar masks (with their per-bone weights), blend trees, mirroring, constraints and bone replacements are all applied, so it matches what runs in-game. Turn on preview for several states across different layers and they composite in the correct order, lower layers first.

### Clip mode - a keyframe editor inside Honami
Need to fix a clip? Don't leave for Unity's Animation window. Clip mode is a per-bone dopesheet: scrub to a frame, move a bone in the Scene, and Honami **records the keyframe straight into the `AnimationClip`**. Curves, keys and per-bone rotation/position tracks all live in the same timeline UI. Clips embedded in an FBX open read-only, so a source asset is never corrupted by accident.

### Sequencer mode (Director) - scripted sequences
*In development - arrange clips and events on tracks, with runtime playback via `HonamiDirector`. Full cinematic tooling is coming.*

## The node graph

You draw states, transitions and layers instead of writing them. Behaviour is split across typed nodes rather than one giant flat state machine, which is what keeps the graph readable as it grows:

- **Animation Node** - plays a single clip, with a trimmable start/end range.
- **Blend Tree Node** - the smooth locomotion blend: flows continuously between clips like **idle → walk → run** along a float such as speed, with no visible transition. **Standard** mode speed-syncs the clips so footfalls stay in phase; **Simple** plays each at its own speed. Damped, so it eases instead of snapping.
- **Random Node** - picks one weighted clip on entry for idle/attack variety; can even sync its pick across linked animators.
- **Sequencer Node** - stitches several clips onto one state's timeline (wind-up → hit → recover) without extra transitions.
- **Event Node** - a state with no animation at all: a pure logic beat that fires event markers, sets parameters and runs Sub-Nodes for a set duration. Chain a few with exit-time transitions for scripted sequences, timers and decision points - all authored in the UI, no code.
- **Portal Nodes** - named entrance/exit pairs that "teleport" the flow across a big graph without long messy wires; virtual, zero runtime cost, with optional source filtering.
- **Any State / Repeater** - global transitions reachable from anywhere (deaths, stuns); the Repeater force-restarts its target for mash-friendly hit reactions with a cooldown and repeat cap. Combo Mode turns it into a melee combo driver: fires go through the outgoing transitions in order, and presses landing before the current attack's cancel window are buffered instead of interrupting it.

## Sub-Nodes

**Sub-Nodes** attach logic *inside* a state so the main graph stays about high-level flow. They are plain `ScriptableObject` assets (`HonamiSubNodeBase`) with an `OnEnter` / `UpdateRuntime` / `OnExit` lifecycle, and they only run while their state is active. Stack as many as you want on a state. Built-ins include:

- **Mask Switcher** - swap or blend the avatar mask from parameter conditions (first matching rule wins), e.g. dual-wielding or directional hit masks.
- **Replacer** - apply bone replacements for the duration of the state.
- **Smooth Bone Weight** - ease per-bone mask weights in and out instead of snapping.
- **Jitter Speed / Jitter Weight** - organic per-frame variation of playback speed or state weight.
- **Log** - a debugging probe that prints on enter/update/exit.

Writing your own is just subclassing `HonamiSubNodeBase` and overriding `UpdateRuntime` (plus optional `OnEnter`/`OnExit`).

## Transitions

Transitions are more than a duration and a condition. Each one has a **type**, a **priority**, interruption rules, and can even write parameters when it fires:

- **Standard** - classic linear or curve-driven cross-fade.
- **Victim** - one side of the blend is the "victim": its speed can be multiplied and its weight can drop on a custom or quadratic curve. Perfect for snappy attack cancels that don't pop.
- **Smart** - adaptive duration that scales with the time left in the current state (short remainder, short blend).

Add **priority** (higher can interrupt lower even when *Can Interrupt* is off), **Sacrifice Existing** (instantly kill other blends on the layer), **Has Exit Time**, **Can Transition To Self**, a **Destination Start Time** offset, and per-transition **parameter assignments**. Conditions combine with AND (draw a second transition for OR) and support `If` / `IfNot` / `Greater` / `Less` / `Equals` / `NotEqual`. Triggers are only consumed when a transition actually fires.

## Parameters

Parameters are the variables of your state machine - transitions read them, blend trees sample them, states can write them back. Types: **Float**, **Int**, **Bool**, **Trigger**, and **Random** (a float that reseeds itself 0-1 every tick, for code-free idle variety). Storage is backed by native arrays for fast, allocation-free access, and every setter/getter has a `string` and an `int`-hash overload - cache the hash once and use it in hot paths. States (and transitions) can also assign parameters on enter/exit from the graph, so simple bookkeeping like an `InCombat` flag needs no code at all.

## Weighted, per-bone masks

Unity Avatar Masks are on/off - a bone is in the mask or it isn't. Honami masks carry a **weight from 0 to 1 for every bone**, painted in a dedicated mask editor. An upper-body layer can bleed into the spine at 40%, a hit reaction can fade across the torso instead of snapping on, and a partial mask can soften the seam between layers. Masks are bone-path based (an `excludeUnlisted` toggle chooses whether unlisted bones pass through or are blocked), so they work on any skeleton, with no Humanoid Avatar and none of its 15-bone minimum.

## The event system

Unity bakes Animation Events into the AnimationClip, which couples every clip to a specific method on a specific GameObject and means a shared clip can only ever fire the same event. Honami puts event markers on the **state** instead. The same clip fires different events in different states, and your `.anim` files stay clean and reusable. Two independent channels:

- **Local Event** - invokes `UnityEvent` actions on a `HonamiLocalEventReceiver` next to the animator. Wire it up in the inspector; no code required.
- **Global Event** - dispatches to reusable `HonamiGlobalEvent` components you subclass in C#, living on **any** GameObject in the scene (e.g. a `FootstepEvent` shared across every character, or a door the animation opens remotely).

Markers behave sensibly: they re-fire each loop, fire in reverse on reversed states, don't fire on paused states or layers, and can be cancelled when a state is skipped. Separately, `HonamiAnimator` also raises C# events - `OnStateEntered`, `OnStateFinished`, and `OnStateExited` (with a full `HonamiStateExitInfo` telling you exactly why a state ended and what replaced it).

## Reuse: override controllers & inheritance

Unity's `AnimatorOverrideController` mostly swaps clips. Honami's **Override Controllers** inherit an entire controller - its layers, parameters and states appear as virtual inherited states - and then let you override just what differs: replace a node, retune a transition, or add a local layer. Character variants (a heavy vs. a light enemy, a rifle vs. a pistol loadout) reuse one authored graph instead of a copy-pasted forest of controllers.

## The Linked Brain

Coordinate many animators from one place. The **Linked Brain** broadcasts a state change, a parameter, or an action to whole groups at once, filtered by target mode - **AllLinked**, **ChildrenOnly**, or **ByTag** (tags are `HonamiTagID` assets, so renames never break references). Author the same broadcasts visually in the Brain graph.

For decentralized, world-space coordination there's also the static `HonamiLinkedAction` - animators whose states carry an **ActionID** register themselves automatically, and a single call fans the reaction out with real spatial targeting:

- `PlayGlobal` - everyone registered (optional cap).
- `PlayByTag` / `PlayByLayer` / `PlayInHierarchy` - filtered groups.
- `PlayNearby` - everyone within a radius.
- `PlayClosest` - the N nearest.
- `PlayPropagated` - a shockwave: reactions ripple outward, delayed by distance ÷ speed.

So you can drive a whole squad's reactions without looping over animators in gameplay code.

## Built-in rig system

Standard Unity rigging needs the external **Animation Rigging** package - a separate dependency, decoupled from the Animator, with its own GameObject-based setup. Honami includes a rig-agnostic constraint pipeline out of the box. All constraints run as a **final correction pass after sampling**, so authored animation stays in charge while procedural adjustments handle contacts, aiming, secondary motion and physics.

- **Pose Constraint** - injects static or dynamic poses into bone hierarchies for actions like a sliding stance, crouch offsets, or holding a weapon, in local or world space with full weight control.
- **LookAt Constraint** - rotates a bone or a chain toward a target with custom aim/up axes and per-bone weights: heads, eyes, turrets, barrels, creature sensors, long necks. Additive mode layers on top of authored animation without overwriting it.
- **Point Constraint** - locks a transform to another's position/rotation with authored offsets, keeping sockets, armor plates and held objects aligned without turning them into Humanoid bones.
- **Pivot Fixer** - solves the "wrong imported pivot" problem for weapon grips, hinges and attachment points by choosing a different effective anchor than the FBX provides.
- **Pseudo-Physics** - springy inertia and secondary motion for hair, tails, cloth, antennae, cables and weapon sway. It's cheap: a spring approximation, no Rigidbody chains involved.
- **Foot IK, Anti-Jitter, Scale Fixer, Space Switcher and Bone Replacer** round out the toolkit for planting feet, cleaning noisy motion, fixing scale, switching bone spaces and swapping skeleton parts at runtime.

No external packages, no humanoid limitations, and no per-object rigging setup overhead.

## Performance

The core evaluation loop produces **zero GC allocations per frame**. Parameters live in native arrays with integer-hash lookup, and every API method has a hash overload so hot paths stay allocation-free. Distant characters can evaluate at a fixed 15 or 30 FPS with optional interpolation via the per-animator **FPS Cap** - a free animation LOD - and standard Unity culling modes are forwarded to the Animator. Off-screen actors can be paused for the cost of one bool check per tick, or frozen in place entirely.

## Scripting API

The full runtime API lives on `HonamiAnimator` in `HonamiAnimationSystem.Runtime.Core`. Every state/parameter method has name, `int`-hash and (for states) `...ByGuid` overloads. Highlights:

- **Playback** - `PlayState`, `PlayStateByGuidWithPriority`, `IsStateActive`, `GetStateProgress`, `GetTransitionWeight`.
- **Skipping** - `TrySkipState` / `TryAutoSkipState` conditionally interrupt a specific state, reusing the transition authored in the graph.
- **Pausing / stopping** - `Pause`, `PauseLayer`, `Stop`, `StopAll`, `StopAndKeepPose`, `ResetToDefault`.
- **Parameters** - `SetFloat/Int/Bool/Trigger` and getters, plus `ResetTrigger`.
- **Events** - `OnStateEntered`, `OnStateFinished`, `OnStateExited`.
- **Controllers** - `SetController` with an optional cross-fade between old and new graphs.
- **Mirroring & time** - `SetGlobalMirror`, `TimeScale`, `FpsCap`, and manual `Tick(dt)` for deterministic or networked updates.

## Honami vs. the built-in Animator

Only the areas where the two genuinely diverge - where Mecanim already does the job well, it isn't listed.

| Area | Built-in Animator | Honami |
|---|---|---|
| **Authoring** | ⚠️ Graph only; previewing behavior means entering Play Mode. | ✅ Timeline editors for states, clips and sequences with faithful in-Scene preview. |
| **Underlying engine** | ⚠️ Closed native Mecanim loop (black box, non-extendable). | ✅ Your own C# evaluation stack over a `PlayableGraph` you can inspect. |
| **Logic style** | ⚠️ Flat state machines, prone to transition spaghetti. | ✅ Modular graphs with typed, custom node types and Sub-Nodes. |
| **Transitions** | ⚠️ Fixed duration & exit time. | ✅ Runtime-parametric: priority interrupts, victim weighting, adaptive duration. |
| **Reuse** | ❌ Override controllers mostly swap clips. | ✅ Controller & layer inheritance with virtual states and parameter propagation. |
| **State logic** | ⚠️ `StateMachineBehaviour`. | ✅ Sub-Nodes (ScriptableObject assets) with `OnEnter`/`Update`/`OnExit`. |
| **Animation events** | ⚠️ Baked into the clip, coupled to code. | ✅ Authored on the state; clips stay clean; Local + Global channels. |
| **Masks** | ⚠️ Binary - a bone is in or out. | ✅ **Weighted** per-bone masks (0-1 per bone). |
| **Rigging** | ⚠️ External Animation Rigging package, complex setup. | ✅ Built-in rig-agnostic constraint pass, no extra package. |
| **Many characters** | ❌ No cross-animator coordination. | ✅ Linked Brain broadcast by tag, radius, wave or closest-N. |
| **Debugging** | ❌ Basic active-state progress bar. | ✅ Live node highlighting, weights and transition values. |
| **Blend trees** | ✅ 1D and 2D blend trees. | ⚠️ 1D only (2D on the roadmap). |
| **Retargeting** | ✅ Humanoid muscle-space retargeting across skeletons. | ⚠️ No runtime retargeting - binds by transform path. The Humanoid Baker tool retargets Humanoid clips onto your skeleton offline and bakes them to Generic. |

## Honest scope

Honami drives any Animator-based object, and it shines on fast, code-driven action games and on complex or exotic rigs. It is **not** a drop-in Mecanim replacement for every project, though: there's **no runtime Humanoid retargeting** (marketplace Humanoid clips are supported via the offline Humanoid Baker tool), blend trees are **1D only** (no 2D blend space), and overlays use masked layers (additive / aim-offset is on the roadmap). If your project leans on live cross-skeleton retargeting or 2D blendspace locomotion, Unity's built-in Animator is still the better fit.

## Migrating from Mecanim

Migration is incremental, not a rewrite. A Honami character and a Mecanim character coexist in the same scene without conflict, so you port one prefab at a time while the rest of the game keeps running - and you can stop halfway and ship like that.

### What actually changes on a character

Less than you'd think. The GameObject keeps its standard Unity **Animator** component exactly as it is - the only edit is emptying the **Controller slot** - and gains a **HonamiAnimator** component pointing at a `HonamiController` asset. That's the whole structural change.

Your rig setup, whatever it is, stays as-is. A Humanoid character keeps its Humanoid import and its **Avatar assigned** in the Animator; a Generic character keeps its Generic setup; a prop or weapon with no avatar at all needs nothing extra. Honami never asks you to convert a model's rig type in either direction - switching an established model between Humanoid and Generic is exactly the kind of change that breaks avatar references, ragdolls and prefab bindings, and it is not part of the migration. Existing clips survive too: Generic clips play as-is, Humanoid clips go through the baker described below.

### The migration tools

Everything lives under **Window ▸ Honami ▸ Tools**:

**Animator Converter** - right-click any `AnimatorController` and choose *Convert to Honami Controller* (or open the window and pick one). It generates a `HonamiController` with your parameters and their defaults, layers with weights, every state (speed, loop, graph positions preserved), sub-state machines (each gets its own Any State and Exit), and transitions with their conditions, exit times and interruption settings. Two things don't auto-port and the converter says so: **2D blend trees** are flattened into a 1D motion list (re-author them as 1D trees, states or layers), and **`StateMachineBehaviour`s** have no direct equivalent - recreate them as Sub-Nodes, which is usually less code.

**Script API Converter** - scans a folder for C# scripts that use the Animator API, lists the affected files, and lets you convert the ones you tick. It finds your `Animator` fields and variables and rewrites them in place: type references (`Animator`, `GetComponent<Animator>`, `typeof(Animator)`) become `HonamiAnimator`, and `Play` / `CrossFade` / `CrossFadeInFixedTime` become `PlayState`. Parameter calls like `SetFloat`, `SetBool` and `SetTrigger` are left alone on purpose - `HonamiAnimator` uses the same method names, so they compile as-is. Converted files also get `using HonamiAnimationSystem.Runtime.Core;` inserted automatically. It's a source-code rewrite, so **commit first** and review the diff after.

**Humanoid Baker** - converts your Humanoid clip library (including marketplace packs and Mixamo) into Generic clips authored for your skeleton. One offline pass per skeleton; the character itself stays Humanoid. See [Honest scope](#honest-scope) for why Honami plays transform-path clips instead of retargeting at runtime.

**Project Validator** - after the move, finds missing references and broken transitions across your Honami assets.

### Step by step, per situation

**A Humanoid character coming off Mecanim:**

1. Convert its `AnimatorController` with the Animator Converter; re-author 2D trees and behaviours.
2. Add `HonamiAnimator` to the prefab, assign the new `HonamiController`, clear the Animator's Controller slot - Avatar stays.
3. Bake the Humanoid clips with the Humanoid Baker and drop the baked clips into the converted states.
4. Run the Script API Converter over that character's scripts.
5. Check with the Project Validator, then compare behaviour side by side - live node highlighting and transition weights make differences obvious.

**A Generic / custom-skeleton character** (creatures, weapons, props): same as above minus the baking - Generic clips already bind by transform path, so steps 1, 2, 4, 5 and you're done.

**A whole project, gradually:** migrate prefab by prefab in whatever order hurts least - Honami and Mecanim characters run side by side indefinitely. Shared scripts that touch many characters are the one thing to plan around: convert them last, or keep a thin wrapper during the transition. Delete the old `AnimatorController`s only after the ported characters have proven themselves in play.

## Roadmap

Honami is in active beta. In progress and planned:

- **Cinematic Director** - clip-to-clip blending, camera tracks and full cutscene tooling on top of the current sequencer.
- **2D blend trees** - a full 2D blend space alongside the current 1D trees.
- **Additive** - additive layers for recoil, lean and look/aim overlays.
- **Inertialization** - velocity-preserving transition blending.

## Used in production

Honami powers all animation in **[Daisen](https://store.steampowered.com/app/3702380/Daisen/)** - a fast-paced action game built by LOYAL Studio.

## Screenshots

<p align="center">
  <img src="Screenshots/1.png" width="800" />
  <br/><br/>
  <img src="Screenshots/2.png" width="800" />
  <br/><br/>
  <img src="Screenshots/3.png" width="800" />
  <br/><br/>
  <img src="Screenshots/4.png" width="800" />
</p>

## Installation

### Via Unity Package Manager (Git URL) *(recommended)*

1. Open **Window → Package Manager**.
2. Click the **＋** button → **Add package from git URL…**
3. Paste:
   ```
   https://github.com/loyal-studio/Honami-Animation-System.git
   ```
4. Click **Add** and wait for import to complete.
5. The **Welcome to Honami** window will open automatically on first launch.

## Documentation

Open **Window → Honami → Documentation** directly inside Unity. The built-in docs cover graph authoring, transitions, blend trees, avatars and masks, IK and constraints, the Linked Brain system, the workflow tools (including the Humanoid Baker), the full scripting API and the optimization guide - with live code examples you can copy with one click.

## Contributing

Contributions, bug reports and feature requests are welcome!

## License

This project is licensed under the **MIT License** - see [LICENSE](LICENSE) for details.

<p align="center">
  Made with Love by <a href="https://github.com/loyal-studio"><b>LOYAL Studio</b></a>
</p>
