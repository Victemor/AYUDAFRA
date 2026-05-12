using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construye barreras laterales cilíndricas siguiendo los samples del track,
/// incluyendo postes verticales de inicio y final para que no nazcan cortadas.
/// </summary>
public static class TrackBarrierMeshBuilder
{
    private const float MinimumRadius = 0.01f;
    private const float MinimumSegmentLength = 0.01f;

    public static Mesh BuildBarrierMesh(
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks,
        int startIndex,
        int endIndex,
        int totalChunkCount,
        TrackBarrierSide side,
        float lateralOffset,
        float radius,
        float verticalOffset,
        int radialSegments,
        int smoothingIterations)
    {
        Mesh mesh = new Mesh
        {
            name = $"TrackCylindricalBarrier_{side}_{startIndex}_{endIndex}",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        if (chunks == null || chunks.Count == 0)
        {
            return mesh;
        }

        List<TrackLayoutSamplePoint> samples = CollectSamples(chunks, startIndex, endIndex);

        if (samples.Count < 2)
        {
            return mesh;
        }

        List<Vector3> centers = BuildBarrierCenters(samples, side, lateralOffset, verticalOffset);
        SmoothCenters(centers, smoothingIterations);

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float safeRadius = Mathf.Max(MinimumRadius, radius);
        int safeRadialSegments = Mathf.Max(3, radialSegments);

        BuildTube(vertices, uvs, triangles, centers, samples, safeRadius, safeRadialSegments);

        AddVerticalPost(
            vertices,
            uvs,
            triangles,
            centers[0],
            verticalOffset,
            safeRadius,
            safeRadialSegments);

        AddVerticalPost(
            vertices,
            uvs,
            triangles,
            centers[centers.Count - 1],
            verticalOffset,
            safeRadius,
            safeRadialSegments);

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private static List<Vector3> BuildBarrierCenters(
        IReadOnlyList<TrackLayoutSamplePoint> samples,
        TrackBarrierSide side,
        float lateralOffset,
        float verticalOffset)
    {
        List<Vector3> centers = new List<Vector3>(samples.Count);
        float sideSign = side == TrackBarrierSide.Right ? 1f : -1f;

        for (int i = 0; i < samples.Count; i++)
        {
            TrackLayoutSamplePoint sample = samples[i];

            Vector3 right = ResolveSafeRight(sample.Right, sample.Forward);
            float halfReferenceWidth = ResolveHalfReferenceWidth(sample);
            float resolvedOffset = halfReferenceWidth + lateralOffset;

            Vector3 center = sample.Position
                             + right * resolvedOffset * sideSign
                             + Vector3.up * verticalOffset;

            centers.Add(center);
        }

        return centers;
    }

    private static void SmoothCenters(List<Vector3> centers, int iterations)
    {
        if (centers == null || centers.Count < 3 || iterations <= 0)
        {
            return;
        }

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            List<Vector3> copy = new List<Vector3>(centers);

            for (int i = 1; i < centers.Count - 1; i++)
            {
                centers[i] = (copy[i - 1] + copy[i] + copy[i + 1]) / 3f;
            }
        }
    }

    private static void BuildTube(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        IReadOnlyList<Vector3> centers,
        IReadOnlyList<TrackLayoutSamplePoint> samples,
        float radius,
        int radialSegments)
    {
        int startVertexIndex = vertices.Count;
        float accumulatedDistance = 0f;

        for (int i = 0; i < centers.Count; i++)
        {
            if (i > 0)
            {
                accumulatedDistance += Vector3.Distance(centers[i - 1], centers[i]);
            }

            ResolveTubeFrame(
                centers,
                samples,
                i,
                out Vector3 right,
                out Vector3 up);

            for (int s = 0; s < radialSegments; s++)
            {
                float angle01 = s / (float)radialSegments;
                float angleRadians = angle01 * Mathf.PI * 2f;

                Vector3 radial = right * Mathf.Cos(angleRadians) + up * Mathf.Sin(angleRadians);

                vertices.Add(centers[i] + radial * radius);
                uvs.Add(new Vector2(angle01, accumulatedDistance));
            }
        }

        for (int ring = 0; ring < centers.Count - 1; ring++)
        {
            if (Vector3.Distance(centers[ring], centers[ring + 1]) <= MinimumSegmentLength)
            {
                continue;
            }

            int ringAStart = startVertexIndex + ring * radialSegments;
            int ringBStart = startVertexIndex + (ring + 1) * radialSegments;

            for (int s = 0; s < radialSegments; s++)
            {
                int next = (s + 1) % radialSegments;

                int a0 = ringAStart + s;
                int a1 = ringAStart + next;
                int b0 = ringBStart + s;
                int b1 = ringBStart + next;

                triangles.Add(a0);
                triangles.Add(b0);
                triangles.Add(b1);

                triangles.Add(a0);
                triangles.Add(b1);
                triangles.Add(a1);
            }
        }
    }

    private static void AddVerticalPost(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Vector3 topCenter,
        float verticalOffset,
        float radius,
        int radialSegments)
    {
        Vector3 bottomCenter = topCenter - Vector3.up * Mathf.Max(0.01f, verticalOffset);

        List<Vector3> postCenters = new List<Vector3>
        {
            bottomCenter,
            topCenter
        };

        List<TrackLayoutSamplePoint> dummySamples = new List<TrackLayoutSamplePoint>
        {
            new TrackLayoutSamplePoint(bottomCenter, Vector3.forward, Vector3.right, 1f, 0f, TrackStructureType.SolidTrack, 0f, 0f),
            new TrackLayoutSamplePoint(topCenter, Vector3.forward, Vector3.right, 1f, 1f, TrackStructureType.SolidTrack, 0f, 0f)
        };

        int startVertexIndex = vertices.Count;

        for (int i = 0; i < postCenters.Count; i++)
        {
            Vector3 center = postCenters[i];

            Vector3 right = Vector3.right;
            Vector3 forward = Vector3.up;
            Vector3 up = Vector3.forward;

            for (int s = 0; s < radialSegments; s++)
            {
                float angle01 = s / (float)radialSegments;
                float angleRadians = angle01 * Mathf.PI * 2f;

                Vector3 radial = right * Mathf.Cos(angleRadians) + up * Mathf.Sin(angleRadians);

                vertices.Add(center + radial * radius);
                uvs.Add(new Vector2(angle01, i));
            }
        }

        int bottomRing = startVertexIndex;
        int topRing = startVertexIndex + radialSegments;

        for (int s = 0; s < radialSegments; s++)
        {
            int next = (s + 1) % radialSegments;

            int a0 = bottomRing + s;
            int a1 = bottomRing + next;
            int b0 = topRing + s;
            int b1 = topRing + next;

            triangles.Add(a0);
            triangles.Add(b0);
            triangles.Add(b1);

            triangles.Add(a0);
            triangles.Add(b1);
            triangles.Add(a1);
        }

        AddTubeCap(vertices, uvs, triangles, 0, startVertexIndex, radialSegments, true);
        AddTubeCap(vertices, uvs, triangles, 1, startVertexIndex, radialSegments, false);
    }

    private static void AddTubeCap(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        int ringIndex,
        int startVertexIndex,
        int radialSegments,
        bool isStart)
    {
        int ringStart = startVertexIndex + ringIndex * radialSegments;
        Vector3 center = Vector3.zero;

        for (int i = 0; i < radialSegments; i++)
        {
            center += vertices[ringStart + i];
        }

        center /= radialSegments;

        int centerIndex = vertices.Count;
        vertices.Add(center);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int s = 0; s < radialSegments; s++)
        {
            int next = (s + 1) % radialSegments;

            if (isStart)
            {
                triangles.Add(centerIndex);
                triangles.Add(ringStart + next);
                triangles.Add(ringStart + s);
            }
            else
            {
                triangles.Add(centerIndex);
                triangles.Add(ringStart + s);
                triangles.Add(ringStart + next);
            }
        }
    }

