using System;
using UnityEngine;

namespace NullFrontier.Maze
{
    /// <summary>
    /// Instantiates a generated maze grid: one wall cube per wall cell (parented),
    /// a single scaled floor, and greybox primitives when no prefabs are assigned.
    /// ExitWorldPosition is the world center of the exit cell.
    /// </summary>
    public class MazeBuilder : MonoBehaviour
    {
        [SerializeField] private float cellSize = 4f;
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private GameObject floorPrefab;

        public event Action OnMazeBuilt;

        public float CellSize => cellSize;
        public bool[,] Grid { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public Vector3 ExitWorldPosition { get; private set; }
        public Vector3 EntranceWorldPosition { get; private set; }

        private Transform _container;
        private Material _greyboxMaterial;

        public void Build(int width, int height, System.Random rng)
        {
            Grid = MazeGenerator.Generate(width, height, rng);
            Width = Grid.GetLength(0);
            Height = Grid.GetLength(1);
            Rebuild();
        }

        public void Rebuild()
        {
            if (Grid == null) return;

            if (_container != null) Destroy(_container.gameObject);
            _container = new GameObject("MazeContainer").transform;
            _container.SetParent(transform, false);

            bool useGreyboxWalls = wallPrefab == null;
            bool useGreyboxFloor = floorPrefab == null;

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                if (!Grid[x, y]) continue;
                SpawnPiece(wallPrefab, CellToWorld(x, y), Quaternion.identity,
                    new Vector3(cellSize, cellSize, cellSize), useGreyboxWalls, "GreyboxWall");
            }

            var floorPos = new Vector3((Width - 1) * cellSize * 0.5f, -cellSize * 0.5f, (Height - 1) * cellSize * 0.5f);
            var floorScale = new Vector3(Width * cellSize, cellSize * 0.1f, Height * cellSize);
            SpawnPiece(floorPrefab, floorPos, Quaternion.identity, floorScale, useGreyboxFloor, "GreyboxFloor");

            ExitWorldPosition = CellToWorld(Width - 1, Height - 2);
            EntranceWorldPosition = CellToWorld(0, 1);

            OnMazeBuilt?.Invoke();
        }

        private GameObject SpawnPiece(GameObject prefab, Vector3 position, Quaternion rotation,
            Vector3 scale, bool greybox, string greyboxName)
        {
            GameObject piece;
            if (prefab != null)
            {
                piece = Instantiate(prefab, position, rotation, _container);
                piece.transform.localScale = scale;
            }
            else
            {
                piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = greyboxName;
                piece.transform.SetParent(_container, false);
                piece.transform.SetPositionAndRotation(position, rotation);
                piece.transform.localScale = scale;
                var renderer = piece.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (_greyboxMaterial == null)
                    {
                        _greyboxMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.grey };
                    }
                    renderer.sharedMaterial = _greyboxMaterial;
                }
            }
            return piece;
        }

        public Vector3 CellToWorld(int x, int y) =>
            transform.position + new Vector3(x * cellSize, 0f, y * cellSize);
    }
}
