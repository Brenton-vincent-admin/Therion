using System.Collections.Generic;
using Dino.World;
using Unity.Netcode;
using UnityEngine;

namespace Dino.Net
{
    // The shared world: how much food is left in every plant, and carcasses. The host's numbers win.
    //
    // Plants are the same objects on every phone (same map), so each gets the same id from its position.
    // When a friend eats, their phone tells the host "I took this much"; the host updates its plant and
    // tells everyone the new amount. Carcasses are created by the host and copied to the others.
    public partial class NetSession
    {
        class FoodEntry
        {
            public int Id;
            public IFoodSource Source;
            public INetSyncedFood Sync;
            public Object Obj;          // for "was it destroyed?" checks
            public bool IsCarcass;
            public float LastKnown;     // client: amount at the last check; host: amount last sent
            public float IgnoreHostUntil;
        }

        readonly Dictionary<int, FoodEntry> _food = new Dictionary<int, FoodEntry>();
        readonly Dictionary<IFoodSource, int> _foodIds = new Dictionary<IFoodSource, int>();
        readonly HashSet<IFoodSource> _unsyncedFood = new HashSet<IFoodSource>();
        readonly List<FoodEntry> _dirty = new List<FoodEntry>();
        readonly List<int> _deadIds = new List<int>();

        float _worldTimer, _fullSyncTimer;
        int _registryCount = -1;
        float _registryTimer;
        int _nextCarcassId = 1;

        const float HostTick = 0.5f, ClientTick = 0.2f, FullSyncEvery = 10f;
        const int FoodPerMessage = 60;

        void ResetWorld()
        {
            _food.Clear();
            _foodIds.Clear();
            _unsyncedFood.Clear();
            _dirty.Clear();
            _worldTimer = 0f; _fullSyncTimer = 0f;
            _registryCount = -1;
            _nextCarcassId = 1;
        }

        void TickWorld()
        {
            _worldTimer += Time.unscaledDeltaTime;
            if (_worldTimer < (IsHost ? HostTick : ClientTick)) return;
            float elapsed = _worldTimer;
            _worldTimer = 0f;

            RefreshFoodRegistry(elapsed);
            if (IsHost) HostTickFood(elapsed);
            else ClientTickFood();
        }

        // Give every food source in the scene an id (new plants streamed in, new carcasses...).
        void RefreshFoodRegistry(float elapsed)
        {
            var all = WorldRegistry.Food;
            _registryTimer += elapsed;
            if (all.Count == _registryCount && _registryTimer < 3f) return;
            _registryCount = all.Count;
            _registryTimer = 0f;

            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || _foodIds.ContainsKey(f) || _unsyncedFood.Contains(f)) continue;
                var comp = f as Component;
                var sync = f as INetSyncedFood;
                if (comp == null || sync == null) { _unsyncedFood.Add(f); continue; }

                if (f is Carcass)
                {
                    // Only the host hands out carcass ids. A carcass a client made by itself stays local.
                    if (!IsHost) { _unsyncedFood.Add(f); continue; }
                    int cid = NetProtocol.CarcassIdBit | _nextCarcassId++;
                    var entry = AddFood(cid, f, sync, comp, true);
                    SendCarcassSpawn(entry, ulong.MaxValue);
                    continue;
                }

                int id = NetProtocol.StaticFoodId(comp.transform.position, (int)f.FoodType);
                if (_food.ContainsKey(id)) { _unsyncedFood.Add(f); continue; } // two plants in the same spot
                AddFood(id, f, sync, comp, false);
            }

