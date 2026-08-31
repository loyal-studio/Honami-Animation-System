using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Runtime storage for Honami controller parameters, backed by native arrays for fast evaluator access.
    /// </summary>
    public sealed class HonamiParameterStore : IDisposable
    {
        private NativeArray<float> _floatValues;
        private NativeArray<int> _intValues;
        private NativeArray<bool> _boolValues;
        private NativeArray<bool> _triggerValues;

        private readonly Dictionary<int, int> _floatHashToIndex = new();
        private readonly Dictionary<int, int> _intHashToIndex = new();
        private readonly Dictionary<int, int> _boolHashToIndex = new();
        private readonly Dictionary<int, int> _triggerHashToIndex = new();

        private readonly HashSet<int> _pendingIndices = new();
        private readonly List<int> _activeIndices = new();
        private readonly HashSet<int> _indicesToConsume = new();
        private readonly List<int> _randomParameterIndices = new();

        /// <summary>
        /// Rebuilds parameter storage from a runtime controller.
        /// </summary>
        public void Initialize(HonamiRuntimeController controller)
        {
            Dispose();

            if (controller == null)
            {
                return;
            }

            var activeParameters = controller.ActiveParameters;
            CountParameters(activeParameters, out int floatCount, out int intCount, out int boolCount, out int triggerCount);
            AllocateStorage(floatCount, intCount, boolCount, triggerCount);
            SeedParameterDefaults(activeParameters);
        }

        public void SetFloat(string name, float value) => SetFloat(HonamiAnimator.StringToHash(name), value);

        public void SetFloat(int hash, float value)
        {
            if (_floatHashToIndex.TryGetValue(hash, out int index))
            {
                _floatValues[index] = value;
            }
        }

        public float GetFloat(string name) => GetFloat(HonamiAnimator.StringToHash(name));

        public float GetFloat(int hash) => _floatHashToIndex.TryGetValue(hash, out int index) ? _floatValues[index] : 0f;

        public void SetInteger(string name, int value) => SetInteger(HonamiAnimator.StringToHash(name), value);

        public void SetInteger(int hash, int value)
        {
            if (_intHashToIndex.TryGetValue(hash, out int index))
            {
                _intValues[index] = value;
            }
        }

        public int GetInteger(string name) => GetInteger(HonamiAnimator.StringToHash(name));

        public int GetInteger(int hash) => _intHashToIndex.TryGetValue(hash, out int index) ? _intValues[index] : 0;

        public void SetBool(string name, bool value) => SetBool(HonamiAnimator.StringToHash(name), value);

        public void SetBool(int hash, bool value)
        {
            if (_boolHashToIndex.TryGetValue(hash, out int index))
            {
                _boolValues[index] = value;
            }
        }

        public bool GetBool(string name) => GetBool(HonamiAnimator.StringToHash(name));

        public bool GetBool(int hash) => _boolHashToIndex.TryGetValue(hash, out int index) && _boolValues[index];

        public void SetTrigger(string name) => SetTrigger(HonamiAnimator.StringToHash(name));

        public void SetTrigger(int hash)
        {
            if (!_triggerHashToIndex.TryGetValue(hash, out int index))
            {
                return;
            }

            _triggerValues[index] = true;
            if (!_pendingIndices.Contains(index))
            {
                _pendingIndices.Add(index);
                _activeIndices.Add(index);
            }
        }

        public void ResetTrigger(string name) => ResetTrigger(HonamiAnimator.StringToHash(name));

        public void ResetTrigger(int hash)
        {
            if (!_triggerHashToIndex.TryGetValue(hash, out int index))
            {
                return;
            }

            _triggerValues[index] = false;
            _pendingIndices.Remove(index);
            _activeIndices.Remove(index);
        }

        public bool IsTriggerSet(int hash) => _triggerHashToIndex.TryGetValue(hash, out int index) && _triggerValues[index];
        public bool IsBoolSet(int hash) => _boolHashToIndex.TryGetValue(hash, out int index) && _boolValues[index];

        public bool ContainsFloat(int hash) => _floatHashToIndex.ContainsKey(hash);
        public bool ContainsInt(int hash) => _intHashToIndex.ContainsKey(hash);
        public bool ContainsBool(int hash) => _boolHashToIndex.ContainsKey(hash);
        public bool ContainsTrigger(int hash) => _triggerHashToIndex.ContainsKey(hash);

        public bool TryGetFloatIndex(int hash, out int index) => _floatHashToIndex.TryGetValue(hash, out index);
        public bool TryGetIntIndex(int hash, out int index) => _intHashToIndex.TryGetValue(hash, out index);
        public bool TryGetBoolIndex(int hash, out int index) => _boolHashToIndex.TryGetValue(hash, out index);
        public bool TryGetTriggerIndex(int hash, out int index) => _triggerHashToIndex.TryGetValue(hash, out index);

        public float GetFloatByIndex(int index) => _floatValues[index];
        public int GetIntByIndex(int index) => _intValues[index];
        public bool GetBoolByIndex(int index) => _boolValues[index];
        public bool GetTriggerByIndex(int index) => _triggerValues[index];

        public void CommitTriggers(List<int> tentativeHashes)
        {
            int count = tentativeHashes.Count;
            for (int i = 0; i < count; i++)
            {
                if (_triggerHashToIndex.TryGetValue(tentativeHashes[i], out int index))
                {
                    _indicesToConsume.Add(index);
                }
            }
        }

        public void UpdateRandomParameters()
        {
            int count = _randomParameterIndices.Count;
            for (int i = 0; i < count; i++)
            {
                _floatValues[_randomParameterIndices[i]] = UnityEngine.Random.value;
            }
        }

        public void ConsumePendingTriggers()
        {
            foreach (int index in _indicesToConsume)
            {
                _triggerValues[index] = false;
                _pendingIndices.Remove(index);
                _activeIndices.Remove(index);
            }

            _indicesToConsume.Clear();
        }

        public void ClearTriggers()
        {
            int count = _activeIndices.Count;
            for (int i = 0; i < count; i++)
            {
                int index = _activeIndices[i];
                _triggerValues[index] = false;
                _pendingIndices.Remove(index);
            }

            _activeIndices.Clear();
        }

        public void ClearAllTriggers()
        {
            _pendingIndices.Clear();
            _activeIndices.Clear();
            _indicesToConsume.Clear();
        }

        public void ApplyAssignment(HonamiParameterAssignment assignment, Dictionary<HonamiParameterAssignment, int> hashCache)
        {
            if (!hashCache.TryGetValue(assignment, out int parameterHash))
            {
                return;
            }

            switch (assignment.type)
            {
                case HonamiParameterType.Float:
                    SetFloat(parameterHash, assignment.targetFloat);
                    break;

                case HonamiParameterType.Int:
                    SetInteger(parameterHash, assignment.targetInt);
                    break;

                case HonamiParameterType.Bool:
                    SetBool(parameterHash, assignment.targetBool);
                    break;

                case HonamiParameterType.Trigger:
                    if (assignment.setTrigger)
                    {
                        SetTrigger(parameterHash);
                    }
                    else
                    {
                        ResetTrigger(parameterHash);
                    }
                    break;
            }
        }

        public void Dispose()
        {
            if (_floatValues.IsCreated)
            {
                _floatValues.Dispose();
            }

            if (_intValues.IsCreated)
            {
                _intValues.Dispose();
            }

            if (_boolValues.IsCreated)
            {
                _boolValues.Dispose();
            }

            if (_triggerValues.IsCreated)
            {
                _triggerValues.Dispose();
            }

            _floatHashToIndex.Clear();
            _intHashToIndex.Clear();
            _boolHashToIndex.Clear();
            _triggerHashToIndex.Clear();
            _randomParameterIndices.Clear();
            _pendingIndices.Clear();
            _activeIndices.Clear();
            _indicesToConsume.Clear();
        }

        private static void CountParameters(
            IReadOnlyList<HonamiParameter> parameters,
            out int floatCount,
            out int intCount,
            out int boolCount,
            out int triggerCount)
        {
            floatCount = 0;
            intCount = 0;
            boolCount = 0;
            triggerCount = 0;

            for (int i = 0; i < parameters.Count; i++)
            {
                switch (parameters[i].type)
                {
                    case HonamiParameterType.Float:
                    case HonamiParameterType.Random:
                        floatCount++;
                        break;

                    case HonamiParameterType.Int:
                        intCount++;
                        break;

                    case HonamiParameterType.Bool:
                        boolCount++;
                        break;

                    case HonamiParameterType.Trigger:
                        triggerCount++;
                        break;
                }
            }
        }

        private void AllocateStorage(int floatCount, int intCount, int boolCount, int triggerCount)
        {
            _floatValues = new NativeArray<float>(floatCount, Allocator.Persistent);
            _intValues = new NativeArray<int>(intCount, Allocator.Persistent);
            _boolValues = new NativeArray<bool>(boolCount, Allocator.Persistent);
            _triggerValues = new NativeArray<bool>(triggerCount, Allocator.Persistent);
        }

        private void SeedParameterDefaults(IReadOnlyList<HonamiParameter> parameters)
        {
            int floatIndex = 0;
            int intIndex = 0;
            int boolIndex = 0;
            int triggerIndex = 0;

            for (int i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                int hash = HonamiAnimator.StringToHash(parameter.name);

                switch (parameter.type)
                {
                    case HonamiParameterType.Float:
                    case HonamiParameterType.Random:
                        RegisterFloatParameter(parameter, hash, ref floatIndex);
                        break;

                    case HonamiParameterType.Int:
                        RegisterIntParameter(parameter, hash, ref intIndex);
                        break;

                    case HonamiParameterType.Bool:
                        RegisterBoolParameter(parameter, hash, ref boolIndex);
                        break;

                    case HonamiParameterType.Trigger:
                        RegisterTriggerParameter(parameter, hash, ref triggerIndex);
                        break;
                }
            }
        }

        private void RegisterFloatParameter(HonamiParameter parameter, int hash, ref int index)
        {
#if UNITY_EDITOR
            if (_floatHashToIndex.ContainsKey(hash))
            {
                Debug.LogError($"[Honami] Float or Random parameter hash collision: '{parameter.name}' shares a hash with another parameter. Parameter may be unreachable.");
            }
#endif
            _floatHashToIndex[hash] = index;

            if (parameter.type == HonamiParameterType.Random)
            {
                _randomParameterIndices.Add(index);
            }

            _floatValues[index++] = parameter.type == HonamiParameterType.Random
                ? UnityEngine.Random.value
                : parameter.defaultFloat;
        }

        private void RegisterIntParameter(HonamiParameter parameter, int hash, ref int index)
        {
#if UNITY_EDITOR
            if (_intHashToIndex.ContainsKey(hash))
            {
                Debug.LogError($"[Honami] Int parameter hash collision: '{parameter.name}' shares a hash with another int parameter. Parameter may be unreachable.");
            }
#endif
            _intHashToIndex[hash] = index;
            _intValues[index++] = parameter.defaultInt;
        }

        private void RegisterBoolParameter(HonamiParameter parameter, int hash, ref int index)
        {
#if UNITY_EDITOR
            if (_boolHashToIndex.ContainsKey(hash))
            {
                Debug.LogError($"[Honami] Bool parameter hash collision: '{parameter.name}' shares a hash with another bool parameter. Parameter may be unreachable.");
            }
#endif
            _boolHashToIndex[hash] = index;
            _boolValues[index++] = parameter.defaultBool;
        }

        private void RegisterTriggerParameter(HonamiParameter parameter, int hash, ref int index)
        {
#if UNITY_EDITOR
            if (_triggerHashToIndex.ContainsKey(hash))
            {
                Debug.LogError($"[Honami] Trigger parameter hash collision: '{parameter.name}' shares a hash with another trigger parameter. Parameter may be unreachable.");
            }
#endif
            _triggerHashToIndex[hash] = index;
            _triggerValues[index++] = false;
        }
    }
}
