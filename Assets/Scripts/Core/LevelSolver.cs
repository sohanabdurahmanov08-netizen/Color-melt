using System.Collections.Generic;

namespace ColorMelt.Core
{
    /// <summary>
    /// Breadth-first search over FlowModel states. Finds the shortest list of
    /// pours that melts every block. Levels are small (a few routes), so the
    /// search is instant and can run at runtime for hints.
    /// </summary>
    public static class LevelSolver
    {
        private const int MaxStates = 200000;

        public static List<Move> Solve(LevelData level)
        {
            var model = new FlowModel(level);
            model.ResolveAll();
            return Solve(model);
        }

        /// <summary>Shortest solution from the given state, or null if none exists.</summary>
        public static List<Move> Solve(FlowModel start)
        {
            if (start.AllMelted)
                return new List<Move>();

            // States are kept as StateKeys and replayed on one scratch model,
            // so the search allocates almost nothing (hints run on the phone).
            var model = start.Clone();
            var startKey = model.StateKey();
            var parents = new Dictionary<ulong, Step> { { startKey, default } };
            var queue = new Queue<ulong>();
            queue.Enqueue(startKey);
            var routes = model.RouteCount;
            var targets = new int[routes];

            while (queue.Count > 0 && parents.Count < MaxStates)
            {
                var key = queue.Dequeue();
                model.LoadStateKey(key);
                for (var route = 0; route < routes; route++)
                    targets[route] = model.TargetOf(route);

                for (var from = 0; from < routes; from++)
                {
                    for (var to = from - 1; to <= from + 1; to++)
                    {
                        if (to < 0 || to >= routes || targets[from] == to)
                            continue;

                        model.LoadStateKey(key);
                        model.Pour(from, to);
                        model.ResolveAll();

                        var next = model.StateKey();
                        if (parents.ContainsKey(next))
                            continue;

                        parents.Add(next, new Step(key, new Move(from, to)));
                        if (model.AllMelted)
                            return Path(parents, startKey, next);

                        queue.Enqueue(next);
                    }
                }
            }

            return null;
        }

        private static List<Move> Path(Dictionary<ulong, Step> parents, ulong startKey, ulong goal)
        {
            var path = new List<Move>();
            for (var key = goal; key != startKey; key = parents[key].parent)
                path.Add(parents[key].move);
            path.Reverse();
            return path;
        }

        private readonly struct Step
        {
            public readonly ulong parent;
            public readonly Move move;

            public Step(ulong parent, Move move)
            {
                this.parent = parent;
                this.move = move;
            }
        }
    }
}