    private static void ResolveTubeFrame(
        IReadOnlyList<Vector3> centers,
        IReadOnlyList<TrackLayoutSamplePoint> samples,
        int index,
        out Vector3 right,
        out Vector3 up)
    {
        Vector3 forward = ResolveCenterForward(centers, index);

        if (index >= 0 && index < samples.Count)
        {
            right = ResolveSafeRight(samples[index].Right, samples[index].Forward);
        }
        else
        {
            right = Vector3.Cross(Vector3.up, forward).normalized;
        }

        if (Mathf.Abs(Vector3.Dot(right, forward)) > 0.95f)
        {
            right = Vector3.Cross(Vector3.up, forward).normalized;
        }

        up = Vector3.Cross(forward, right).normalized;

        if (up.sqrMagnitude < 0.0001f)
        {
            up = Vector3.up;
            right = Vector3.Cross(up, forward).normalized;
        }
    }

    private static Vector3 ResolveCenterForward(IReadOnlyList<Vector3> centers, int index)
    {
        if (centers.Count < 2)
        {
            return Vector3.forward;
        }

        Vector3 forward;

        if (index == 0)
        {
            forward = centers[1] - centers[0];
        }
        else if (index == centers.Count - 1)
        {
            forward = centers[index] - centers[index - 1];
        }
        else
        {
            forward = centers[index + 1] - centers[index - 1];
        }

        return forward.sqrMagnitude < 0.0001f
            ? Vector3.forward
            : forward.normalized;
    }

