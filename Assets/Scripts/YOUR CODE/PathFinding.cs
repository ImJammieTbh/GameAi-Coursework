using System.Collections.Generic;
using UnityEngine;

namespace YOUR_CODE
{
    /// <summary>
    /// A* pathfinding on the map grid. One grid cell = one world unit.
    ///
    /// How it works, in plain English:
    ///   1. Start at the start cell. Keep a list of "cells still to look at" (the open list).
    ///   2. Always look at the cell that seems most promising next: the lowest
    ///      (cost so far) + (estimated cost still to go).
    ///   3. For each neighbour, work out the cost to reach it through this cell. If that is
    ///      cheaper than any route found so far, remember it and add it to the open list.
    ///   4. Stop when the goal cell is picked. Walk the "came from" links backwards to get the path.
    ///
    /// Water and mud cost more than normal ground (agents move slower there), so paths avoid them
    /// when a reasonable detour exists.
    /// </summary>
    public static class PathFinding
    {
        // ------------------------------------------------------------------ constants

        /// <summary>Cost value that means "can't walk here" (wall or outside the map)</summary>
        private const float BlockedCellCost = -1f;

        /// <summary>Moving diagonally is sqrt(2) times as far as moving straight</summary>
        private const float DiagonalStepCost = 1.41421356f;

        /// <summary>How far apart (world units) the sample points are when checking a straight line</summary>
        private const float LineCheckSpacing = 0.25f;

        /// <summary>How far to each side of the line we also check, so the agent's width fits. Just under the agent radius (0.45)</summary>
        private const float LineCheckSideOffset = 0.4f;

        /// <summary>When the goal is inside a wall, how many cells out we search for a free cell instead</summary>
        private const int MaxSearchRadiusForFreeCell = 6;

        // ------------------------------------------------------------------ map data (cached)

        private static Map gameMap;
        private static int mapWidth;
        private static int mapHeight;

        /// <summary>Movement cost per cell: BlockedCellCost, 1 for normal ground, higher for mud/water</summary>
        private static float[] movementCostPerCell;

        /// <summary>Whether movementCostPerCell has been worked out yet for this cell (it is filled in lazily)</summary>
        private static bool[] isCostCached;

        // ------------------------------------------------------------------ per-search data (reused every search)

        /// <summary>Cheapest known cost to reach each cell from the start</summary>
        private static float[] costFromStart;

        /// <summary>For each cell, the cell we came from on the cheapest known route</summary>
        private static int[] cameFromCell;

        // Instead of clearing the arrays above every search (slow), each search gets a new id number.
        // A value only counts as valid if its id matches the current search.
        private static int[] costValidForSearchId;
        private static int[] closedInSearchId;
        private static int currentSearchId;

        /// <summary>Cells waiting to be looked at, lowest estimated total cost first</summary>
        private static readonly CellPriorityQueue openCells = new CellPriorityQueue();

        /// <summary>Temporary list used while straightening a path</summary>
        private static readonly List<Vector3> straightenedPathBuffer = new List<Vector3>();

        // The 8 directions to a neighbour. The first 4 are straight, the last 4 are diagonal.
        private static readonly int[] NeighbourOffsetX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] NeighbourOffsetY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        private const int FirstDiagonalNeighbourIndex = 4;

        // ------------------------------------------------------------------ public functions

