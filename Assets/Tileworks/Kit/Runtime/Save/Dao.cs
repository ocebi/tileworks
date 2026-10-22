using System;
using System.Collections.Generic;
using System.Reflection;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Save
{
    public abstract class Dao<TEntity, TKey> where TEntity : class, new()
    {
        protected Dao(IDatabase db)
        {
            Db = Guard.NotNull(db, nameof(db));
        }

        protected IDatabase Db { get; }

        public virtual TEntity Find(TKey key) => Db.Find<TEntity>(key);

        public virtual IReadOnlyList<TEntity> All() => Db.Query<TEntity>("SELECT * FROM " + Db.TableOf<TEntity>());

        public virtual void Insert(TEntity entity) => Db.Insert(Guard.NotNull(entity, nameof(entity)));

        public virtual void Upsert(TEntity entity) => Db.InsertOrReplace(Guard.NotNull(entity, nameof(entity)));

        public virtual void Delete(TKey key) => Db.Delete<TEntity>(key);

        public int Count() => Db.Scalar<int>("SELECT COUNT(*) FROM " + Db.TableOf<TEntity>());
    }

    // Cache changes only after commit. Returned entities are shared: don't mutate them.
    public interface ICachedDao
    {
        bool IsLoaded { get; }
        void Load();
    }

    public abstract class CachedDao<TEntity, TKey> : Dao<TEntity, TKey>, ICachedDao where TEntity : class, new()
    {
        private static readonly Func<object, object> CloneMethod = CreateClone();

        private readonly Dictionary<TKey, TEntity> _cache = new Dictionary<TKey, TEntity>();
        private readonly List<TEntity> _all = new List<TEntity>();
        private bool _allDirty = true;

        protected CachedDao(IDatabase db) : base(db) { }

        public bool IsLoaded { get; private set; }

        protected abstract TKey KeyOf(TEntity entity);

        public void Load()
        {
            _cache.Clear();
            foreach (var entity in base.All()) _cache[KeyOf(entity)] = entity;
            _allDirty = true;
            IsLoaded = true;
        }

        public override TEntity Find(TKey key)
        {
            EnsureLoaded();
            return _cache.TryGetValue(key, out var entity) ? entity : null;
        }

        public override IReadOnlyList<TEntity> All()
        {
            EnsureLoaded();
            if (_allDirty)
            {
                _all.Clear();
                _all.AddRange(_cache.Values);
                _allDirty = false;
            }

            return _all;
        }

        public override void Insert(TEntity entity)
        {
            EnsureLoaded();
            base.Insert(entity);
            var copy = Copy(entity);
            AfterCommit(() => Put(copy));
        }

        public override void Upsert(TEntity entity)
        {
            EnsureLoaded();
            base.Upsert(entity);
            var copy = Copy(entity);
            AfterCommit(() => Put(copy));
        }

        public override void Delete(TKey key)
        {
            EnsureLoaded();
            base.Delete(key);
            AfterCommit(() =>
            {
                _cache.Remove(key);
                _allDirty = true;
            });
        }

        private void Put(TEntity entity)
        {
            _cache[KeyOf(entity)] = entity;
            _allDirty = true;
        }

        private void AfterCommit(Action apply)
        {
            var transaction = Db.Current;
            if (transaction == null) apply();
            else transaction.OnCommitted(apply);
        }

        private void EnsureLoaded()
        {
            if (!IsLoaded) throw new InvalidOperationException($"{GetType().Name} used before Load().");
        }

        private static TEntity Copy(TEntity entity) => (TEntity)CloneMethod(entity);

        private static Func<object, object> CreateClone()
        {
            var method = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            return o => method.Invoke(o, null);
        }
    }
}
