#if UNITY_WEBGL && !UNITY_EDITOR
#else

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

        public void Clear()
        {
            _authoritative.Clear();
        }

        public void ReplaceFromSnapshot(RepeatedField<EntityState> entities)
        {
            _authoritative.Clear();
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
            _authoritative[e.EntityId] = ToVector3(e.Position);
        }

        public bool TryGetAuthoritative(ulong entityId, out Vector3 position)
        {
            return _authoritative.TryGetValue(entityId, out position);
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
#endif