        /// <summary>
        /// Finds a path from startPosition to goalPosition.
        /// Fills resultWaypoints with world positions to walk through (the start is not included).
        /// Returns false if there is no route.
        /// </summary>
        /// <param name="startPosition">World position to start from</param>
        /// <param name="goalPosition">World position to get to</param>
        /// <param name="resultWaypoints">List that gets cleared and filled with the path</param>
        /// <param name="straightenPath">If true, remove unnecessary waypoints so the path is less jagged</param>
        public static bool FindPath(Vector3 startPosition, Vector3 goalPosition, List<Vector3> resultWaypoints, bool straightenPath = true)
        {
            resultWaypoints.Clear();
            EnsureGridIsReady();

            int startX = Mathf.Clamp(Mathf.FloorToInt(startPosition.x), 0, mapWidth - 1);
            int startY = Mathf.Clamp(Mathf.FloorToInt(startPosition.y), 0, mapHeight - 1);
            int goalX = Mathf.Clamp(Mathf.FloorToInt(goalPosition.x), 0, mapWidth - 1);
            int goalY = Mathf.Clamp(Mathf.FloorToInt(goalPosition.y), 0, mapHeight - 1);

            // If the goal is inside a wall, aim for the closest free cell instead
            bool goalIsBlocked = GetCellMovementCost(goalX, goalY) < 0f;
            if (goalIsBlocked && !FindNearestWalkableCell(ref goalX, ref goalY))
            {
                return false;
            }

            int startCell = startY * mapWidth + startX;
            int goalCell = goalY * mapWidth + goalX;
            if (startCell == goalCell)
            {
                return true; // already there, so the path is empty
            }

            // Start a new search
            currentSearchId++;
            costFromStart[startCell] = 0f;
            costValidForSearchId[startCell] = currentSearchId;
            cameFromCell[startCell] = -1;

            openCells.Clear();
            openCells.Add(EstimateDistanceToGoal(startX, startY, goalX, goalY), startCell);

            while (openCells.Count > 0)
            {
                int currentCell = openCells.RemoveLowest();

                // The same cell can be added more than once (when a cheaper route is found later).
                // Ignore it if we have already finished with it.
                if (closedInSearchId[currentCell] == currentSearchId) continue;
                closedInSearchId[currentCell] = currentSearchId;

                if (currentCell == goalCell)
                {
                    BuildWaypointList(startCell, goalCell, resultWaypoints);
                    if (straightenPath && resultWaypoints.Count > 1)
                    {
                        StraightenPath(startPosition, resultWaypoints);
                    }
                    return true;
                }

                int currentX = currentCell % mapWidth;
                int currentY = currentCell / mapWidth;

                for (int neighbourNumber = 0; neighbourNumber < 8; neighbourNumber++)
                {
                    int neighbourX = currentX + NeighbourOffsetX[neighbourNumber];
                    int neighbourY = currentY + NeighbourOffsetY[neighbourNumber];

                    float neighbourMovementCost = GetCellMovementCost(neighbourX, neighbourY);
                    if (neighbourMovementCost < 0f) continue; // wall or outside the map

                    // Don't cut diagonally past the corner of a wall. Agents have a radius
                    // and the framework would push them back, making them jitter.
                    bool isDiagonalMove = neighbourNumber >= FirstDiagonalNeighbourIndex;
                    if (isDiagonalMove)
                    {
                        bool sideCellOneBlocked = GetCellMovementCost(neighbourX, currentY) < 0f;
                        bool sideCellTwoBlocked = GetCellMovementCost(currentX, neighbourY) < 0f;
                        if (sideCellOneBlocked || sideCellTwoBlocked) continue;
                    }

                    int neighbourCell = neighbourY * mapWidth + neighbourX;
                    if (closedInSearchId[neighbourCell] == currentSearchId) continue; // already finished with it

                    float stepDistance = isDiagonalMove ? DiagonalStepCost : 1f;
                    float newCostFromStart = costFromStart[currentCell] + stepDistance * neighbourMovementCost;

                    bool neverReachedBefore = costValidForSearchId[neighbourCell] != currentSearchId;
                    bool foundCheaperRoute = !neverReachedBefore && newCostFromStart < costFromStart[neighbourCell];

                    if (neverReachedBefore || foundCheaperRoute)
                    {
                        costValidForSearchId[neighbourCell] = currentSearchId;
                        costFromStart[neighbourCell] = newCostFromStart;
                        cameFromCell[neighbourCell] = currentCell;

                        float estimatedTotalCost = newCostFromStart + EstimateDistanceToGoal(neighbourX, neighbourY, goalX, goalY);
                        openCells.Add(estimatedTotalCost, neighbourCell);
                    }
                }
            }

            return false; // searched everywhere reachable, no route to the goal
        }

        /// <summary>
        /// Returns true if an agent could walk in a straight line between the two positions over
        /// plain ground (no walls, water or mud within its width). This is cheap, so it is used to
        /// skip the full path search when the way is clear.
        /// </summary>
        public static bool IsStraightLineWalkable(Vector3 fromPosition, Vector3 toPosition)
        {
            EnsureGridIsReady();

            Vector3 fullVector = toPosition - fromPosition;
            fullVector.z = 0f;
            float lineLength = fullVector.magnitude;
            if (lineLength < 0.001f) return true;

            Vector3 lineDirection = fullVector / lineLength;
            Vector3 sideOffset = new Vector3(-lineDirection.y, lineDirection.x, 0f) * LineCheckSideOffset;

            // Walk along the line checking the middle and both sides at each sample point
            for (float distanceAlongLine = 0f; distanceAlongLine <= lineLength; distanceAlongLine += LineCheckSpacing)
            {
                Vector3 samplePoint = fromPosition + lineDirection * distanceAlongLine;
                bool middleIsClear = IsPlainGround(samplePoint);
                bool leftIsClear = IsPlainGround(samplePoint + sideOffset);
                bool rightIsClear = IsPlainGround(samplePoint - sideOffset);

                if (!middleIsClear || !leftIsClear || !rightIsClear) return false;
            }
            return IsPlainGround(toPosition);
        }

