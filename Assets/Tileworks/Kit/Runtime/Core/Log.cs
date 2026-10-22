using System;
using System.Collections.Generic;

namespace Tileworks.Kit.Core
{
    public enum LogLevel : byte
    {
        Verbose = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
    }

    public readonly struct LogEntry
    {
        public readonly LogLevel Level;
        public readonly string Tag;
        public readonly string Message;
        public readonly Exception Exception;
        public readonly long TimestampTicks;

        public LogEntry(LogLevel level, string tag, string message, Exception exception, long timestampTicks)
        {
            Level = level;
            Tag = tag;
            Message = message;
            Exception = exception;
            TimestampTicks = timestampTicks;
        }
    }

    public interface ILogSink
    {
        void Write(in LogEntry entry);
    }

    public interface ILog
    {
        LogLevel MinimumLevel { get; set; }
        bool IsEnabled(LogLevel level);
        void Write(LogLevel level, string tag, string message, Exception exception = null);
        void AddSink(ILogSink sink);

        RingBuffer<LogEntry> Recent { get; }
    }

    public static class LogTag
    {
        public const string App = "App";
        public const string Boot = "Boot";
        public const string Di = "DI";
        public const string Save = "Save";
        public const string Time = "Time";
        public const string Ui = "UI";
        public const string Rules = "Rules";
        public const string Level = "Level";
        public const string Meta = "Meta";
        public const string Assets = "Assets";
        public const string Input = "Input";
        public const string Audio = "Audio";
        public const string Content = "Content";
    }

    public sealed class Log : ILog
    {
        private readonly List<ILogSink> _sinks = new List<ILogSink>(4);

        public Log(LogLevel minimumLevel = LogLevel.Info, int recentCapacity = 256)
        {
            MinimumLevel = minimumLevel;
            Recent = new RingBuffer<LogEntry>(recentCapacity);
        }

        public LogLevel MinimumLevel { get; set; }
        public RingBuffer<LogEntry> Recent { get; }

        public bool IsEnabled(LogLevel level) => level >= MinimumLevel;

        public void AddSink(ILogSink sink) => _sinks.Add(Guard.NotNull(sink, nameof(sink)));

        public void Write(LogLevel level, string tag, string message, Exception exception = null)
        {
            if (level < MinimumLevel) return;
            var entry = new LogEntry(level, tag, message, exception, DateTime.UtcNow.Ticks);
            Recent.Push(entry);
            for (var i = 0; i < _sinks.Count; i++) _sinks[i].Write(entry);
        }
    }

    public static class LogExtensions
    {
        public static void Info(this ILog log, string tag, string message) => log.Write(LogLevel.Info, tag, message);

        public static void Info<T1>(this ILog log, string tag, string format, T1 a)
        {
            if (log.IsEnabled(LogLevel.Info)) log.Write(LogLevel.Info, tag, string.Format(format, a));
        }

        public static void Info<T1, T2>(this ILog log, string tag, string format, T1 a, T2 b)
        {
            if (log.IsEnabled(LogLevel.Info)) log.Write(LogLevel.Info, tag, string.Format(format, a, b));
        }

        public static void Info<T1, T2, T3>(this ILog log, string tag, string format, T1 a, T2 b, T3 c)
        {
            if (log.IsEnabled(LogLevel.Info)) log.Write(LogLevel.Info, tag, string.Format(format, a, b, c));
        }

        public static void Warn(this ILog log, string tag, string message) => log.Write(LogLevel.Warning, tag, message);

        public static void Warn<T1>(this ILog log, string tag, string format, T1 a)
        {
            if (log.IsEnabled(LogLevel.Warning)) log.Write(LogLevel.Warning, tag, string.Format(format, a));
        }

        public static void Warn<T1, T2>(this ILog log, string tag, string format, T1 a, T2 b)
        {
            if (log.IsEnabled(LogLevel.Warning)) log.Write(LogLevel.Warning, tag, string.Format(format, a, b));
        }

        public static void Error(this ILog log, string tag, string message, Exception exception = null) =>
            log.Write(LogLevel.Error, tag, message, exception);

        public static void Exception(this ILog log, string tag, Exception exception) =>
            log.Write(LogLevel.Error, tag, exception.Message, exception);

        [System.Diagnostics.Conditional("TILEWORKS_LOG_VERBOSE")]
        public static void Debug(this ILog log, string tag, string message) => log.Write(LogLevel.Debug, tag, message);

        [System.Diagnostics.Conditional("TILEWORKS_LOG_VERBOSE")]
        public static void Debug<T1>(this ILog log, string tag, string format, T1 a)
        {
            if (log.IsEnabled(LogLevel.Debug)) log.Write(LogLevel.Debug, tag, string.Format(format, a));
        }

        [System.Diagnostics.Conditional("TILEWORKS_LOG_VERBOSE")]
        public static void Debug<T1, T2>(this ILog log, string tag, string format, T1 a, T2 b)
        {
            if (log.IsEnabled(LogLevel.Debug)) log.Write(LogLevel.Debug, tag, string.Format(format, a, b));
        }
    }

    public sealed class NullLog : ILog
    {
        public LogLevel MinimumLevel { get; set; } = LogLevel.Error;
        public RingBuffer<LogEntry> Recent { get; } = new RingBuffer<LogEntry>(1);
        public bool IsEnabled(LogLevel level) => false;
        public void Write(LogLevel level, string tag, string message, Exception exception = null) { }
        public void AddSink(ILogSink sink) { }
    }
}
