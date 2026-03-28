using System.Collections.Generic;
using Google.Protobuf.Collections;
using Mmo.Game.V1;
using UnityEngine;

namespace Mmo.Client.Unity
{
    /// <summary>Авторитетные позиции сущностей с соты (снимок/дельты), отдельно от визуалов и сетевого клиента.</summary>
    public sealed class MmoClientWorldState
    {
        readonly Dictionary<ulong, Vector3> _authoritative = new Dictionary<ulong, Vector3>();
        readonly Dictionary<ulong, Vector3> _previous = new Dictionary<ulong, Vector3>();

        public void Clear()
        {
            _authoritative.Clear();
            _previous.Clear();
        }

        public void ReplaceFromSnapshot(RepeatedField<EntityState> entities)
        {
            _authoritative.Clear();
            _previous.Clear();
            if (entities == null)
            {
                return;
            }
            foreach (var e in entities)
            {
                _authoritative[e.EntityId] = ToVector3(e.Position);
            }
        }

        public void MergeEntity(EntityState e)
        {
            if (e == null)
            {
                return;
            }
            var id = e.EntityId;
            var next = ToVector3(e.Position);
            if (_authoritative.TryGetValue(id, out var cur))
            {
                _previous[id] = cur;
            }
            _authoritative[id] = next;
        }

        public bool TryGetAuthoritative(ulong entityId, out Vector3 position)
        {
            return _authoritative.TryGetValue(entityId, out position);
        }

        /// <summary>Цель для визуала: при extrapolation01 &gt; 0 смешивает к (auth + (auth − prev)).</summary>
        public bool TryGetDisplayTarget(ulong entityId, float extrapolation01, out Vector3 worldPos)
        {
            if (!_authoritative.TryGetValue(entityId, out var auth))
            {
                worldPos = default;
                return false;
            }
            if (extrapolation01 <= 0f || !_previous.TryGetValue(entityId, out var prev))
            {
                worldPos = auth;
                return true;
            }
            var delta = auth - prev;
            worldPos = Vector3.Lerp(auth, auth + delta, Mathf.Clamp01(extrapolation01));
            return true;
        }

        public IEnumerable<ulong> EnumerateEntityIds()
        {
            return _authoritative.Keys;
        }

        static Vector3 ToVector3(Vec3f p)
        {
            return new Vector3(p.X, p.Y, p.Z);
        }
    }
}