            if (_unsyncedFood.Count > 256) _unsyncedFood.RemoveWhere(x => (x as Object) == null);
        }

        FoodEntry AddFood(int id, IFoodSource f, INetSyncedFood sync, Component comp, bool carcass)
        {
            var e = new FoodEntry { Id = id, Source = f, Sync = sync, Obj = comp, IsCarcass = carcass, LastKnown = f.Remaining };
            _food[id] = e;
            _foodIds[f] = id;
            return e;
        }

        void RemoveFood(FoodEntry e)
        {
            _food.Remove(e.Id);
            _foodIds.Remove(e.Source);
        }

        // ------------------------------------------------------------------ host

        void HostTickFood(float elapsed)
        {
            _fullSyncTimer += elapsed;
            bool full = _fullSyncTimer >= FullSyncEvery;
            if (full) _fullSyncTimer = 0f;

            _dirty.Clear();
            _deadIds.Clear();
            foreach (var e in _food.Values)
            {
                if (e.Obj == null) { _deadIds.Add(e.Id); continue; }
                float now = e.Source.Remaining;
                if (full || Mathf.Abs(now - e.LastKnown) > 0.5f)
                {
                    e.LastKnown = now;
                    _dirty.Add(e);
                }
            }

            foreach (int id in _deadIds)
            {
                var e = _food[id];
                RemoveFood(e);
                if (e.IsCarcass) SendCarcassGone(id);
            }

            if (_peers.Count > 0) SendFoodStates(_dirty, ulong.MaxValue);
        }

        void SendFoodStates(List<FoodEntry> entries, ulong onlyTo)
        {
            for (int start = 0; start < entries.Count; start += FoodPerMessage)
            {
                int count = Mathf.Min(FoodPerMessage, entries.Count - start);
                var w = NewWriter(NetProtocol.FoodState, 8 + count * 8);
                try
                {
                    w.WriteValueSafe((byte)count);
                    for (int i = 0; i < count; i++)
                    {
                        var e = entries[start + i];
                        w.WriteValueSafe(e.Id);
                        w.WriteValueSafe(e.Source.Remaining);
                    }
                    if (onlyTo != ulong.MaxValue) SendTo(onlyTo, w, true);
                    else Broadcast(w, true);
                }
                finally { w.Dispose(); }
            }
        }

        void SendCarcassSpawn(FoodEntry e, ulong onlyTo)
        {
            if (e.Obj == null) return;
            Vector3 p = ((Component)e.Obj).transform.position;
            var w = NewWriter(NetProtocol.CarcassSpawn, 32);
            try
            {
                w.WriteValueSafe(e.Id);
                w.WriteValueSafe(p.x);
                w.WriteValueSafe(p.y);
                w.WriteValueSafe(p.z);
                w.WriteValueSafe(e.Source.Remaining);
                if (onlyTo != ulong.MaxValue) SendTo(onlyTo, w, true);
                else Broadcast(w, true);
            }
            finally { w.Dispose(); }
        }

        void SendCarcassGone(int id)
        {
            var w = NewWriter(NetProtocol.CarcassGone, 8);
            try
            {
                w.WriteValueSafe(id);
                Broadcast(w, true);
            }
            finally { w.Dispose(); }
        }

        /// <summary>Host: bring a new joiner up to date.</summary>
        void SendWorldTo(ulong clientId)
        {
            RefreshFoodRegistry(100f);
            var all = new List<FoodEntry>();
            foreach (var e in _food.Values)
            {
                if (e.Obj == null) continue;
                if (e.IsCarcass) SendCarcassSpawn(e, clientId);
                all.Add(e);
            }
            SendFoodStates(all, clientId);
        }

        void OnFoodBite(ref FastBufferReader r)
        {
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out float amount);
            if (float.IsNaN(amount) || amount <= 0f) return;
            if (!_food.TryGetValue(id, out var e) || e.Obj == null) return;
            e.Source.Bite(Mathf.Min(amount, 1000f), null);
            // The next host tick notices the change and tells everyone.
        }

        // ------------------------------------------------------------------ client

        void ClientTickFood()
        {
            _deadIds.Clear();
            float time = Time.unscaledTime;
            foreach (var e in _food.Values)
            {
                bool dead = e.Obj == null;
                // A carcass we just finished is already destroyed here, but its last bite still counts.
                float now = dead ? (e.IsCarcass ? 0f : e.LastKnown) : e.Source.Remaining;
                float eaten = e.LastKnown - now;
                if (eaten > 0.01f)
                {
                    SendFoodBite(e.Id, eaten);
                    e.IgnoreHostUntil = time + 1f; // don't let an older host number undo our bite on screen
                }
                e.LastKnown = now;
                if (dead) _deadIds.Add(e.Id);
            }
            foreach (int id in _deadIds) RemoveFood(_food[id]);
        }

        void SendFoodBite(int id, float amount)
        {
            var w = NewWriter(NetProtocol.FoodBite, 16);
            try
            {
                w.WriteValueSafe(id);
                w.WriteValueSafe(amount);
                SendToHost(w, true);
            }
            finally { w.Dispose(); }
        }

        void OnFoodState(ref FastBufferReader r)
        {
            r.ReadValueSafe(out byte count);
            float time = Time.unscaledTime;
            for (int i = 0; i < count; i++)
            {
                r.ReadValueSafe(out int id);
                r.ReadValueSafe(out float remaining);
                if (float.IsNaN(remaining)) continue;
                if (!_food.TryGetValue(id, out var e) || e.Obj == null) continue;
                if (time < e.IgnoreHostUntil) continue;
                e.Sync.NetSetRemaining(remaining);
                e.LastKnown = e.Obj == null ? 0f : e.Source.Remaining;
            }
        }

        void OnCarcassSpawn(ref FastBufferReader r)
        {
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out float x);
            r.ReadValueSafe(out float y);
            r.ReadValueSafe(out float z);
            r.ReadValueSafe(out float meat);
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) || float.IsNaN(meat) || meat < 1f) return;
            if ((id & NetProtocol.CarcassIdBit) == 0 || _food.ContainsKey(id)) return;
            var carcass = Carcass.Spawn(new Vector3(x, y, z), Mathf.Min(meat, 100000f));
            AddFood(id, carcass, carcass, carcass, true);
        }

        void OnCarcassGone(ref FastBufferReader r)
        {
            r.ReadValueSafe(out int id);
            if ((id & NetProtocol.CarcassIdBit) == 0) return;
            if (!_food.TryGetValue(id, out var e)) return;
            RemoveFood(e);
            if (e.Obj != null) Destroy(((Component)e.Obj).gameObject);
        }
    }
}
