using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class RaceSplinePath : MonoBehaviour
{
    [Tooltip("Segments ordered from the start line to the finish line. " +
             "A ordem do array é respeitada: arrasta os elementos para os pôr pela ordem da corrida.")]
    public RaceSplineSegment[] segments;

    public float TotalLength { get; private set; }

    void Awake()
    {
        CollectSegmentsIfNeeded();
        RecalculateLength();
        if (segments.Length == 0)
            Debug.LogError($"{name}: no RaceSplineSegment children were found.", this);
    }

    void OnValidate()
    {
        CollectSegmentsIfNeeded();
        RecalculateLength();
    }

    // ---------- Recolha de segmentos ----------

    // Se o array já tiver exatamente os segmentos filhos, mantém a ordem manual.
    // Só reconstrói (pela ordem da hierarquia) quando falta ou sobra algum.
    public void CollectSegmentsIfNeeded()
    {
        RaceSplineSegment[] found = GetComponentsInChildren<RaceSplineSegment>(true);

        if (segments != null && segments.Length == found.Length)
        {
            bool same = true;
            foreach (var s in segments)
            {
                if (s == null || System.Array.IndexOf(found, s) < 0)
                {
                    same = false;
                    break;
                }
            }
            if (same) return;
        }

        var unique = new List<RaceSplineSegment>(found.Length);
        foreach (RaceSplineSegment segment in found)
        {
            if (segment != null && !unique.Contains(segment))
                unique.Add(segment);
        }

        segments = unique.ToArray();
    }

    public void RecalculateLength()
    {
        TotalLength = 0f;
        if (segments == null) return;

        foreach (RaceSplineSegment segment in segments)
        {
            SplineContainer container = GetContainer(segment);
            if (container != null)
                TotalLength += container.Spline.GetLength();
        }
    }

    static SplineContainer GetContainer(RaceSplineSegment segment)
    {
        if (segment == null) return null;
        SplineContainer container = segment.Spline;
        if (container == null)
            container = segment.GetComponent<SplineContainer>();
        return container;
    }

    // ---------- Validação e gizmos ----------

    [ContextMenu("Validate Connections")]
    public void ValidateConnections()
    {
        CollectSegmentsIfNeeded();
        int n = segments.Length;
        if (n < 2)
        {
            Debug.Log("Validação: menos de 2 segmentos.");
            return;
        }

        int problems = 0;
        for (int i = 0; i < n; i++)
        {
            var a = segments[i];
            var b = segments[(i + 1) % n];
            var ca = GetContainer(a);
            var cb = GetContainer(b);
            if (ca == null || cb == null) continue;

            Vector3 endPos = ca.transform.TransformPoint(SplineUtility.EvaluatePosition(ca.Spline, 1f));
            Vector3 startPos = cb.transform.TransformPoint(SplineUtility.EvaluatePosition(cb.Spline, 0f));
            Vector3 endDir = ca.transform.TransformDirection(SplineUtility.EvaluateTangent(ca.Spline, 1f));
            Vector3 startDir = cb.transform.TransformDirection(SplineUtility.EvaluateTangent(cb.Spline, 0f));

            float gap = Vector3.Distance(endPos, startPos);
            float angle = Vector3.Angle(
                Vector3.ProjectOnPlane(endDir, Vector3.up),
                Vector3.ProjectOnPlane(startDir, Vector3.up));

            if (gap > 0.05f || angle > 5f)
            {
                problems++;
                Debug.LogWarning(
                    $"Junta {i} ({a.name}) -> {(i + 1) % n} ({b.name}): " +
                    $"afastamento {gap:F2} m, ângulo {angle:F1}°", b);
            }
        }

        Debug.Log(problems == 0
            ? "Validação concluída: todas as juntas estão certas."
            : $"Validação concluída: {problems} junta(s) com problemas.");
    }

    // Com o RaceSplinePath selecionado: bola verde = início, vermelha = fim, com o número da ordem
    void OnDrawGizmosSelected()
    {
        if (segments == null) return;

        for (int i = 0; i < segments.Length; i++)
        {
            SplineContainer c = GetContainer(segments[i]);
            if (c == null) continue;

            Vector3 a = c.transform.TransformPoint(SplineUtility.EvaluatePosition(c.Spline, 0f));
            Vector3 b = c.transform.TransformPoint(SplineUtility.EvaluatePosition(c.Spline, 1f));

            Gizmos.color = Color.green; Gizmos.DrawSphere(a, 1f);
            Gizmos.color = Color.red;   Gizmos.DrawSphere(b, 1f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(a + Vector3.up * 3f, $"{i} início ({segments[i].name})");
            UnityEditor.Handles.Label(b + Vector3.up * 3f, $"{i} fim");
#endif
        }
    }

    // ---------- Consultas ao circuito ----------

    public bool Evaluate(
        float distance,
        float lateralOffset,
        out Vector3 position,
        out Vector3 forward,
        out RaceSplineSegment segment)
    {
        position = default;
        forward = Vector3.forward;
        segment = null;

        if (segments == null || segments.Length == 0 || TotalLength <= 0f) return false;

        float wrappedDistance = Mathf.Repeat(distance, TotalLength);
        float passed = 0f;

        foreach (RaceSplineSegment candidate in segments)
        {
            SplineContainer container = GetContainer(candidate);
            if (container == null) continue;

            float length = container.Spline.GetLength();
            if (length <= 0f) continue;
            if (wrappedDistance > passed + length)
            {
                passed += length;
                continue;
            }

            float distanceInSegment = wrappedDistance - passed;
            position = EvaluateCenterPosition(container, distanceInSegment);
            forward = EvaluateCenterTangent(container, distanceInSegment);

            if (forward.sqrMagnitude > 0.0001f)
            {
                Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
                position += rotation * Vector3.right * lateralOffset;
            }

            segment = candidate;
            return true;
        }

        return false;
    }

    static Vector3 EvaluateCenterPosition(SplineContainer container, float distance)
    {
        float t = GetNormalizedDistance(container, distance);
        Vector3 localPosition = SplineUtility.EvaluatePosition(container.Spline, t);
        return container.transform.TransformPoint(localPosition);
    }

    static Vector3 EvaluateCenterTangent(SplineContainer container, float distance)
    {
        float t = GetNormalizedDistance(container, distance);
        Vector3 localTangent = SplineUtility.EvaluateTangent(container.Spline, t);
        return container.transform.TransformDirection(localTangent).normalized;
    }

    static float GetNormalizedDistance(SplineContainer container, float distance)
    {
        return SplineUtility.ConvertIndexUnit(
            container.Spline,
            Mathf.Max(0f, distance),
            PathIndexUnit.Distance,
            PathIndexUnit.Normalized);
    }

    public float GetClosestDistance(Vector3 worldPosition)
    {
        if (segments == null || segments.Length == 0 || TotalLength <= 0f)
            return 0f;

        const int samplesPerSegment = 64;
        float distanceBeforeSegment = 0f;
        float closestDistance = 0f;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (RaceSplineSegment segment in segments)
        {
            SplineContainer container = GetContainer(segment);
            if (container == null) continue;

            float length = container.Spline.GetLength();
            if (length <= 0f) continue;

            for (int sample = 0; sample <= samplesPerSegment; sample++)
            {
                float t = sample / (float)samplesPerSegment;
                Vector3 point = container.transform.TransformPoint(
                    SplineUtility.EvaluatePosition(container.Spline, t));
                float sqrDistance = (point - worldPosition).sqrMagnitude;

                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestDistance = distanceBeforeSegment +
                        SplineUtility.ConvertIndexUnit(
                            container.Spline,
                            t,
                            PathIndexUnit.Normalized,
                            PathIndexUnit.Distance);
                }
            }

            distanceBeforeSegment += length;
        }

        return closestDistance;
    }

    public float GetSegmentProgress(float distance, RaceSplineSegment target)
    {
        if (TotalLength <= 0f) return 0.5f;

        float passed = 0f;
        float wrappedDistance = Mathf.Repeat(distance, TotalLength);

        foreach (RaceSplineSegment segment in segments)
        {
            SplineContainer container = GetContainer(segment);
            if (container == null) continue;

            float length = container.Spline.GetLength();
            if (segment == target)
                return length > 0f
                    ? Mathf.Clamp01((wrappedDistance - passed) / length)
                    : 0f;
            passed += length;
        }

        return 0.5f;
    }

    public float GetDistanceToSegmentStart(float distance, RaceSplineSegment target)
    {
        if (TotalLength <= 0f) return float.PositiveInfinity;

        float passed = 0f;
        float wrappedDistance = Mathf.Repeat(distance, TotalLength);

        foreach (RaceSplineSegment segment in segments)
        {
            if (segment == target)
                return Mathf.Repeat(passed - wrappedDistance, TotalLength);

            SplineContainer container = GetContainer(segment);
            if (container != null)
                passed += container.Spline.GetLength();
        }

        return float.PositiveInfinity;
    }
}