using System;
using System.Collections.Generic;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;
using TileMatch.Model;

namespace TileMatch.Generation
{
    public sealed class AssignerSettings
    {
        public int KindCount = 6;
        public int Margin = 1;
        public int CloseBiasPercent = 55;
        public int UpperLayerWeight = 3;
    }

    public sealed class Assignment
    {
        public ushort[] Kinds;
        public int[] WitnessOrder;
        public int PeakTray;
        public int KindsUsed;
    }

    // Plays a removal order on the kind-less layout and deals kinds along it while simulating the tray, so the
    // order itself is a solution that never needs more than Capacity − Margin slots (§10.8).
    public sealed class KindAssigner
    {
        private const ushort Unassigned = ushort.MaxValue;

        public Assignment TryAssign(TileMatchLevel layout, AssignerSettings settings, ulong seed)
        {
            var sim = new Simulation(layout, settings, new Pcg32(seed));
            return sim.Run() ? sim.Result() : null;
        }

        private sealed class Simulation
        {
            private readonly TileMatchLevel _layout;
            private readonly AssignerSettings _settings;
            private readonly int _limit;
            private Pcg32 _rng;

            private readonly ushort[] _kinds;
            private readonly bool[] _removed;
            private readonly bool[] _onBoard;
            private readonly int[] _occupant;
            private readonly int[] _ice;
            private readonly int[] _partner;
            private readonly int[] _queueNext;
            private readonly int[] _openCount;
            private readonly int[] _groupsUsed;
            private readonly List<int> _order = new List<int>();
            private readonly List<int> _candidates = new List<int>();
            private CoverCounts _cover;
            private int _tray;
            private int _peak;
            private int _closed;

            public Simulation(TileMatchLevel layout, AssignerSettings settings, Pcg32 rng)
            {
                _layout = layout;
                _settings = settings;
                _rng = rng;
                _limit = layout.TrayCapacity - settings.Margin;
                var tiles = layout.TileCount;
                _kinds = new ushort[tiles];
                Array.Fill(_kinds, Unassigned);
                _removed = new bool[tiles];
                _onBoard = new bool[tiles];
                _occupant = new int[layout.SlotCount];
                Array.Fill(_occupant, -1);
                _ice = new int[tiles];
                _partner = new int[tiles];
                Array.Fill(_partner, -1);
                _queueNext = new int[layout.Spawners.Length];
                _openCount = new int[settings.KindCount];
                _groupsUsed = new int[settings.KindCount];

                var occupied = new bool[layout.SlotCount];
                for (var t = 0; t < layout.BoardTileCount; t++)
                {
                    _onBoard[t] = true;
                    _occupant[layout.SlotOf(t)] = t;
                    occupied[layout.SlotOf(t)] = true;
                }

                _cover = CoverCounts.Create(layout.Cover, occupied);
                foreach (var spec in layout.Modifiers)
                {
                    if (spec.Type == ModifierTypes.Frozen) _ice[spec.Tile] = Math.Max(1, spec.Value);
                    if (spec.Type == ModifierTypes.Linked)
                    {
                        _partner[spec.Tile] = spec.Value;
                        _partner[spec.Value] = spec.Tile;
                    }
                }
            }

            public bool Run()
            {
                while (_order.Count < _layout.TileCount)
                {
                    var tile = PickTile();
                    if (tile < 0) return false;
                    var partner = _partner[tile] >= 0 && !_removed[_partner[tile]] ? _partner[tile] : -1;
                    if (!Take(tile)) return false;
                    if (partner >= 0 && !Take(partner)) return false;
                }

                foreach (var count in _openCount)
                {
                    if (count != 0) return false;
                }

                return true;
            }

            public Assignment Result()
            {
                var used = 0;
                foreach (var groups in _groupsUsed) if (groups > 0) used++;
                return new Assignment { Kinds = _kinds, WitnessOrder = _order.ToArray(), PeakTray = _peak, KindsUsed = used };
            }

