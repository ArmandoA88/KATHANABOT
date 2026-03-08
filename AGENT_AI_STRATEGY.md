# Agent AI Strategy

## Goal

Build the leveling agent in layers:

1. Stable stationary combat and recovery.
2. Coordinate-aware map navigation.
3. Route planning across roads, portals, and farming loops.
4. Recovery when the agent drifts, gets blocked, or loses map certainty.

## Current Direction

The current codebase is already suitable for a stationary leveling agent. The next major expansion is navigation.

For this game, the best navigation model is not freeform movement prediction. It is a coordinate-based map model driven by the world/map UI, where the agent reads location from the map and moves between known route points.

## Navigation Model

Use the in-game map as the source of truth.

The map appears to provide:

- A stable 2D layout of roads and terrain.
- Cardinal orientation cues (`N`, `S`, `E`, `W`).
- A player marker/current position region.
- Named landmarks such as portals, towns, and ferry points.
- A fixed-width coordinate readout: `3 digits for X` and `3 digits for Y`.

This should be treated as a graph navigation problem:

- Nodes: important coordinates such as portals, intersections, grind spots, ferry/town entrances, and recovery points.
- Edges: traversable paths between nodes.
- Route: ordered node list from current position to target zone or farming loop.

## Recommended Architecture

Add a navigation layer above the current combat loop.

### High-level route planner

- Input: current map coordinate, destination node, known path graph.
- Output: route as waypoint list.
- Algorithm: start with simple graph search (`A*` or `Dijkstra`) over predefined map nodes.

### Mid-level travel controller

- Goal: move from current waypoint to next waypoint.
- Inputs:
  - current map position
  - heading/orientation
  - target waypoint
  - timeout/stuck detection
- Outputs:
  - movement commands
  - camera/turn corrections
  - recovery action if progress stalls

### Low-level recovery

- If the coordinate does not change for too long:
  - stop current movement
  - retry with alternate turn angle
  - back up and re-approach path
  - reopen map and re-localize
- If position confidence is lost:
  - move to nearest known anchor node
  - or abort navigation and return control to guardrails

## Data Model Additions

Extend the agent config with navigation-specific data:

- `NavigationEnabled`
- `MapOpenKey`
- `MapRect`
- `MapCoordinateRect`
- `PlayerMarkerColor/Profile`
- `WorldMapName`
- `RouteNodes`
- `RouteEdges`
- `FarmLoopNodes`
- `TravelTolerance`
- `WaypointReachRadius`
- `NavigationTimeoutSeconds`
- `RepathOnStuck`

Add runtime status fields:

- `CurrentMapName`
- `CurrentCoordinate`
- `TargetWaypoint`
- `RouteProgress`
- `NavigationState`
- `NavigationReason`
- `PositionConfidence`

## State Machine Expansion

The current leveling states should expand to include travel:

- `Searching`
- `Fighting`
- `Looting`
- `Recovering`
- `Traveling`
- `Repositioning`
- `Repathing`
- `Stuck`
- `GuardedStop`

Recommended travel flow:

1. Open map.
2. Read current position.
3. Compute route to selected grind area or loop.
4. Close map.
5. Move toward next waypoint.
6. Periodically reopen map to verify progress.
7. When destination is reached, return to combat mode.

## Map Interpretation Strategy

Use the map as a periodic localization tool, not as something that must stay open constantly.

Recommended approach:

1. Calibrate the full map capture region.
2. Calibrate the area where the player marker appears.
3. Parse the coordinate readout as a fixed `XXX/YYY` format and normalize OCR output back to three digits.
4. Detect named landmarks and orientation markers only as supporting signals.
5. Store normalized coordinates for important nodes.
6. During travel, sample the map every few seconds or after a movement burst.

This keeps navigation robust even if moment-to-moment movement in the world is noisy.

## Route Representation

Represent each map as a waypoint graph:

```text
Node:
- Id
- MapName
- X
- Y
- Label
- Tags: portal, town, farm, ferry, safe, recovery

Edge:
- FromNodeId
- ToNodeId
- TravelMode: walk, portal, ferry
- Cost
- Notes
```

For farming, define loop routes:

- entry node
- combat area center
- fallback node
- vendor/town return node

## Why This Fits Better Than Pure Free-Movement

Pure free-movement pathing is fragile in this kind of game because the agent has weak world geometry information.

A map-coordinate system is better because:

- route planning becomes deterministic
- portals and towns become explicit nodes
- recovery logic has clear anchors
- farming loops can be repeated reliably
- future multi-map travel becomes manageable

## Implementation Order

### Phase 1

Keep the current leveling agent stationary and stable.

### Phase 2

Add map calibration and coordinate extraction:

- open map
- capture map
- read current position
- confirm heading/orientation

### Phase 3

Add waypoint graph support for one map only.

Start with a single route between:

- town
- one farming area
- one recovery point

### Phase 4

Add travel controller:

- move to waypoint
- verify coordinate progress
- recover on stall

### Phase 5

Integrate travel with leveling behavior:

- travel to grind zone
- fight until guardrail triggers
- return to safe node if needed

## Practical Constraint

The agent will still need to navigate around paths rather than moving as a straight line.

So the coordinate system should define the route skeleton, while the travel controller handles local corrections between nearby path nodes.

That means:

- coordinates solve "where to go"
- waypoint edges solve "which road/path to take"
- movement recovery solves "how to get unstuck on the way"

## Next Build Step

When navigation work starts in code, begin with:

1. navigation config types in `BotEngine.vb`
2. map calibration UI in `Form1.vb`
3. one hardcoded map graph
4. a travel-only mode that moves between two nodes without combat

Only after that should combat and travel be merged into one autonomous loop.
