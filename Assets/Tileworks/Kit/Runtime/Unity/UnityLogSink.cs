using Tileworks.Kit.Core;
using UnityEngine;

namespace Tileworks.Kit.Unity
{
    public sealed class UnityLogSink : ILogSink
    {
        public void Write(in LogEntry entry)
        {
            var text = $"[{entry.Tag}] {entry.Message}";
            switch (entry.Level)
            {
                case LogLevel.Error:
                    if (entry.Exception != null)
                    {
                        Debug.LogError(text);
                        Debug.LogException(entry.Exception);
                    }
                    else
                    {
                        Debug.LogError(text);
                    }

                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(text);
                    break;
                default:
                    Debug.Log(text);
                    break;
            }
        }
    }
}
