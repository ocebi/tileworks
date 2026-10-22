using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Tileworks.Tiles.Editor
{
    // Generates levels on every core but one, off the main thread, behind a cancellable progress bar; the results come
    // back on the main thread. `make` must not touch Unity APIs.
    public static class LevelBatch
    {
        public static void Run<T>(string title, int count, Func<int, T> make, Action<T[]> finish)
        {
            var results = new T[count];
            var done = 0;
            var cancel = new CancellationTokenSource();
            var options = new ParallelOptions { CancellationToken = cancel.Token, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            var started = EditorApplication.timeSinceStartup;
            var task = Task.Run(() => Parallel.For(0, count, options, i =>
            {
                results[i] = make(i);
                Interlocked.Increment(ref done);
            }));

            void Poll()
            {
                if (!task.IsCompleted)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(title, $"{done} of {count}", (float)done / count)) cancel.Cancel();
                    return;
                }

                EditorApplication.update -= Poll;
                EditorUtility.ClearProgressBar();
                cancel.Dispose();
                if (task.IsCanceled || task.Exception?.InnerException is OperationCanceledException)
                {
                    Debug.Log($"[Tileworks] {title}: cancelled, nothing written");
                    return;
                }

                if (task.IsFaulted)
                {
                    Debug.LogException(task.Exception);
                    return;
                }

                Debug.Log($"[Tileworks] {title}: {count} in {EditorApplication.timeSinceStartup - started:0}s");
                finish(results);
            }

            EditorApplication.update += Poll;
        }
    }
}
