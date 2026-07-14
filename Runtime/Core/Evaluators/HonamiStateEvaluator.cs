namespace HonamiAnimationSystem.Runtime.Core
{
    public static class HonamiStateEvaluator
    {
        public static float GetStateDuration(HonamiRuntimeController controller, HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParamValue = 0f)
        {
            if (state?.node == null) return 1f;
            return controller.GetActiveNode(state).GetDuration(state, stateIndex, runtime, blendParamValue);
        }

        public static float GetUnscaledStateDuration(HonamiRuntimeController controller, HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParamValue = 0f)
        {
            if (state?.node == null) return 1f;
            return controller.GetActiveNode(state).GetUnscaledDuration(state, stateIndex, runtime, blendParamValue);
        }
    }
}
