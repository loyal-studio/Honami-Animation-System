using System.Linq;
using HonamiAnimationSystem.Editor;
using HonamiAnimationSystem.Runtime.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UBlendTree = UnityEditor.Animations.BlendTree;

namespace HonamiAnimationSystem.Tests.Editor.Converter
{
    public sealed class ConverterBlendTreeTests
    {
        private string Folder;

        private AnimatorToHonamiConverterWindow window;
        private AnimationClip[] clips;

        [SetUp]
        public void SetUp()
        {
            string folderName = "__HonamiConverterTests_" + System.Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folderName);
            Folder = "Assets/" + folderName;

            window = ScriptableObject.CreateInstance<AnimatorToHonamiConverterWindow>();
            clips = new AnimationClip[9];
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = new AnimationClip { name = "Clip" + i };
                clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
                AssetDatabase.CreateAsset(clip, $"{Folder}/Clip{i}.anim");
                clips[i] = clip;
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (window != null) Object.DestroyImmediate(window);
            AssetDatabase.DeleteAsset(Folder);
        }

        private UBlendTree CreateTree(BlendTreeType type, out AnimatorController controller)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath($"{Folder}/Source.controller");
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.CreateBlendTreeInController("Tree", out UBlendTree tree);
            tree.blendType = type;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveY";
            return tree;
        }

        private static void Tweak(UBlendTree tree, int index, float timeScale, float cycleOffset, bool mirror = false)
        {
            var children = tree.children;
            children[index].timeScale = timeScale;
            children[index].cycleOffset = cycleOffset;
            children[index].mirror = mirror;
            tree.children = children;
        }

        private static void AddCross(UBlendTree tree, AnimationClip[] clips, bool center)
        {
            int i = 0;
            if (center) tree.AddChild(clips[i++], Vector2.zero);
            tree.AddChild(clips[i++], new Vector2(0f, 1f));
            tree.AddChild(clips[i++], new Vector2(1f, 0f));
            tree.AddChild(clips[i++], new Vector2(0f, -1f));
            tree.AddChild(clips[i], new Vector2(-1f, 0f));
        }

        private HonamiBlendTreeNode Convert(AnimatorController controller)
        {
            HonamiController result = window.RunConversion(controller, $"{Folder}/Converted.asset");
            HonamiState state = result.states.First(s => s.stateName == "Tree");
            return (HonamiBlendTreeNode)state.node;
        }

        [Test]
        public void FreeformDirectional2D_ConvertsToHonami2DTree()
        {
            UBlendTree tree = CreateTree(BlendTreeType.FreeformDirectional2D, out var controller);
            AddCross(tree, clips, center: true);
            Tweak(tree, 1, 1.5f, 0.25f);
            Tweak(tree, 2, 0.5f, 1.75f);

            var node = Convert(controller);

            Assert.AreEqual(HonamiBlendTreeType.FreeformDirectional2D, node.blendType);
            Assert.AreEqual("MoveX", node.blendParameter);
            Assert.AreEqual("MoveY", node.blendParameterY);
            Assert.AreEqual(5, node.blendMotions.Count);
            Assert.AreEqual(Vector2.zero, node.blendMotions[0].position);
            Assert.AreEqual(new Vector2(0f, 1f), node.blendMotions[1].position);
            Assert.AreEqual(new Vector2(-1f, 0f), node.blendMotions[4].position);
            Assert.AreSame(clips[1], node.blendMotions[1].clip);
            Assert.AreEqual(1.5f, node.blendMotions[1].speed);
            Assert.AreEqual(0.25f, node.blendMotions[1].cycleOffset, 1e-6f);
            Assert.AreEqual(0.5f, node.blendMotions[2].speed);
            Assert.AreEqual(0.75f, node.blendMotions[2].cycleOffset, 1e-6f);
            Assert.IsEmpty(window.conversionWarnings);
            Assert.AreEqual(0f, node.blendParameterDampTime);
            Assert.AreEqual(0f, node.blendParameterDampTimeY);
            Assert.IsTrue(node.TryGetDirectionalSolver(out _, out string error), error);
        }

        [Test]
        public void SimpleDirectional2D_ConvertsToFreeformWithInfoNote()
        {
            UBlendTree tree = CreateTree(BlendTreeType.SimpleDirectional2D, out var controller);
            AddCross(tree, clips, center: true);

            var node = Convert(controller);

            Assert.AreEqual(HonamiBlendTreeType.FreeformDirectional2D, node.blendType);
            Assert.AreEqual("MoveY", node.blendParameterY);
            Assert.AreEqual(5, node.blendMotions.Count);
            Assert.IsEmpty(window.conversionWarnings);
            Assert.AreEqual(1, window.conversionInfo.Count);
            StringAssert.Contains("SimpleDirectional2D", window.conversionInfo[0]);
            Assert.IsTrue(node.TryGetDirectionalSolver(out _, out string error), error);
        }

        [Test]
        public void SimpleDirectional2D_WithoutCenter_WarnsItCannotEvaluate()
        {
            UBlendTree tree = CreateTree(BlendTreeType.SimpleDirectional2D, out var controller);
            AddCross(tree, clips, center: false);

            var node = Convert(controller);

            Assert.AreEqual(HonamiBlendTreeType.FreeformDirectional2D, node.blendType);
            Assert.IsTrue(window.conversionWarnings.Any(w => w.Contains("will not evaluate")));
        }

        [Test]
        public void Directional2D_MirrorIsDroppedWithWarning()
        {
            UBlendTree tree = CreateTree(BlendTreeType.FreeformDirectional2D, out var controller);
            AddCross(tree, clips, center: true);
            Tweak(tree, 2, 1f, 0f, mirror: true);

            var node = Convert(controller);

            Assert.IsFalse(node.blendMotions.Any(m => m.mirror));
            Assert.IsTrue(window.conversionWarnings.Any(w => w.Contains("mirror")));
            Assert.IsTrue(node.TryGetDirectionalSolver(out _, out string error), error);
        }

        [Test]
        public void Directional2D_ReverseChildWarnsNamingChild()
        {
            UBlendTree tree = CreateTree(BlendTreeType.FreeformDirectional2D, out var controller);
            AddCross(tree, clips, center: true);
            Tweak(tree, 3, -1f, 0f);

            Convert(controller);

            Assert.IsTrue(window.conversionWarnings.Any(w => w.Contains("child 3") && w.Contains("reverse playback")));
        }

        [Test]
        public void Directional2D_NegativeStateSpeedAndStateMirrorWarn()
        {
            UBlendTree tree = CreateTree(BlendTreeType.FreeformDirectional2D, out var controller);
            AddCross(tree, clips, center: true);
            AnimatorState unityState = controller.layers[0].stateMachine.states.First(s => s.state.name == "Tree").state;
            unityState.speed = -1f;
            unityState.mirror = true;

            Convert(controller);

            Assert.IsTrue(window.conversionWarnings.Any(w => w.Contains("state speed")));
            Assert.IsTrue(window.conversionWarnings.Any(w => w.Contains("mirrored state")));
            Assert.IsTrue(window.conversionWarnings.Any(w => w.Contains("layer 'Base Layer'")));
        }

        [Test]
        public void Simple1D_WithNestedDirectional2DChild_FlattensWithWarning()
        {
            UBlendTree tree = CreateTree(BlendTreeType.Simple1D, out var controller);
            tree.AddChild(clips[0], 0f);
            UBlendTree nested = tree.CreateBlendTreeChild(0f);
            nested.blendType = BlendTreeType.FreeformDirectional2D;
            AddCross(nested, clips, center: true);

            var node = Convert(controller);

            Assert.AreEqual(HonamiBlendTreeType.Standard1D, node.blendType);
            Assert.AreEqual(6, node.blendMotions.Count);
            Assert.IsTrue(window.conversionWarnings.Any(w => w.Contains("nested FreeformDirectional2D") && w.Contains("Thresholds")));
        }

        [Test]
        public void Directional2D_NullMotionIsSkippedWithWarning()
        {
            UBlendTree tree = CreateTree(BlendTreeType.FreeformDirectional2D, out var controller);
            AddCross(tree, clips, center: true);
            tree.AddChild(null, new Vector2(2f, 2f));

            var node = Convert(controller);

            Assert.AreEqual(5, node.blendMotions.Count);
            Assert.IsTrue(window.conversionWarnings.Any(w => w.Contains("empty motion")));
        }

        [Test]
        public void Directional2D_NestedTreeFallsBackToFlat()
        {
            UBlendTree tree = CreateTree(BlendTreeType.FreeformDirectional2D, out var controller);
            tree.AddChild(clips[0], Vector2.zero);
            UBlendTree nested = tree.CreateBlendTreeChild(new Vector2(1f, 0f));
            nested.blendType = BlendTreeType.Simple1D;
            nested.AddChild(clips[1], 0f);
            nested.AddChild(clips[2], 1f);

            var node = Convert(controller);

            Assert.AreEqual(HonamiBlendTreeType.Standard1D, node.blendType);
            Assert.AreEqual(3, node.blendMotions.Count);
            Assert.IsTrue(window.conversionWarnings.Any(w => w.Contains("nested")));
        }

        [Test]
        public void FreeformCartesian2D_FlattensWithWarning()
        {
            UBlendTree tree = CreateTree(BlendTreeType.FreeformCartesian2D, out var controller);
            AddCross(tree, clips, center: true);

            var node = Convert(controller);

            Assert.AreEqual(HonamiBlendTreeType.Standard1D, node.blendType);
            Assert.AreEqual("MoveX", node.blendParameter);
            Assert.IsTrue(string.IsNullOrEmpty(node.blendParameterY));
            Assert.AreEqual(5, node.blendMotions.Count);
            Assert.AreEqual(1, window.conversionWarnings.Count);
            StringAssert.Contains("FreeformCartesian2D", window.conversionWarnings[0]);
            StringAssert.Contains("'Tree'", window.conversionWarnings[0]);
        }

        [Test]
        public void Direct_FlattensWithWarning()
        {
            UBlendTree tree = CreateTree(BlendTreeType.Direct, out var controller);
            tree.AddChild(clips[0]);
            tree.AddChild(clips[1]);

            var node = Convert(controller);

            Assert.AreEqual(HonamiBlendTreeType.Standard1D, node.blendType);
            Assert.AreEqual(2, node.blendMotions.Count);
            StringAssert.Contains("Direct", window.conversionWarnings.Single());
        }

        [Test]
        public void Simple1D_StillConvertsWithoutWarnings()
        {
            UBlendTree tree = CreateTree(BlendTreeType.Simple1D, out var controller);
            tree.AddChild(clips[0], 0f);
            tree.AddChild(clips[1], 1f);
            Tweak(tree, 1, 2f, 0f);

            var node = Convert(controller);

            Assert.AreEqual(HonamiBlendTreeType.Standard1D, node.blendType);
            Assert.AreEqual(2, node.blendMotions.Count);
            Assert.AreEqual(1f, node.blendMotions[1].threshold);
            Assert.AreEqual(2f, node.blendMotions[1].speed);
            Assert.IsEmpty(window.conversionWarnings);
        }
    }
}
