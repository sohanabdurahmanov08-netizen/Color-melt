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

            var visited = new HashSet<ulong> { start.StateKey() };
            var queue = new Queue<Node>();
            queue.Enqueue(new Node(start, null, default));

            while (queue.Count > 0 && visited.Count < MaxStates)
            {
                var node = queue.Dequeue();
                for (var from = 0; from < node.model.RouteCount; from++)
                {
                    for (var to = from - 1; to <= from + 1; to++)
                    {
                        if (!node.model.CanPour(from, to) || node.model.TargetOf(from) == to)
                            continue;

                        var next = node.model.Clone();
                        next.Pour(from, to);
                        next.ResolveAll();

                        if (!visited.Add(next.StateKey()))
                            continue;

                        var child = new Node(next, node, new Move(from, to));
                        if (next.AllMelted)
                            return child.Path();

                        queue.Enqueue(child);
                    }
                }
            }

            return null;
        }

        private sealed class Node
        {
            public readonly FlowModel model;
            private readonly Node _parent;
            private readonly Move _move;

            public Node(FlowModel model, Node parent, Move move)
            {
                this.model = model;
                _parent = parent;
                _move = move;
            }

            public List<Move> Path()
            {
                var path = new List<Move>();
                for (var node = this; node._parent != null; node = node._parent)
                    path.Add(node._move);
                path.Reverse();
                return path;
            }
        }
    }
}
