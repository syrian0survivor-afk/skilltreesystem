using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SkillTreeSystem
{
    public class SkillTreeGridBuilder : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private TextAsset jsonAsset;
        [SerializeField] private int treeIndex;

        [Header("Grid")]
        [SerializeField] private RectTransform gridRoot;
        [SerializeField] private float cellSize = 64f;
        [SerializeField] private float uiUnitsPerCell = 100f;
        [SerializeField] private Vector2 gridPadding = new Vector2(2f, 2f);

        [Header("Visuals")]
        [SerializeField] private Sprite pipeStraightSprite;
        [SerializeField] private Sprite pipeElbowSprite;
        [SerializeField] private Sprite pipeTeeSprite;
        [SerializeField] private Color pipeColor = new Color(0.8f, 0.1f, 0.1f, 0.8f);
        [SerializeField] private Color junctionColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color inactiveSkillColor = new Color(0.7f, 0.1f, 0.1f, 1f);
        [SerializeField] private Color activeSkillColor = new Color(0.2f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color rootSkillColor = new Color(1f, 0.9f, 0.2f, 1f);

        [Header("Runtime")]
        [SerializeField] private List<string> purchasedSkillIds = new List<string>();

        private readonly Dictionary<Vector2Int, GridCell> gridCells = new Dictionary<Vector2Int, GridCell>();
        private readonly Dictionary<string, SkillNodeInstance> nodeInstances = new Dictionary<string, SkillNodeInstance>();
        private Vector2Int rootGridPosition;

        private void Start()
        {
            Build();
        }

        public void Build()
        {
            if (jsonAsset == null || gridRoot == null)
            {
                Debug.LogError("Missing jsonAsset or gridRoot reference.");
                return;
            }

            ClearGrid();

            SkillTreeExport export = JsonUtility.FromJson<SkillTreeExport>(jsonAsset.text);
            if (export == null || export.trees == null || export.trees.Count == 0)
            {
                Debug.LogError("Failed to parse skill tree JSON.");
                return;
            }

            SkillTreeDefinition tree = export.trees[Mathf.Clamp(treeIndex, 0, export.trees.Count - 1)];
            Dictionary<string, SkillTreeNode> nodeLookup = tree.nodes.ToDictionary(node => node.id, node => node);

            Vector2 minUi = new Vector2(float.MaxValue, float.MaxValue);
            foreach (SkillTreeNode node in tree.nodes)
            {
                minUi.x = Mathf.Min(minUi.x, node.ui.x);
                minUi.y = Mathf.Min(minUi.y, node.ui.y);
            }

            foreach (SkillTreeNode node in tree.nodes)
            {
                Vector2Int gridPos = UiToGrid(node.ui, minUi);
                CreateNodeCell(node, gridPos, tree.rootNodeId);
            }

            foreach (SkillTreeEdge edge in tree.edges)
            {
                if (edge.endpoints == null || edge.endpoints.Count < 2)
                {
                    continue;
                }

                if (!nodeLookup.TryGetValue(edge.endpoints[0], out SkillTreeNode startNode) ||
                    !nodeLookup.TryGetValue(edge.endpoints[1], out SkillTreeNode endNode))
                {
                    continue;
                }

                Vector2Int start = UiToGrid(startNode.ui, minUi);
                Vector2Int end = UiToGrid(endNode.ui, minUi);
                BuildPipePath(start, end);
            }

            BuildPipeVisuals();
            EvaluateSkillActivation(tree.rootNodeId);
        }

        private void ClearGrid()
        {
            foreach (Transform child in gridRoot)
            {
                Destroy(child.gameObject);
            }

            gridCells.Clear();
            nodeInstances.Clear();
        }

        private Vector2Int UiToGrid(SkillTreeUiRect rect, Vector2 minUi)
        {
            int x = Mathf.RoundToInt((rect.x - minUi.x) / uiUnitsPerCell);
            int y = Mathf.RoundToInt((rect.y - minUi.y) / uiUnitsPerCell);
            return new Vector2Int(x, y);
        }

        private void CreateNodeCell(SkillTreeNode node, Vector2Int gridPos, string rootId)
        {
            GridCell cell = GetOrCreateCell(gridPos);
            cell.Node = node;
            cell.Type = DetermineCellType(node, rootId);

            SkillNodeInstance instance = new SkillNodeInstance
            {
                NodeId = node.id,
                Cell = cell,
                Purchased = node.id == rootId || purchasedSkillIds.Contains(node.id)
            };

            cell.IsTraversable = true;

            if (node.id == rootId)
            {
                rootGridPosition = gridPos;
            }

            nodeInstances[node.id] = instance;
        }

        private static GridCellType DetermineCellType(SkillTreeNode node, string rootId)
        {
            if (node.id == rootId)
            {
                return GridCellType.RootSkill;
            }

            if (!string.IsNullOrEmpty(node.type) && node.type.StartsWith("skill", StringComparison.OrdinalIgnoreCase))
            {
                return GridCellType.Skill;
            }

            if (string.Equals(node.type, "root", StringComparison.OrdinalIgnoreCase))
            {
                return GridCellType.RootSkill;
            }

            return GridCellType.Junction;
        }

        private void BuildPipePath(Vector2Int start, Vector2Int end)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int current = start;

            while (current.x != end.x)
            {
                current.x += Math.Sign(end.x - current.x);
                path.Add(current);
            }

            while (current.y != end.y)
            {
                current.y += Math.Sign(end.y - current.y);
                path.Add(current);
            }

            Vector2Int previous = start;
            foreach (Vector2Int step in path)
            {
                AddConnection(previous, step);
                previous = step;
            }
        }

        private void AddConnection(Vector2Int from, Vector2Int to)
        {
            GridCell fromCell = GetOrCreateCell(from);
            GridCell toCell = GetOrCreateCell(to);
            Vector2Int delta = to - from;

            Direction direction = DirectionFromDelta(delta);
            fromCell.AddConnection(direction);
            toCell.AddConnection(direction.Opposite());

            fromCell.IsTraversable = true;
            toCell.IsTraversable = true;
        }

        private void BuildPipeVisuals()
        {
            foreach (KeyValuePair<Vector2Int, GridCell> cellPair in gridCells)
            {
                GridCell cell = cellPair.Value;
                if (cell.Type == GridCellType.Empty && cell.ConnectionCount > 0)
                {
                    cell.Type = GridCellType.Pipe;
                }

                if (cell.Type == GridCellType.Empty)
                {
                    continue;
                }

                GameObject cellObject = new GameObject($"Cell_{cellPair.Key.x}_{cellPair.Key.y}", typeof(RectTransform), typeof(Image));
                cellObject.transform.SetParent(gridRoot, false);

                RectTransform rect = cellObject.GetComponent<RectTransform>();
                rect.sizeDelta = Vector2.one * cellSize;
                rect.anchoredPosition = new Vector2(
                    (cellPair.Key.x + gridPadding.x) * cellSize,
                    (cellPair.Key.y + gridPadding.y) * cellSize);

                Image image = cellObject.GetComponent<Image>();
                if (cell.Type == GridCellType.Pipe)
                {
                    ApplyPipeVisual(cell, image, rect);
                }
                else
                {
                    image.color = cell.Type switch
                    {
                        GridCellType.RootSkill => rootSkillColor,
                        GridCellType.Skill => inactiveSkillColor,
                        GridCellType.Junction => junctionColor,
                        _ => Color.white
                    };
                }

                if (cell.Type == GridCellType.Skill || cell.Type == GridCellType.RootSkill)
                {
                    if (nodeInstances.TryGetValue(cell.Node.id, out SkillNodeInstance instance))
                    {
                        instance.Image = image;
                    }
                }
            }
        }

        private void ApplyPipeVisual(GridCell cell, Image image, RectTransform rect)
        {
            PipeVisual pipeVisual = GetPipeVisual(cell);
            image.sprite = pipeVisual.Sprite;
            image.color = pipeColor;
            rect.localRotation = Quaternion.Euler(0f, 0f, pipeVisual.Rotation);
        }

        private PipeVisual GetPipeVisual(GridCell cell)
        {
            int connectionCount = cell.ConnectionCount;
            if (connectionCount == 2 && cell.HasOppositeConnections())
            {
                return new PipeVisual
                {
                    Sprite = pipeStraightSprite,
                    Rotation = cell.HasConnection(Direction.Left) ? 90f : 0f
                };
            }

            if (connectionCount == 2)
            {
                return new PipeVisual
                {
                    Sprite = pipeElbowSprite,
                    Rotation = GetElbowRotation(cell)
                };
            }

            return new PipeVisual
            {
                Sprite = pipeTeeSprite,
                Rotation = GetTeeRotation(cell)
            };
        }

        private float GetElbowRotation(GridCell cell)
        {
            bool up = cell.HasConnection(Direction.Up);
            bool right = cell.HasConnection(Direction.Right);
            bool down = cell.HasConnection(Direction.Down);
            bool left = cell.HasConnection(Direction.Left);

            if (up && right) return 0f;
            if (right && down) return 270f;
            if (down && left) return 180f;
            if (left && up) return 90f;
            return 0f;
        }

        private float GetTeeRotation(GridCell cell)
        {
            bool up = cell.HasConnection(Direction.Up);
            bool right = cell.HasConnection(Direction.Right);
            bool down = cell.HasConnection(Direction.Down);
            bool left = cell.HasConnection(Direction.Left);

            if (!up) return 180f;
            if (!right) return 90f;
            if (!down) return 0f;
            if (!left) return 270f;
            return 0f;
        }

        private void EvaluateSkillActivation(string rootId)
        {
            if (!nodeInstances.TryGetValue(rootId, out SkillNodeInstance root))
            {
                return;
            }

            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();

            queue.Enqueue(rootGridPosition);
            visited.Add(rootGridPosition);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                GridCell cell = gridCells[current];

                foreach (Direction dir in cell.Connections)
                {
                    Vector2Int neighborPos = current + dir.ToVector2Int();
                    if (!gridCells.TryGetValue(neighborPos, out GridCell neighbor))
                    {
                        continue;
                    }

                    if (!neighbor.IsTraversable || !neighbor.HasConnection(dir.Opposite()))
                    {
                        continue;
                    }

                    if (visited.Add(neighborPos))
                    {
                        queue.Enqueue(neighborPos);
                    }
                }
            }

            foreach (SkillNodeInstance instance in nodeInstances.Values)
            {
                bool isReachable = visited.Contains(instance.Cell.GridPosition);
                bool isActive = isReachable && instance.Purchased;
                if (instance.Image != null)
                {
                    instance.Image.color = isActive ? activeSkillColor : inactiveSkillColor;
                }
            }

            if (root.Image != null)
            {
                root.Image.color = rootSkillColor;
            }
        }

        private GridCell GetOrCreateCell(Vector2Int gridPosition)
        {
            if (!gridCells.TryGetValue(gridPosition, out GridCell cell))
            {
                cell = new GridCell(gridPosition);
                gridCells[gridPosition] = cell;
            }

            return cell;
        }

        private static Direction DirectionFromDelta(Vector2Int delta)
        {
            if (delta == Vector2Int.up) return Direction.Up;
            if (delta == Vector2Int.right) return Direction.Right;
            if (delta == Vector2Int.down) return Direction.Down;
            if (delta == Vector2Int.left) return Direction.Left;
            throw new ArgumentException($"Invalid delta: {delta}");
        }

        private class GridCell
        {
            private readonly HashSet<Direction> connections = new HashSet<Direction>();

            public GridCell(Vector2Int gridPosition)
            {
                GridPosition = gridPosition;
            }

            public Vector2Int GridPosition { get; }
            public SkillTreeNode Node { get; set; }
            public GridCellType Type { get; set; } = GridCellType.Empty;
            public bool IsTraversable { get; set; }

            public IEnumerable<Direction> Connections => connections;
            public int ConnectionCount => connections.Count;

            public void AddConnection(Direction direction)
            {
                connections.Add(direction);
            }

            public bool HasConnection(Direction direction)
            {
                return connections.Contains(direction);
            }

            public bool HasOppositeConnections()
            {
                return (HasConnection(Direction.Left) && HasConnection(Direction.Right)) ||
                       (HasConnection(Direction.Up) && HasConnection(Direction.Down));
            }
        }

        private class SkillNodeInstance
        {
            public string NodeId;
            public GridCell Cell;
            public bool Purchased;
            public Image Image;
        }

        private struct PipeVisual
        {
            public Sprite Sprite;
            public float Rotation;
        }

        private enum GridCellType
        {
            Empty,
            Pipe,
            Junction,
            Skill,
            RootSkill
        }

    }

    public enum Direction
    {
        Up,
        Right,
        Down,
        Left
    }

    public static class DirectionExtensions
    {
        public static Direction Opposite(this Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Down,
                Direction.Right => Direction.Left,
                Direction.Down => Direction.Up,
                _ => Direction.Right
            };
        }

        public static Vector2Int ToVector2Int(this Direction direction)
        {
            return direction switch
            {
                Direction.Up => Vector2Int.up,
                Direction.Right => Vector2Int.right,
                Direction.Down => Vector2Int.down,
                _ => Vector2Int.left
            };
        }
    }
}
