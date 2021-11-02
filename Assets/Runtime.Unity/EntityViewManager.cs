using System;
using System.Collections.Generic;
using Tofunaut.TofuECS.Physics;
using UnityEngine;

namespace Tofunaut.TofuECS.Unity
{
    public class EntityViewManager
    {
        private readonly Simulation _sim;
        private readonly ECSDatabase _database;
        private Dictionary<int, (EntityView entityView, Transform transform)> _entityToView;
        private Dictionary<int, Queue<EntityView>> _prefabIdToPool;

        public EntityViewManager(Simulation sim, ECSDatabase database)
        {
            _sim = sim;
            _database = database;
            _entityToView = new Dictionary<int, (EntityView, Transform)>();
            _prefabIdToPool = new Dictionary<int, Queue<EntityView>>();
        }

        public EntityView GetEntityView(int entityId) =>
            !_entityToView.TryGetValue(entityId, out var tuple) ? null : tuple.Item1;

        public void RequestView(int entityId, int prefabId)
        {
            if (!_prefabIdToPool.TryGetValue(prefabId, out var pool))
            {
                pool = new Queue<EntityView>();
                _prefabIdToPool.Add(prefabId, pool);
            }

            // TODO: pre-instantiated pools?
            var entityView = pool.Count <= 0
                ? UnityEngine.Object.Instantiate(_database.GetEntityViewPrefab(prefabId)).GetComponent<EntityView>()
                : pool.Dequeue();

            _entityToView[entityId] = (entityView, entityView.transform);
            entityView.Initialize(_sim, entityId);
        }

        public void ReleaseView(int entityId)
        {
            if (!_entityToView.TryGetValue(entityId, out var tuple))
                return;
            
            tuple.entityView.CleanUp();
            _entityToView.Remove(entityId);
            tuple.entityView.gameObject.SetActive(false);
            _prefabIdToPool[tuple.Item1.PrefabId].Enqueue(tuple.Item1);
        }

        public void UpdateTransforms(Frame f)
        {
            var transform2dIterator = f.GetIterator<Transform2D>();
            while (transform2dIterator.Next(out var entityId, out var transform2d))
            {
                if (!_entityToView.TryGetValue(entityId, out var tuple))
                    continue;

                tuple.transform.position = new Vector3((float)transform2d.Position.X, (float)transform2d.Position.Y, 0f);
                tuple.transform.eulerAngles = new Vector3(0f, 0f, (float)transform2d.Rotation);
            }
        }
    }
}