            private int PickTile()
            {
                _candidates.Clear();
                var totalWeight = 0;
                for (var t = 0; t < _layout.TileCount; t++)
                {
                    if (!Available(t)) continue;
                    var partner = _partner[t];
                    if (partner >= 0 && !_removed[partner] && (!Available(partner) || _tray + 2 > _layout.TrayCapacity)) continue;
                    _candidates.Add(t);
                    totalWeight += Weight(t);
                }

                if (_candidates.Count == 0) return -1;
                var roll = _rng.NextInt(totalWeight);
                foreach (var t in _candidates)
                {
                    roll -= Weight(t);
                    if (roll < 0) return t;
                }

                return _candidates[_candidates.Count - 1];
            }

            private bool Available(int t) => _onBoard[t] && !_removed[t] && _cover.Covering[_layout.SlotOf(t)] == 0 && _ice[t] <= _closed;

            private int Weight(int t) => 1 + _layout.Layout.CoordOf(_layout.SlotOf(t)).Layer * _settings.UpperLayerWeight;

            private bool Take(int tile)
            {
                var kind = ChooseKind();
                if (kind < 0) return false;
                _kinds[tile] = (ushort)kind;
                _order.Add(tile);
                Remove(tile);

                if (_openCount[kind] == 0) _groupsUsed[kind]++;
                _openCount[kind]++;
                _tray++;
                if (_openCount[kind] == 3)
                {
                    _openCount[kind] = 0;
                    _tray -= 3;
                    _closed++;
                }

                _peak = Math.Max(_peak, _tray);
                return _tray <= _limit;
            }

            private int ChooseKind()
            {
                var remaining = _layout.TileCount - _order.Count;
                var needed = 0;
                var open = 0;
                for (var k = 0; k < _openCount.Length; k++)
                {
                    if (_openCount[k] == 0) continue;
                    needed += 3 - _openCount[k];
                    open++;
                }

                var mustContinue = open > 0 && remaining <= needed;
                var canOpen = !mustContinue && _tray + 1 <= _limit && remaining - 1 >= needed + 2;
                var preferClose = open > 0 && (_tray >= _limit - 1 || _rng.Chance(_settings.CloseBiasPercent, 100));

                if (open > 0 && (mustContinue || !canOpen || preferClose)) return PickOpenKind();
                return canOpen ? PickNewKind() : -1;
            }

            private int PickOpenKind()
            {
                var best = -1;
                var ties = 0;
                for (var k = 0; k < _openCount.Length; k++)
                {
                    if (_openCount[k] == 0) continue;
                    if (best < 0 || _openCount[k] > _openCount[best])
                    {
                        best = k;
                        ties = 1;
                    }
                    else if (_openCount[k] == _openCount[best] && _rng.NextInt(++ties) == 0)
                    {
                        best = k;
                    }
                }

                return best;
            }

            private int PickNewKind()
            {
                var best = -1;
                var ties = 0;
                for (var k = 0; k < _openCount.Length; k++)
                {
                    if (_openCount[k] != 0) continue;
                    if (best < 0 || _groupsUsed[k] < _groupsUsed[best])
                    {
                        best = k;
                        ties = 1;
                    }
                    else if (_groupsUsed[k] == _groupsUsed[best] && _rng.NextInt(++ties) == 0)
                    {
                        best = k;
                    }
                }

                return best;
            }

            private void Remove(int tile)
            {
                _removed[tile] = true;
                _onBoard[tile] = false;
                var slot = _layout.SlotOf(tile);
                _occupant[slot] = -1;

                var spawners = _layout.Spawners;
                for (var i = 0; i < spawners.Length; i++)
                {
                    if (spawners[i].Slot != slot || _queueNext[i] >= spawners[i].Queue.Length) continue;
                    var next = spawners[i].Queue[_queueNext[i]++];
                    _onBoard[next] = true;
                    _occupant[slot] = next;
                    return;
                }

                Span<int> uncovered = stackalloc int[_layout.Cover.MaxCovers];
                _cover.OnVacated(_layout.Cover, slot, uncovered);
            }
        }
    }
}
