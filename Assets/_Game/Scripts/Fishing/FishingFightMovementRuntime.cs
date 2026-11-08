using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Fight-movement companion.
/// - Plans up to 20 m ahead through the terrain/water-depth field before a fish reaches shore.
/// - Chooses a safe heading fan and remembers which side of an island it is rounding,
///   so boat-to-shore casts do not make the fish repeatedly run straight onto land.
/// - Redirects the core fight's existing per-frame movement distance onto that planned
///   heading; it does not give the fish extra escape speed.
/// - Uses stable base-water depth (not the moving wave crest) for routing decisions.
/// - Keeps a small emergency recovery only for an already critically-shallow fish.
/// - Runs the existing unconscious retrieve step one extra time while REEL is held,
///   preserving the requested 2x unconscious retrieve speed.
/// </summary>
[DefaultExecutionOrder(2400)]
public sealed class FishingFightMovementRuntime : MonoBehaviour
{
    private const float PlannerInterval = 0.11f;
    private const float RouteLookAheadMetres = 20f;
    private const float SafeRouteDepth = 1.10f;
    private const float MinimumEscapeDepth = 0.72f;
    private const float EmergencyDepth = 0.46f;
    private const float RouteSideHoldSeconds = 2.8f;
    private const float StallGraceSeconds = 0.65f;
    private const float MinimumProgressMetres = 0.004f;

    private static readonly float[] RouteDistances = { 2f, 4f, 7f, 10f, 14f, 17f, RouteLookAheadMetres };
    private static readonly float[] CandidateAngles = { 0f, 25f, -25f, 45f, -45f, 65f, -65f, 85f, -85f, 105f, -105f, 125f, -125f };

    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo StateField = typeof(FishingSystem).GetField("state", Flags);
    private static readonly FieldInfo UnconsciousField = typeof(FishingSystem).GetField("fishUnconscious", Flags);
    private static readonly FieldInfo FishOnShoreField = typeof(FishingSystem).GetField("fishOnShore", Flags);
    private static readonly FieldInfo PondCastField = typeof(FishingSystem).GetField("pondCast", Flags);
    private static readonly FieldInfo BobberField = typeof(FishingSystem).GetField("bobber", Flags);
    private static readonly FieldInfo CastPointField = typeof(FishingSystem).GetField("castPoint", Flags);
    private static readonly FieldInfo FightMovePhaseField = typeof(FishingSystem).GetField("fightMovePhase", Flags);
    private static readonly FieldInfo FightTravelDirectionField = typeof(FishingSystem).GetField("fightTravelDirection", Flags);
    private static readonly FieldInfo OceanWaterField = typeof(FishingSystem).GetField("oceanWater", Flags);
    private static readonly MethodInfo UpdateUnconsciousBobberMethod = typeof(FishingSystem).GetMethod("UpdateUnconsciousBobber", Flags);
    private static readonly MethodInfo UpdateUnconsciousFishVisualMethod = typeof(FishingSystem).GetMethod("UpdateUnconsciousFishVisual", Flags);