        /// <summary>Call this if the map can change while the game runs, because costs are cached per cell</summary>
        public static void ClearCostCache()
        {
            if (isCostCached != null)
            {
                System.Array.Clear(isCostCached, 0, isCostCached.Length);
            }
        }

        // ------------------------------------------------------------------ private helpers

        /// <summary>Sets up (or rebuilds) the arrays if this is the first search or the map object changed</summary>
        private static void EnsureGridIsReady()
        {
            Map currentMap = GameData.Instance.Map;
            if (currentMap == gameMap && movementCostPerCell != null) return;

            gameMap = currentMap;
            mapWidth = (int)Map.MapWidth;
            mapHeight = (int)Map.MapHeight;

            int cellCount = mapWidth * mapHeight;
            movementCostPerCell = new float[cellCount];
            isCostCached = new bool[cellCount];
            costFromStart = new float[cellCount];
            cameFromCell = new int[cellCount];
            costValidForSearchId = new int[cellCount];
            closedInSearchId = new int[cellCount];
            currentSearchId = 0;
        }

        /// <summary>
        /// How expensive it is to move into this cell.
        /// Returns BlockedCellCost (-1) for walls or outside the map, 1 for normal ground,
        /// and more than 1 for slow terrain (mud = 2, water = 4).
        /// </summary>
        private static float GetCellMovementCost(int cellX, int cellY)
        {
            bool outsideMap = cellX < 0 || cellY < 0 || cellX >= mapWidth || cellY >= mapHeight;
            if (outsideMap) return BlockedCellCost;

            int cellIndex = cellY * mapWidth + cellX;
            if (!isCostCached[cellIndex])
            {
                isCostCached[cellIndex] = true;

                if (!gameMap.IsNavigatable(cellX, cellY))
                {
                    movementCostPerCell[cellIndex] = BlockedCellCost;
                }
                else
                {
                    // Slower terrain = higher cost. A speed factor of 0.25 means 4x the cost.
                    switch (gameMap.GetTerrainAt(cellX, cellY))
                    {
                        case Map.Terrain.Water:
                            movementCostPerCell[cellIndex] = 1f / SteeringAgent.WaterSpeedFactor;
                            break;
                        case Map.Terrain.Mud:
                            movementCostPerCell[cellIndex] = 1f / SteeringAgent.MudSpeedFactor;
                            break;
                        default:
                            movementCostPerCell[cellIndex] = 1f;
                            break;
                    }
                }
            }
            return movementCostPerCell[cellIndex];
        }

        /// <summary>True if this world position is on normal ground (not a wall, water or mud)</summary>
        private static bool IsPlainGround(Vector3 worldPosition)
        {
            float cost = GetCellMovementCost(Mathf.FloorToInt(worldPosition.x), Mathf.FloorToInt(worldPosition.y));
            return cost > 0f && cost <= 1.001f;
        }

        /// <summary>
        /// The "H" in A*: a guess at the remaining cost to the goal, ignoring walls.
        /// Uses octile distance (straight and diagonal steps allowed). It never guesses too high,
        /// because the cheapest possible cell costs 1, so A* is guaranteed to find the cheapest route.
        /// </summary>
        private static float EstimateDistanceToGoal(int cellX, int cellY, int goalX, int goalY)
        {
            int distanceX = Mathf.Abs(cellX - goalX);
            int distanceY = Mathf.Abs(cellY - goalY);
            int diagonalSteps = Mathf.Min(distanceX, distanceY);
            int straightSteps = (distanceX + distanceY) - 2 * diagonalSteps;
            return straightSteps + diagonalSteps * DiagonalStepCost;
        }

        /// <summary>Looks in growing rings around the cell for the closest free cell. Changes cellX/cellY to it.</summary>
        private static bool FindNearestWalkableCell(ref int cellX, ref int cellY)
        {
            for (int ringRadius = 1; ringRadius <= MaxSearchRadiusForFreeCell; ringRadius++)
            {
                bool foundFreeCell = false;
                int bestX = 0;
                int bestY = 0;
                float bestSquaredDistance = float.MaxValue;

                for (int offsetX = -ringRadius; offsetX <= ringRadius; offsetX++)
                {
                    for (int offsetY = -ringRadius; offsetY <= ringRadius; offsetY++)
                    {
                        // Only look at cells on the edge of this ring, inner cells were checked in earlier rings
                        bool isOnRingEdge = Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY)) == ringRadius;
                        if (!isOnRingEdge) continue;

                        if (GetCellMovementCost(cellX + offsetX, cellY + offsetY) < 0f) continue;

                        float squaredDistance = offsetX * offsetX + offsetY * offsetY;
                        if (squaredDistance < bestSquaredDistance)
                        {
                            bestSquaredDistance = squaredDistance;
                            bestX = cellX + offsetX;
                            bestY = cellY + offsetY;
                            foundFreeCell = true;
                        }
                    }
                }

