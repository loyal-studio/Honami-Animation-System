using UnityEngine;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Debug sub-node that logs formatted state context when a state enters, updates, or exits.
    /// </summary>
    public sealed class HonamiLogSubNode : HonamiSubNodeBase
    {
        [SerializeField] private bool _logOnEnter = true;
        [SerializeField] private bool _logOnUpdate = false;
        [SerializeField] private bool _logOnExit = true;
        [SerializeField, TextArea(3, 10)] private string _messageFormat = "State: {state} | Time: {time}";
        [SerializeField] private LogType _logType = LogType.Log;

        public override string DisplayName => "Log";

        public override void OnEnter(in HonamiExecutionContext context)
        {
            if (_logOnEnter)
            {
                Log(context);
            }
        }

        public override void UpdateRuntime(in HonamiExecutionContext context)
        {
            if (_logOnUpdate)
            {
                Log(context);
            }
        }

        public override void OnExit(in HonamiExecutionContext context)
        {
            if (_logOnExit)
            {
                Log(context);
            }
        }

        private void Log(in HonamiExecutionContext context)
        {
            string message = FormatMessage(context);
            Debug.LogFormat(_logType, LogOption.None, context.Animator, message);
        }

        private string FormatMessage(in HonamiExecutionContext context)
        {
            if (string.IsNullOrEmpty(_messageFormat))
            {
                return string.Empty;
            }

            string result = _messageFormat;
            result = result.Replace("{state}", context.State.stateName);
            result = result.Replace("{layer}", context.Layer.ToString());
            result = result.Replace("{controller}", GetControllerName(context));
            result = result.Replace("{previousState}", GetPreviousStateName(context));
            result = result.Replace("{time}", context.Playable.IsValid() ? context.Playable.GetTime().ToString("F2") : "0.00");
            result = result.Replace("{normalizedTime}", context.Animator.GetStateProgress(context.Layer, context.PortIndex).ToString("F2"));

            return ReplaceParameterTokens(result, context.Params);
        }

        private static string GetControllerName(in HonamiExecutionContext context)
        {
            return context.Animator.CurrentController != null ? context.Animator.CurrentController.name : "None";
        }

        private static string GetPreviousStateName(in HonamiExecutionContext context)
        {
            int previousIndex = context.Animator.GetPreviousStateIndex(context.Layer);
            return previousIndex >= 0 && previousIndex < context.Animator._activeStatesCount
                ? context.Animator._runtimeStates[previousIndex].stateName
                : "None";
        }

        private static string ReplaceParameterTokens(string message, HonamiParameterStore store)
        {
            int startIndex = 0;

            while ((startIndex = message.IndexOf("{param:", startIndex)) != -1)
            {
                int endIndex = message.IndexOf("}", startIndex);
                if (endIndex == -1)
                {
                    break;
                }

                string parameterName = message.Substring(startIndex + 7, endIndex - (startIndex + 7));
                string value = GetParameterValue(store, parameterName);

                message = message.Remove(startIndex, endIndex - startIndex + 1);
                message = message.Insert(startIndex, value);
                startIndex += value.Length;
            }

            return message;
        }

        private static string GetParameterValue(HonamiParameterStore store, string parameterName)
        {
            int hash = HonamiAnimator.StringToHash(parameterName);

            if (store.ContainsFloat(hash))
            {
                return store.GetFloat(hash).ToString("F2");
            }

            if (store.ContainsBool(hash))
            {
                return store.GetBool(hash).ToString();
            }

            if (store.ContainsInt(hash))
            {
                return store.GetInteger(hash).ToString();
            }

            if (store.ContainsTrigger(hash))
            {
                return store.IsTriggerSet(hash).ToString();
            }

            return "N/A";
        }
    }
}
