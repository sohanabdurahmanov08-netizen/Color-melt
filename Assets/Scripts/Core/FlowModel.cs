using System.Collections.Generic;

namespace ColorMelt.Core
{
    public readonly struct BlockRef
    {
        public readonly int route;
        public readonly int index;

        public BlockRef(int route, int index)
        {
            this.route = route;
            this.index = index;
        }
    }

    public readonly struct Move
    {
        public readonly int from;
        public readonly int to;

        public Move(int from, int to)
        {
            this.from = from;
            this.to = to;
        }

        public override string ToString() => $"{from}->{to}";
    }

    /// <summary>
    /// Pure game rules, shared by the running level, the solver, hints and
    /// the tutorial so they can never disagree.
    ///
    /// Every route has a source that pours into its own channel or into a
    /// neighbouring one. A channel's paint is the mix of every source pouring
    /// into it. Paint runs down the channel until the first intact block; if
    /// the paint has exactly the block's colour the block melts. After any
    /// melt all sources return to their own channels.
    /// </summary>
    public sealed class FlowModel
    {
        private readonly ColorType[] _sources;
        private readonly ColorType[][] _blocks;
        private readonly int[] _targets;
        private readonly bool[][] _melted;
        private readonly ColorType[] _channelColors;

        public int RouteCount => _sources.Length;

        public FlowModel(LevelData level)
        {
            var count = level.routes.Count;
            _sources = new ColorType[count];
            _blocks = new ColorType[count][];
            _targets = new int[count];
            _melted = new bool[count][];
            _channelColors = new ColorType[count];

            for (var route = 0; route < count; route++)
            {
                var data = level.routes[route];
                _sources[route] = data.color;
                _targets[route] = route;
                _blocks[route] = new ColorType[data.blocks.Count];
                _melted[route] = new bool[data.blocks.Count];
                for (var block = 0; block < data.blocks.Count; block++)
                    _blocks[route][block] = data.blocks[block].color;
            }

            Recalculate();
        }

        private FlowModel(FlowModel other)
        {
            _sources = other._sources;
            _blocks = other._blocks;
            _targets = (int[])other._targets.Clone();
            _melted = new bool[other._melted.Length][];
            for (var route = 0; route < _melted.Length; route++)
                _melted[route] = (bool[])other._melted[route].Clone();
            _channelColors = (ColorType[])other._channelColors.Clone();
        }

        public FlowModel Clone() => new FlowModel(this);

        public ColorType SourceColor(int route) => _sources[route];
        public ColorType ChannelColor(int route) => _channelColors[route];
        public int TargetOf(int route) => _targets[route];
        public int BlockCount(int route) => _blocks[route].Length;
        public ColorType BlockColor(BlockRef block) => _blocks[block.route][block.index];
        public bool IsMelted(BlockRef block) => _melted[block.route][block.index];

        public bool AllMelted
        {
            get
            {
                foreach (var route in _melted)
                    foreach (var melted in route)
                        if (!melted)
                            return false;
                return true;
            }
        }

        /// <summary>Index of the block that currently stops the paint, or -1.</summary>
        public int FirstIntactBlock(int route)
        {
            var melted = _melted[route];
            for (var block = 0; block < melted.Length; block++)
                if (!melted[block])
                    return block;
            return -1;
        }

        public bool CanPour(int from, int to) =>
            from >= 0 && from < RouteCount && to >= 0 && to < RouteCount && System.Math.Abs(from - to) <= 1;

        /// <summary>Points a source at a channel. Returns false if nothing changed.</summary>
        public bool Pour(int from, int to)
        {
            if (!CanPour(from, to) || _targets[from] == to)
                return false;

            _targets[from] = to;
            Recalculate();
            return true;
        }

        public void ResetTargets()
        {
            for (var route = 0; route < _targets.Length; route++)
                _targets[route] = route;
            Recalculate();
        }

        public void Recalculate()
        {
            for (var route = 0; route < _channelColors.Length; route++)
                _channelColors[route] = ColorType.None;

            for (var source = 0; source < _sources.Length; source++)
            {
                var target = _targets[source];
                _channelColors[target] = _channelColors[target].Mix(_sources[source]);
            }
        }

        /// <summary>Blocks that the current paint reaches and matches.</summary>
        public List<BlockRef> FindMeltable()
        {
            var result = new List<BlockRef>();
            for (var route = 0; route < RouteCount; route++)
            {
                var block = FirstIntactBlock(route);
                if (block >= 0 && !_channelColors[route].IsEmpty() && _channelColors[route] == _blocks[route][block])
                    result.Add(new BlockRef(route, block));
            }
            return result;
        }

        public void Melt(BlockRef block) => _melted[block.route][block.index] = true;

        /// <summary>
        /// Melts everything reachable, resetting sources after each wave, until
        /// the board is stable. Used by the solver; the running level plays the
        /// same steps with animations in between.
        /// </summary>
        public int ResolveAll()
        {
            var total = 0;
            while (true)
            {
                var meltable = FindMeltable();
                if (meltable.Count == 0)
                    return total;

                foreach (var block in meltable)
                    Melt(block);
                total += meltable.Count;
                ResetTargets();
            }
        }

        /// <summary>Compact state key: source targets plus melted blocks.</summary>
        public ulong StateKey()
        {
            ulong key = 0;
            for (var route = 0; route < _targets.Length; route++)
                key = key * 3 + (ulong)(_targets[route] - route + 1);
            foreach (var route in _melted)
                foreach (var melted in route)
                    key = key * 2 + (melted ? 1UL : 0UL);
            return key;
        }
    }
}