                if (foundFreeCell)
                {
                    cellX = bestX;
                    cellY = bestY;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Follows the "came from" links back from the goal to the start, then reverses them into walking order</summary>
        private static void BuildWaypointList(int startCell, int goalCell, List<Vector3> resultWaypoints)
        {
            for (int cell = goalCell; cell != startCell; cell = cameFromCell[cell])
            {
                // +0.5 puts the waypoint in the middle of the cell
                float worldX = cell % mapWidth + 0.5f;
                float worldY = cell / mapWidth + 0.5f;
                resultWaypoints.Add(new Vector3(worldX, worldY, 0f));
            }
            resultWaypoints.Reverse();
        }

        /// <summary>
        /// Removes waypoints that aren't needed. From the current point, skip ahead to the furthest
        /// waypoint we can walk to in a straight line over plain ground, and repeat from there.
        /// Waypoints in water or mud are kept so the path still respects those costs.
        /// </summary>
        private static void StraightenPath(Vector3 startPosition, List<Vector3> waypoints)
        {
            straightenedPathBuffer.Clear();

            Vector3 currentPoint = startPosition;
            int nextWaypointToConsider = 0;

            while (nextWaypointToConsider < waypoints.Count)
            {
                // Find the furthest waypoint that can be reached in a straight line
                int furthestReachable = waypoints.Count - 1;
                while (furthestReachable > nextWaypointToConsider &&
                       !IsStraightLineWalkable(currentPoint, waypoints[furthestReachable]))
                {
                    furthestReachable--;
                }

                straightenedPathBuffer.Add(waypoints[furthestReachable]);
                currentPoint = waypoints[furthestReachable];
                nextWaypointToConsider = furthestReachable + 1;
            }

            waypoints.Clear();
            waypoints.AddRange(straightenedPathBuffer);
        }

        /// <summary>
        /// A simple "binary heap" that always hands back the cell with the lowest priority number.
        /// A* uses it to pick the most promising cell next. You can treat this as a black box.
        /// </summary>
        private sealed class CellPriorityQueue
        {
            private float[] priorities = new float[256];
            private int[] cellIndices = new int[256];
            private int itemCount;

            public int Count => itemCount;

            public void Clear()
            {
                itemCount = 0;
            }

            /// <summary>Adds a cell. Lower priority numbers come out first.</summary>
            public void Add(float priority, int cellIndex)
            {
                if (itemCount == priorities.Length)
                {
                    System.Array.Resize(ref priorities, itemCount * 2);
                    System.Array.Resize(ref cellIndices, itemCount * 2);
                }

                // Add at the end, then move it up while it has a lower priority than its parent
                int position = itemCount++;
                while (position > 0)
                {
                    int parentPosition = (position - 1) / 2;
                    if (priorities[parentPosition] <= priority) break;

                    priorities[position] = priorities[parentPosition];
                    cellIndices[position] = cellIndices[parentPosition];
                    position = parentPosition;
                }
                priorities[position] = priority;
                cellIndices[position] = cellIndex;
            }

            /// <summary>Removes and returns the cell with the lowest priority number</summary>
            public int RemoveLowest()
            {
                int lowestCell = cellIndices[0];
                itemCount--;

                if (itemCount > 0)
                {
                    // Take the last item and move it down from the top until it fits
                    float movingPriority = priorities[itemCount];
                    int movingCell = cellIndices[itemCount];
                    int position = 0;

                    while (true)
                    {
                        int childPosition = position * 2 + 1;
                        if (childPosition >= itemCount) break;

                        bool rightChildIsLower = childPosition + 1 < itemCount &&
                                                 priorities[childPosition + 1] < priorities[childPosition];
                        if (rightChildIsLower) childPosition++;

                        if (priorities[childPosition] >= movingPriority) break;

                        priorities[position] = priorities[childPosition];
                        cellIndices[position] = cellIndices[childPosition];
                        position = childPosition;
                    }
                    priorities[position] = movingPriority;
                    cellIndices[position] = movingCell;
                }
                return lowestCell;
            }
        }
    }
}