    private FishingSystem fishing;
    private FishingHUD hud;
    private OceanWater ocean;
    private float plannerCooldown;
    private float nextPhaseShuffle;
    private float routeSideTimer;
    private int routeSideSign;
    private bool steeringActive;
    private Vector3 steeringDirection;
    private bool havePreviousBobberPosition;
    private Vector3 previousBobberPosition;
    private float previousDistance = -1f;
    private float stalledSeconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach (FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
        {
            if (system != null && system.GetComponent<FishingFightMovementRuntime>() == null)
                system.gameObject.AddComponent<FishingFightMovementRuntime>();
        }
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        hud = FindFirstObjectByType<FishingHUD>();
        ocean = fishing != null && OceanWaterField != null ? OceanWaterField.GetValue(fishing) as OceanWater : null;
        nextPhaseShuffle = UnityEngine.Random.Range(0.55f, 1.35f);

        if (fishing == null || StateField == null || UnconsciousField == null ||
            FishOnShoreField == null || PondCastField == null || BobberField == null ||
            CastPointField == null || FightMovePhaseField == null || FightTravelDirectionField == null ||
            UpdateUnconsciousBobberMethod == null || UpdateUnconsciousFishVisualMethod == null)
        {
            Debug.LogError("FishingFightMovementRuntime could not bind FishingSystem movement fields and was disabled.");
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!enabled || fishing == null || !IsFighting())
        {
            ResetTracking();
            return;
        }

        if (hud == null) hud = FindFirstObjectByType<FishingHUD>();
        if (ocean == null && OceanWaterField != null) ocean = OceanWaterField.GetValue(fishing) as OceanWater;

        bool reeling = hud != null && hud.ActionInput != null && hud.ActionInput.IsHeld;
        bool unconscious = (bool)UnconsciousField.GetValue(fishing);
        GameObject bobber = BobberField.GetValue(fishing) as GameObject;

        if (unconscious)
        {
            ResetRouteOnly();
            if (reeling && !(bool)FishOnShoreField.GetValue(fishing))
            {
                UpdateUnconsciousBobberMethod.Invoke(fishing, null);
                UpdateUnconsciousFishVisualMethod.Invoke(fishing, null);
            }
            return;
        }

        if (bobber == null || !bobber.activeSelf)
        {
            ResetTracking();
            return;
        }

        // Pond movement has no ocean route. Temporary forced movement pauses the
        // route instead of forgetting which way the fish was rounding an obstacle.
        if ((bool)PondCastField.GetValue(fishing) || fishing.IsSirenCalling || fishing.IsSkillStunned)
        {
            if ((bool)PondCastField.GetValue(fishing)) ResetRouteOnly();
            plannerCooldown = 0f;
            stalledSeconds = 0f;
            previousBobberPosition = bobber.transform.position;
            havePreviousBobberPosition = true;
            previousDistance = HorizontalDistance(bobber.transform.position, fishing.transform.position);
            return;
        }

        Vector3 corePosition = bobber.transform.position;
        if (!havePreviousBobberPosition)
        {
            previousBobberPosition = corePosition;
            havePreviousBobberPosition = true;
        }

        nextPhaseShuffle -= Time.deltaTime;
        if (nextPhaseShuffle <= 0f)
        {
            FightMovePhaseField.SetValue(fishing, UnityEngine.Random.Range(0f, Mathf.PI * 2f));
            nextPhaseShuffle = UnityEngine.Random.Range(0.55f, 1.35f);
        }

        if (routeSideTimer > 0f)
        {
            routeSideTimer -= Time.deltaTime;
            if (routeSideTimer <= 0f) routeSideSign = 0;
        }

        Vector3 routeOrigin = previousBobberPosition;
        Vector3 away = HorizontalDirection(routeOrigin - fishing.transform.position, fishing.transform.forward);
        Vector3 currentDirection = steeringActive
            ? HorizontalDirection(steeringDirection, away)
            : HorizontalDirection(corePosition - previousBobberPosition, away);

        float originDepth = StableWaterDepth(routeOrigin);
        bool fishAlreadyOnShore = (bool)FishOnShoreField.GetValue(fishing);

        plannerCooldown -= Time.deltaTime;
        if (plannerCooldown <= 0f)
        {
            float currentRouteMinimum = RouteMinimumDepth(routeOrigin, currentDirection);
            bool routeThreatened = currentRouteMinimum < SafeRouteDepth;

            // Once the planner commits to circling one side of the island, keep
            // evaluating that route before each turn rather than waiting for another
            // shallow-water collision.
            if (routeThreatened)
                PlanRoute(routeOrigin, away, currentDirection, routeThreatened);
            else if (steeringActive && routeSideTimer <= 0f &&
                     RouteMinimumDepth(routeOrigin, away) >= SafeRouteDepth)
                ResetRouteOnly();

            plannerCooldown = PlannerInterval;
        }

        // The core uses our saved escape heading before applying reel pull. Do not
        // rotate that combined velocity here: doing so would turn retrieval into
        // extra escape movement. Still plan/check routes while REEL is held.
        if (reeling)
        {
            previousBobberPosition = corePosition;
            havePreviousBobberPosition = true;
            previousDistance = HorizontalDistance(corePosition, fishing.transform.position);
            stalledSeconds = 0f;
            return;
        }

        // FishingSystem already moved the bobber this frame. If a route is active,
        // preserve that exact horizontal step length but rotate the step onto the
        // planned safe heading. This is steering, not an extra movement impulse.
        if (steeringActive)
        {
            Vector3 coreStep = corePosition - previousBobberPosition;
            coreStep.y = 0f;
            float stepLength = coreStep.magnitude;

            if (stepLength > 0.0001f)
            {
                Vector3 candidate = previousBobberPosition + steeringDirection * stepLength;
                if (StableWaterDepth(candidate) >= MinimumEscapeDepth &&
                    RockFishSegmentClear(
                        previousBobberPosition, candidate, .40f))
                {
                    if (ocean != null) candidate.y = ocean.GetSurfaceHeight(candidate) - 0.08f;
                    bobber.transform.position = candidate;
                    CastPointField.SetValue(fishing, candidate);
                    FightTravelDirectionField.SetValue(fishing, steeringDirection);
                    corePosition = candidate;
                }
                else
                {
                    // Terrain can have a tiny spike between planner samples. Replan
                    // immediately instead of letting one frame cross into the beach.
                    PlanRoute(previousBobberPosition, away, steeringDirection, true);
                    candidate = previousBobberPosition + steeringDirection * stepLength;
                    if (StableWaterDepth(candidate) >= MinimumEscapeDepth &&
                        RockFishSegmentClear(
                            previousBobberPosition, candidate, .40f))
                    {
                        if (ocean != null) candidate.y = ocean.GetSurfaceHeight(candidate) - 0.08f;
                        bobber.transform.position = candidate;
                        CastPointField.SetValue(fishing, candidate);
                        FightTravelDirectionField.SetValue(fishing, steeringDirection);
                        corePosition = candidate;
                    }
                }
            }
        }

        // Emergency recovery is intentionally last-resort only. Normal shoreline
        // avoidance should happen many metres before this branch is ever necessary.
        if (fishAlreadyOnShore || originDepth < EmergencyDepth || StableWaterDepth(corePosition) < EmergencyDepth)
        {
            EmergencyRecover(bobber, away);
            corePosition = bobber.transform.position;
        }

        float distance = HorizontalDistance(corePosition, fishing.transform.position);
        if (previousDistance < 0f || distance > previousDistance + MinimumProgressMetres || steeringActive)
            stalledSeconds = 0f;
        else
            stalledSeconds += Time.deltaTime;
        previousDistance = distance;

        if (stalledSeconds >= StallGraceSeconds)
        {
            PlanRoute(corePosition, away, currentDirection, true);
            stalledSeconds = 0f;
            plannerCooldown = PlannerInterval;
        }

        previousBobberPosition = corePosition;
    }

