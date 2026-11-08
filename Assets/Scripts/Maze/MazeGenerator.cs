using System.Collections.Generic;

namespace NullFrontier.Maze
{
    /// <summary>
    /// Recursive-backtracker maze on an odd-dimension grid. Returns a wall map
    /// (true = wall); entrance is cell (0,1) and exit cell (width-1, height-2), both
    /// guaranteed connected to the carved path. Pure static — unit-testable.
    /// </summary>
    public static class MazeGenerator
    {
        public static bool[,] Generate(int width, int height, System.Random rng)
        {
            if (width < 3) width = 3;
            if (height < 3) height = 3;
            if (width % 2 == 0) width++;
            if (height % 2 == 0) height++;
            if (rng == null) rng = new System.Random(0);

            var walls = new bool[width, height];
            var visited = new bool[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                walls[x, y] = true;

            var stack = new Stack<(int x, int y)>();
            visited[1, 1] = true;
            walls[1, 1] = false;
            stack.Push((1, 1));

            while (stack.Count > 0)
            {
                var (cx, cy) = stack.Peek();
                var neighbors = GetUnvisitedNeighbors(cx, cy, width, height, visited);
                if (neighbors.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                var (nx, ny) = neighbors[rng.Next(neighbors.Count)];
                walls[(cx + nx) / 2, (cy + ny) / 2] = false;
                walls[nx, ny] = false;
                visited[nx, ny] = true;
                stack.Push((nx, ny));
            }

            walls[0, 1] = false;
            walls[width - 1, height - 2] = false;
            return walls;
        }

        private static List<(int x, int y)> GetUnvisitedNeighbors(int x, int y,
            int width, int height, bool[,] visited)
        {
            var result = new List<(int x, int y)>(4);
            if (x - 2 >= 1 && !visited[x - 2, y]) result.Add((x - 2, y));
            if (x + 2 < width - 1 && !visited[x + 2, y]) result.Add((x + 2, y));
            if (y - 2 >= 1 && !visited[x, y - 2]) result.Add((x, y - 2));
            if (y + 2 < height - 1 && !visited[x, y + 2]) result.Add((x, y + 2));
            return result;
        }
    }
}
