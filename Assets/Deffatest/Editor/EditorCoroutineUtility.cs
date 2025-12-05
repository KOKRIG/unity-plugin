using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

namespace Deffatest.Editor
{
    /// <summary>
    /// Utility for running coroutines in Unity Editor.
    /// Required for async API calls in Editor scripts since
    /// MonoBehaviour.StartCoroutine() is not available in Editor context.
    /// </summary>
    public class EditorCoroutine
    {
        private readonly IEnumerator routine;
        private bool isRunning = true;
        private readonly object owner;

        private EditorCoroutine(IEnumerator routine, object owner)
        {
            this.routine = routine;
            this.owner = owner;
            EditorApplication.update += Update;
        }

        private void Update()
        {
            if (!isRunning)
            {
                EditorApplication.update -= Update;
                return;
            }

            try
            {
                // Check if owner is still valid (for EditorWindow/ScriptableObject)
                if (owner != null && owner is UnityEngine.Object unityObj && unityObj == null)
                {
                    Stop();
                    return;
                }

                if (!routine.MoveNext())
                {
                    isRunning = false;
                    EditorApplication.update -= Update;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[Deffatest] EditorCoroutine error: {e.Message}");
                isRunning = false;
                EditorApplication.update -= Update;
            }
        }

        /// <summary>
        /// Stop the coroutine
        /// </summary>
        public void Stop()
        {
            isRunning = false;
        }

        /// <summary>
        /// Check if coroutine is still running
        /// </summary>
        public bool IsRunning => isRunning;

        /// <summary>
        /// Start a new editor coroutine
        /// </summary>
        public static EditorCoroutine Start(IEnumerator routine, object owner = null)
        {
            return new EditorCoroutine(routine, owner);
        }
    }

    /// <summary>
    /// Static utility class for managing editor coroutines
    /// </summary>
    public static class EditorCoroutineUtility
    {
        private static readonly Dictionary<object, List<EditorCoroutine>> ownerCoroutines 
            = new Dictionary<object, List<EditorCoroutine>>();

        /// <summary>
        /// Start a coroutine with optional owner tracking
        /// </summary>
        /// <param name="routine">The coroutine to run</param>
        /// <param name="owner">Optional owner object for lifecycle management</param>
        /// <returns>EditorCoroutine instance that can be stopped</returns>
        public static EditorCoroutine StartCoroutine(IEnumerator routine, object owner = null)
        {
            var coroutine = EditorCoroutine.Start(routine, owner);

            if (owner != null)
            {
                if (!ownerCoroutines.ContainsKey(owner))
                {
                    ownerCoroutines[owner] = new List<EditorCoroutine>();
                }
                ownerCoroutines[owner].Add(coroutine);
            }

            return coroutine;
        }

        /// <summary>
        /// Stop a specific coroutine
        /// </summary>
        public static void StopCoroutine(EditorCoroutine coroutine)
        {
            coroutine?.Stop();
        }

        /// <summary>
        /// Stop all coroutines owned by a specific object
        /// </summary>
        public static void StopAllCoroutines(object owner)
        {
            if (owner == null) return;

            if (ownerCoroutines.TryGetValue(owner, out var coroutines))
            {
                foreach (var coroutine in coroutines)
                {
                    coroutine.Stop();
                }
                ownerCoroutines.Remove(owner);
            }
        }

        /// <summary>
        /// Clean up finished coroutines (call periodically)
        /// </summary>
        public static void Cleanup()
        {
            var emptyOwners = new List<object>();

            foreach (var kvp in ownerCoroutines)
            {
                kvp.Value.RemoveAll(c => !c.IsRunning);
                if (kvp.Value.Count == 0)
                {
                    emptyOwners.Add(kvp.Key);
                }
            }

            foreach (var owner in emptyOwners)
            {
                ownerCoroutines.Remove(owner);
            }
        }
    }

    /// <summary>
    /// Yield instructions for editor coroutines
    /// </summary>
    public class EditorWaitForSeconds : IEnumerator
    {
        private readonly double targetTime;

        public EditorWaitForSeconds(float seconds)
        {
            targetTime = EditorApplication.timeSinceStartup + seconds;
        }

        public object Current => null;

        public bool MoveNext()
        {
            return EditorApplication.timeSinceStartup < targetTime;
        }

        public void Reset() { }
    }

    /// <summary>
    /// Wait until a condition is true
    /// </summary>
    public class EditorWaitUntil : IEnumerator
    {
        private readonly Func<bool> predicate;

        public EditorWaitUntil(Func<bool> predicate)
        {
            this.predicate = predicate;
        }

        public object Current => null;

        public bool MoveNext()
        {
            return !predicate();
        }

        public void Reset() { }
    }

    /// <summary>
    /// Wait while a condition is true
    /// </summary>
    public class EditorWaitWhile : IEnumerator
    {
        private readonly Func<bool> predicate;

        public EditorWaitWhile(Func<bool> predicate)
        {
            this.predicate = predicate;
        }

        public object Current => null;

        public bool MoveNext()
        {
            return predicate();
        }

        public void Reset() { }
    }
}