    // Supply only the fish's escape direction; reel pull and status-dependent
    // speed remain the core movement's responsibility.
    public bool TryGetEscapeDirection(out Vector3 direction)
    {
        direction = steeringDirection;
        return enabled && steeringActive && fishing != null && IsFighting() &&
            !(bool)UnconsciousField.GetValue(fishing) &&
            !(bool)PondCastField.GetValue(fishing);
    }

    private void PlanRoute(Vector3 position, Vector3 away, Vector3 currentDirection, bool threatened)
    {
        Vector3 bestDirection = currentDirection;
        float bestScore = float.NegativeInfinity;
        int bestSign = routeSideSign;

        for (int i = 0; i < CandidateAngles.Length; i++)
        {
            float angle = CandidateAngles[i];
            Vector3 direction = HorizontalDirection(Quaternion.AngleAxis(angle, Vector3.up) * away, away);

            // Rounding a shoreline can require a temporary partly-inward arc. More
            // than ~125 degrees back toward the player is never considered.
            float outward = Vector3.Dot(direction, away);
            if (outward < -0.58f) continue;

            float minimumDepth;
            float averageDepth;
            float endDepth;
            EvaluateRoute(position, direction, out minimumDepth, out averageDepth, out endDepth);

            float continuity = Vector3.Dot(direction, currentDirection);
            int sign = SignedSide(away, direction);
            float score = minimumDepth * 5.2f + averageDepth * 1.35f + endDepth * 0.75f;
            score += outward * 1.0f + continuity * 0.70f;

            if (minimumDepth < MinimumEscapeDepth) score -= 80f;
            else if (minimumDepth < SafeRouteDepth) score -= (SafeRouteDepth - minimumDepth) * 18f;

            if (routeSideSign != 0 && sign != 0)
                score += sign == routeSideSign ? 2.8f : -3.6f;

            score += UnityEngine.Random.Range(-0.08f, 0.08f);

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = direction;
                bestSign = sign;
            }
        }

        steeringDirection = bestDirection;
        steeringActive = true;
        FightTravelDirectionField.SetValue(fishing, bestDirection);
        FightMovePhaseField.SetValue(fishing, UnityEngine.Random.Range(0f, Mathf.PI * 2f));
        nextPhaseShuffle = UnityEngine.Random.Range(0.55f, 1.35f);

