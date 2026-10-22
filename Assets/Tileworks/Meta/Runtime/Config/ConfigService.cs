using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.Core;
using Tileworks.Meta.Api;

namespace Tileworks.Meta.Config
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ConfigSectionAttribute : Attribute
    {
        public ConfigSectionAttribute(string name) => Name = name;
        public string Name { get; }
    }

    // Where section JSON comes from; v1 is local files, a remote source would layer on top (§12.1).
    public interface IConfigSource
    {
        bool TryGetSection(string name, out string json);
    }

    public sealed class JsonConfigSource : IConfigSource
    {
        private readonly JObject _root;

        public JsonConfigSource(string json) => _root = string.IsNullOrEmpty(json) ? new JObject() : JObject.Parse(json);

        public bool TryGetSection(string name, out string json)
        {
            json = _root[name]?.ToString(Formatting.None);
            return json != null;
        }
    }

    // Compiled defaults, overridden section by section; a section that fails validation keeps its default.
    public sealed class ConfigService : IConfig
    {
        private readonly IReadOnlyList<IConfigSource> _sources;
        private readonly ILog _log;
        private readonly Dictionary<Type, object> _sections = new Dictionary<Type, object>();
        private readonly List<string> _invalid = new List<string>();

        public ConfigService(IReadOnlyList<IConfigSource> sources, ILog log)
        {
            _sources = Guard.NotNull(sources, nameof(sources));
            _log = Guard.NotNull(log, nameof(log));
        }

        public IReadOnlyCollection<string> Invalid => _invalid;

        public T Get<T>() where T : class, IValidatable, new()
        {
            if (_sections.TryGetValue(typeof(T), out var cached)) return (T)cached;
            var section = Load<T>();
            _sections[typeof(T)] = section;
            return section;
        }

        private T Load<T>() where T : class, IValidatable, new()
        {
            var name = typeof(T).GetCustomAttribute<ConfigSectionAttribute>()?.Name ?? typeof(T).Name;
            var section = new T();
            foreach (var source in _sources)
            {
                if (!source.TryGetSection(name, out var json)) continue;
                try
                {
                    var candidate = new T();
                    JsonConvert.PopulateObject(json, candidate);
                    var report = new ValidationReport();
                    candidate.Validate(report);
                    if (report.HasErrors)
                    {
                        _invalid.Add(name);
                        _log.Warn("Meta", "Config section {0} is invalid, keeping the previous value:\n{1}", name, report);
                        continue;
                    }

                    section = candidate;
                }
                catch (Exception e)
                {
                    _invalid.Add(name);
                    _log.Warn("Meta", "Config section {0} could not be read: {1}", name, e.Message);
                }
            }

            return section;
        }
    }
}