    private static List<TrackLayoutSamplePoint> CollectSamples(
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks,
        int startIndex,
        int endIndex)
    {
        List<TrackLayoutSamplePoint> samples = new List<TrackLayoutSamplePoint>();

        int safeStartIndex = Mathf.Clamp(startIndex, 0, chunks.Count - 1);
        int safeEndIndex = Mathf.Clamp(endIndex, safeStartIndex, chunks.Count - 1);

        for (int i = safeStartIndex; i <= safeEndIndex; i++)
        {
            IReadOnlyList<TrackLayoutSamplePoint> chunkSamples = chunks[i].Samples;

            for (int j = 0; j < chunkSamples.Count; j++)
            {
                TrackLayoutSamplePoint sample = chunkSamples[j];

                if (samples.Count > 0)
                {
                    TrackLayoutSamplePoint last = samples[samples.Count - 1];

                    if (Vector3.Distance(last.Position, sample.Position) <= 0.001f)
                    {
                        continue;
                    }
                }

                samples.Add(sample);
            }
        }

        return samples;
    }

    private static float ResolveHalfReferenceWidth(TrackLayoutSamplePoint sample)
    {
        if (sample.StructureType == TrackStructureType.RailTrack)
        {
            float railSeparation = Mathf.Max(0f, sample.RailSeparation);
            float railWidth = Mathf.Max(0f, sample.RailWidth);

            if (railSeparation <= 0.0001f || railWidth <= 0.0001f)
            {
                return Mathf.Max(0f, sample.Width) * 0.5f;
            }

            return railSeparation * 0.5f + railWidth * 0.5f;
        }

        return Mathf.Max(0f, sample.Width) * 0.5f;
    }

    private static Vector3 ResolveSafeRight(Vector3 right, Vector3 forward)
    {
        if (right.sqrMagnitude >= 0.0001f)
        {
            return right.normalized;
        }

        Vector3 horizontalForward = new Vector3(forward.x, 0f, forward.z);

        if (horizontalForward.sqrMagnitude < 0.0001f)
        {
            return Vector3.right;
        }

        horizontalForward.Normalize();

        Vector3 resolvedRight = Vector3.Cross(Vector3.up, horizontalForward);

        return resolvedRight.sqrMagnitude < 0.0001f
            ? Vector3.right
            : resolvedRight.normalized;
    }
}