        if (bestSign != 0 && (threatened || routeSideSign != 0))
        {
            routeSideSign = bestSign;
            routeSideTimer = RouteSideHoldSeconds;
        }
        else if (!threatened)
        {
            routeSideSign = 0;
            routeSideTimer = 0f;
        }
    }

    private void EvaluateRoute(Vector3 origin, Vector3 direction, out float minimumDepth, out float averageDepth, out float endDepth)
    {
        minimumDepth = float.PositiveInfinity;
        averageDepth = 0f;
        endDepth = StableWaterDepth(origin);
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        int samples = 0;

        for (int i = 0; i < RouteDistances.Length; i++)
        {
            float distance = RouteDistances[i];
            Vector3 center = origin + direction * distance;

            // A three-point corridor rejects a route before the fish's weave/body
            // can clip a shallow sandbar beside the route centerline.
            float centerDepth = StableWaterDepth(center);
            float leftDepth = StableWaterDepth(center + side * 0.70f);
            float rightDepth = StableWaterDepth(center - side * 0.70f);
            float localMinimum = Mathf.Min(centerDepth, Mathf.Min(leftDepth, rightDepth));

            minimumDepth = Mathf.Min(minimumDepth, localMinimum);
            averageDepth += (centerDepth + leftDepth + rightDepth) / 3f;
            samples++;
            if (i == RouteDistances.Length - 1) endDepth = centerDepth;
        }

        if (samples > 0) averageDepth /= samples;
        if (float.IsPositiveInfinity(minimumDepth)) minimumDepth = endDepth;
        // Rock geometry can protrude into deep water between terrain-depth
        // samples. Check the entire projected corridor against the rock meshes.
        if (!RockFishSegmentClear(
                origin, origin + direction * RouteLookAheadMetres, 0.40f))
            minimumDepth = 0f;
    }

    private float RouteMinimumDepth(Vector3 origin, Vector3 direction)
    {
        float minimum, average, end;
        EvaluateRoute(origin, direction, out minimum, out average, out end);
        return minimum;
    }

    private void EmergencyRecover(GameObject bobber, Vector3 away)
    {
        Vector3 position = bobber.transform.position;
        Vector3 bestDirection = away;
        float currentDepth = StableWaterDepth(position);
        float bestDepth = currentDepth;

        for (int i = 0; i < CandidateAngles.Length; i++)
        {
            Vector3 direction = HorizontalDirection(Quaternion.AngleAxis(CandidateAngles[i], Vector3.up) * away, away);
            Vector3 candidate = position + direction * 0.45f;
            float depth = StableWaterDepth(candidate);
            if (depth > bestDepth)
            {
                bestDepth = depth;
                bestDirection = direction;
            }
        }

        if (bestDepth <= currentDepth + 0.02f) return;
        if (!RockFishSegmentClear(
                position, position + bestDirection * 0.32f, .40f)) return;

        Vector3 recovered = position + bestDirection * 0.32f;
        if (ocean != null) recovered.y = ocean.GetSurfaceHeight(recovered) - 0.08f;
        bobber.transform.position = recovered;
        CastPointField.SetValue(fishing, recovered);
        FightTravelDirectionField.SetValue(fishing, bestDirection);
        FishOnShoreField.SetValue(fishing, false);
        steeringDirection = bestDirection;
        steeringActive = true;
        previousBobberPosition = recovered;
        havePreviousBobberPosition = true;
    }

    // Imported Bluewater rock meshes must block fish routes just like Snapper rocks.
    private static bool RockFishSegmentClear(Vector3 from, Vector3 to, float radius)
    {
        return SnapperRockFishObstacles.SurfaceSegmentClear(from,to,radius) &&
               BrinebreakRockRegion.SurfaceSegmentClear(from,to,radius) &&
               BluewaterCayRockCollision.SurfaceSegmentClear(from,to,radius);
    }

    private static bool RockFishPointClear(Vector3 position, float radius)
    {
        return SnapperRockFishObstacles.SurfacePointClear(position,radius) &&
               BrinebreakRockRegion.SurfacePointClear(position,radius) &&
               BluewaterCayRockCollision.SurfacePointClear(position,radius);
    }

    private float StableWaterDepth(Vector3 position)
    {
        if (!MarinaDockExtensionRuntime.WaterClear(position,.4f))return 0f;
        if (!RockFishPointClear(position,.40f))return 0f;
        if (ocean == null || !WorldTerrain.TryGetHeight(position, out float ground)) return 1000f;
        return ocean.BaseWaterLevel - ground;
    }

    private static Vector3 HorizontalDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            fallback.y = 0f;
            direction = fallback.sqrMagnitude < 0.0001f ? Vector3.forward : fallback;
        }
        return direction.normalized;
    }

    private static int SignedSide(Vector3 away, Vector3 direction)
    {
        float angle = Vector3.SignedAngle(away, direction, Vector3.up);
        if (Mathf.Abs(angle) < 8f) return 0;
        return angle > 0f ? 1 : -1;
    }

    private bool IsFighting()
    {
        object state = StateField.GetValue(fishing);
        return state != null && string.Equals(state.ToString(), "Fighting", StringComparison.Ordinal);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector3 delta = a - b;
        delta.y = 0f;
        return delta.magnitude;
    }

    private void ResetRouteOnly()
    {
        plannerCooldown = 0f;
        routeSideTimer = 0f;
        routeSideSign = 0;
        steeringActive = false;
        stalledSeconds = 0f;
    }

    private void ResetTracking()
    {
        ResetRouteOnly();
        havePreviousBobberPosition = false;
        previousDistance = -1f;
    }
}


