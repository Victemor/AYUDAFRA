using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera una zona de muerte procedural siguiendo la forma general del track generado,
/// ignorando gaps y estrechamientos.
/// 
/// Responsabilidades:
/// - Replicar la trayectoria global del track.
/// - Ignorar cambios de ancho y secciones sin superficie.
/// - Expandir lateralmente y longitudinalmente el volumen de muerte.
/// - Construir una representación visual opcional.
/// - Construir colliders trigger por segmentos para matar al jugador al tocar la zona.
/// </summary>
public sealed class VoidZoneGenerator : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]
    [Tooltip("Generador principal del track.")]
    [SerializeField] private TrackGeneratorController trackGenerator;

    [Tooltip("Perfil base del track. Se usa para recuperar ancho normal.")]
    [SerializeField] private TrackGenerationProfile generationProfile;

    [Header("Shape")]
    [Tooltip("Distancia vertical hacia abajo respecto al track real.")]
    [SerializeField] private float verticalOffset = 4f;

    [Tooltip("Grosor vertical de la zona de muerte.")]
    [SerializeField] private float deathZoneThickness = 3f;

    [Tooltip("Padding extra hacia la izquierda del track.")]
    [SerializeField] private float leftPadding = 2f;

    [Tooltip("Padding extra hacia la derecha del track.")]
    [SerializeField] private float rightPadding = 2f;

    [Tooltip("Extensión extra antes del inicio del track.")]
    [SerializeField] private float startPadding = 2f;

    [Tooltip("Extensión extra después del final del track.")]
    [SerializeField] private float endPadding = 2f;

    [Header("Sampling")]
    [Tooltip("Cantidad mínima de subdivisiones para giros del volumen de muerte.")]
    [SerializeField] private int minimumTurnSubdivisions = 6;

    [Tooltip("Resolución lineal aproximada para secciones rectas o pendientes.")]
    [SerializeField] private float linearSamplesPerUnit = 0.35f;

    [Header("Output")]
    [Tooltip("Nombre del objeto hijo generado.")]
    [SerializeField] private string generatedObjectName = "GeneratedVoidZone";

    [Tooltip("Si está activo, se genera MeshRenderer además de los triggers.")]
    [SerializeField] private bool generateRenderer;

    [Tooltip("Material opcional para debug visual de la zona de muerte.")]
    [SerializeField] private Material debugMaterial;

    [Header("Trigger Colliders")]
    [Tooltip("Altura adicional de seguridad para que el trigger capture bien al jugador.")]
    [SerializeField] private float triggerHeightPadding = 0.5f;

    [Tooltip("Solape extra entre colliders consecutivos para evitar huecos.")]
    [SerializeField] private float triggerLengthOverlap = 0.25f;

    [Tooltip("Si está activo, genera colliders trigger por segmentos.")]
    [SerializeField] private bool generateSegmentTriggers = true;

    #endregion

    #region Public API

    /// <summary>
    /// Reconstruye la zona de muerte a partir del mapa runtime generado.
    /// </summary>
    public void Rebuild()
    {
        if (trackGenerator == null || trackGenerator.GeneratedMap == null)
        {
            Debug.LogWarning("[VOID ZONE] TrackGenerator or GeneratedMap is missing.");
            return;
        }

        if (generationProfile == null)
        {
            Debug.LogWarning("[VOID ZONE] GenerationProfile is missing.");
            return;
        }

        ClearGenerated();

        IReadOnlyList<TrackSectionDefinition> sections = trackGenerator.GeneratedMap.Sections;
        if (sections == null || sections.Count == 0)
        {
            return;
        }

        List<VoidZoneSamplePoint> samples = BuildSamplesIgnoringGapsAndNarrow(sections);
        if (samples.Count < 2)
        {
            return;
        }

        ApplyStartAndEndPadding(samples);

        Mesh mesh = null;
        if (generateRenderer)
        {
            mesh = BuildVoidZoneMesh(samples);
        }

        CreateGeneratedObject(samples, mesh);
    }

    /// <summary>
    /// Elimina la zona de muerte generada actualmente.
    /// </summary>
    public void ClearGenerated()
    {
        Transform existing = transform.Find(generatedObjectName);
        if (existing == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(existing.gameObject);
        }
        else
        {
            Destroy(existing.gameObject);
        }
#else
        Destroy(existing.gameObject);
#endif
    }

    #endregion

    #region Sample Build

    private List<VoidZoneSamplePoint> BuildSamplesIgnoringGapsAndNarrow(
        IReadOnlyList<TrackSectionDefinition> sections)
    {
        List<VoidZoneSamplePoint> samples = new List<VoidZoneSamplePoint>();

        for (int i = 0; i < sections.Count; i++)
        {
            TrackSectionDefinition section = sections[i];
            AppendSectionSamples(samples, section);
        }

        return samples;
    }

    private void AppendSectionSamples(
        List<VoidZoneSamplePoint> target,
        TrackSectionDefinition section)
    {
        if (Mathf.Abs(section.TurnAngleDegrees) > 0.001f && section.TurnRadius > 0.001f)
        {
            AppendTurnSamples(target, section);
            return;
        }

        AppendLinearSamples(target, section);
    }

    private void AppendLinearSamples(
        List<VoidZoneSamplePoint> target,
        TrackSectionDefinition section)
    {
        int sampleCount = Mathf.Max(2, Mathf.CeilToInt(section.Length * linearSamplesPerUnit) + 1);

        Vector3 forward = section.EndPosition - section.StartPosition;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = section.StartForward;
        }

        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }
        else
        {
            forward.Normalize();
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float width = generationProfile.NormalTrackWidth + leftPadding + rightPadding;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 0f : i / (float)(sampleCount - 1);
            Vector3 position = Vector3.Lerp(section.StartPosition, section.EndPosition, t);
            position += Vector3.down * verticalOffset;

            AddSampleIfNeeded(target, new VoidZoneSamplePoint(position, forward, right, width));
        }
    }

    private void AppendTurnSamples(
        List<VoidZoneSamplePoint> target,
        TrackSectionDefinition section)
    {
        float signedAngle = section.TurnAngleDegrees;
        float radius = section.TurnRadius;

        Vector3 startForward = section.StartForward;
        startForward.y = 0f;
        if (startForward.sqrMagnitude <= 0.0001f)
        {
            startForward = Vector3.forward;
        }
        else
        {
            startForward.Normalize();
        }

        Vector3 startRight = Vector3.Cross(Vector3.up, startForward).normalized;
        float turnSign = Mathf.Sign(signedAngle);

        Vector3 center = section.StartPosition + (startRight * radius * turnSign);
        Vector3 radialStart = section.StartPosition - center;

        int sampleCount = Mathf.Max(
            minimumTurnSubdivisions,
            generationProfile.CurveSubdivisionCount + 1);

        float width = generationProfile.NormalTrackWidth + leftPadding + rightPadding;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 0f : i / (float)(sampleCount - 1);
            float currentAngle = signedAngle * t;

            Vector3 rotatedRadial = Quaternion.AngleAxis(currentAngle, Vector3.up) * radialStart;
            Vector3 position = center + rotatedRadial;
            position.y = Mathf.Lerp(section.StartPosition.y, section.EndPosition.y, t);
            position += Vector3.down * verticalOffset;

            Vector3 forward = Quaternion.AngleAxis(currentAngle, Vector3.up) * startForward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            AddSampleIfNeeded(target, new VoidZoneSamplePoint(position, forward, right, width));
        }
    }

    private void ApplyStartAndEndPadding(List<VoidZoneSamplePoint> samples)
    {
        if (samples == null || samples.Count < 2)
        {
            return;
        }

        VoidZoneSamplePoint first = samples[0];
        VoidZoneSamplePoint last = samples[samples.Count - 1];

        Vector3 paddedStartPosition = first.Position - (first.Forward * startPadding);
        Vector3 paddedEndPosition = last.Position + (last.Forward * endPadding);

        samples[0] = new VoidZoneSamplePoint(
            paddedStartPosition,
            first.Forward,
            first.Right,
            first.Width);

        samples[samples.Count - 1] = new VoidZoneSamplePoint(
            paddedEndPosition,
            last.Forward,
            last.Right,
            last.Width);
    }

    private static void AddSampleIfNeeded(
        List<VoidZoneSamplePoint> target,
        VoidZoneSamplePoint sample)
    {
        if (target.Count == 0)
        {
            target.Add(sample);
            return;
        }

        VoidZoneSamplePoint last = target[target.Count - 1];
        if (Vector3.Distance(last.Position, sample.Position) <= 0.001f)
        {
            return;
        }

        target.Add(sample);
    }

    #endregion

    #region Mesh Build

    private Mesh BuildVoidZoneMesh(List<VoidZoneSamplePoint> samples)
    {
        Mesh mesh = new Mesh
        {
            name = generatedObjectName,
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float accumulatedV = 0f;

        for (int i = 0; i < samples.Count - 1; i++)
        {
            VoidZoneSamplePoint a = samples[i];
            VoidZoneSamplePoint b = samples[i + 1];

            float segmentLength = Vector3.Distance(a.Position, b.Position);
            float v0 = accumulatedV;
            float v1 = accumulatedV + segmentLength;
            accumulatedV = v1;

            float halfWidthA = a.Width * 0.5f;
            float halfWidthB = b.Width * 0.5f;

            Vector3 aTopLeft = a.Position - (a.Right * halfWidthA);
            Vector3 aTopRight = a.Position + (a.Right * halfWidthA);
            Vector3 bTopLeft = b.Position - (b.Right * halfWidthB);
            Vector3 bTopRight = b.Position + (b.Right * halfWidthB);

            Vector3 aBottomLeft = aTopLeft - (Vector3.up * deathZoneThickness);
            Vector3 aBottomRight = aTopRight - (Vector3.up * deathZoneThickness);
            Vector3 bBottomLeft = bTopLeft - (Vector3.up * deathZoneThickness);
            Vector3 bBottomRight = bTopRight - (Vector3.up * deathZoneThickness);

            AddQuad(vertices, uvs, triangles,
                aTopLeft, bTopLeft, bTopRight, aTopRight,
                new Vector2(0f, v0), new Vector2(0f, v1), new Vector2(1f, v1), new Vector2(1f, v0));

            AddQuad(vertices, uvs, triangles,
                aBottomRight, bBottomRight, bBottomLeft, aBottomLeft,
                new Vector2(0f, v0), new Vector2(0f, v1), new Vector2(1f, v1), new Vector2(1f, v0));

            AddQuad(vertices, uvs, triangles,
                aTopLeft, aBottomLeft, bBottomLeft, bTopLeft,
                new Vector2(0f, v0), new Vector2(1f, v0), new Vector2(1f, v1), new Vector2(0f, v1));

            AddQuad(vertices, uvs, triangles,
                bTopRight, bBottomRight, aBottomRight, aTopRight,
                new Vector2(0f, v1), new Vector2(1f, v1), new Vector2(1f, v0), new Vector2(0f, v0));
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private static void AddQuad(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        Vector3 v3,
        Vector2 uv0,
        Vector2 uv1,
        Vector2 uv2,
        Vector2 uv3)
    {
        int startIndex = vertices.Count;

        vertices.Add(v0);
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);

        uvs.Add(uv0);
        uvs.Add(uv1);
        uvs.Add(uv2);
        uvs.Add(uv3);

        triangles.Add(startIndex + 0);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);

        triangles.Add(startIndex + 0);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);
    }

    #endregion

    #region Output

    private void CreateGeneratedObject(List<VoidZoneSamplePoint> samples, Mesh mesh)
    {
        GameObject generated = new GameObject(generatedObjectName);
        generated.transform.SetParent(transform);
        generated.transform.localPosition = Vector3.zero;
        generated.transform.localRotation = Quaternion.identity;
        generated.transform.localScale = Vector3.one;

        VoidZone rootVoidZone = generated.AddComponent<VoidZone>();
        CopyVoidZoneSettingsIfPresent(rootVoidZone);

        Rigidbody rootRigidbody = generated.AddComponent<Rigidbody>();
        rootRigidbody.isKinematic = true;
        rootRigidbody.useGravity = false;

        if (generateRenderer && mesh != null)
        {
            MeshFilter meshFilter = generated.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = generated.AddComponent<MeshRenderer>();
            if (debugMaterial != null)
            {
                meshRenderer.sharedMaterial = debugMaterial;
            }
        }

        if (generateSegmentTriggers)
        {
            BuildSegmentTriggers(generated.transform, samples, rootVoidZone);
        }
    }

    private void BuildSegmentTriggers(
        Transform parent,
        List<VoidZoneSamplePoint> samples,
        VoidZone rootVoidZone)
    {
        for (int i = 0; i < samples.Count - 1; i++)
        {
            VoidZoneSamplePoint a = samples[i];
            VoidZoneSamplePoint b = samples[i + 1];

            Vector3 segment = b.Position - a.Position;
            float segmentLength = segment.magnitude;

            if (segmentLength <= 0.001f)
            {
                continue;
            }

            Vector3 center = (a.Position + b.Position) * 0.5f;
            Vector3 forward = segment.normalized;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

            float averageWidth = (a.Width + b.Width) * 0.5f;
            float colliderLength = segmentLength + triggerLengthOverlap;

            GameObject segmentTrigger = new GameObject($"VoidTrigger_{i:D3}");
            segmentTrigger.transform.SetParent(parent);
            segmentTrigger.transform.position = center;
            segmentTrigger.transform.rotation = rotation;
            segmentTrigger.transform.localScale = Vector3.one;

            BoxCollider boxCollider = segmentTrigger.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.center = Vector3.zero;
            boxCollider.size = new Vector3(
                averageWidth,
                deathZoneThickness + triggerHeightPadding,
                colliderLength);

            VoidZoneTriggerProxy proxy = segmentTrigger.AddComponent<VoidZoneTriggerProxy>();
            proxy.SetRootVoidZone(rootVoidZone);

        }
    }

    private void CopyVoidZoneSettingsIfPresent(VoidZone targetVoidZone)
    {
        if (targetVoidZone == null)
        {
            return;
        }

        VoidZone source = GetComponent<VoidZone>();
        if (source == null)
        {
            return;
        }

        LayerMask sourceMask = GetPlayerLayersFromVoidZone(source);
        SetPlayerLayersToVoidZone(targetVoidZone, sourceMask);

        targetVoidZone.SetDebugLogs(source.EnableDebugLogs);
    }

    private static LayerMask GetPlayerLayersFromVoidZone(VoidZone source)
    {
        return source.PlayerLayers;
    }

    private static void SetPlayerLayersToVoidZone(VoidZone target, LayerMask mask)
    {
        target.SetPlayerLayers(mask);
    }

    #endregion

    #region Internal Types

    private readonly struct VoidZoneSamplePoint
    {
        public Vector3 Position { get; }
        public Vector3 Forward { get; }
        public Vector3 Right { get; }
        public float Width { get; }

        public VoidZoneSamplePoint(
            Vector3 position,
            Vector3 forward,
            Vector3 right,
            float width)
        {
            Position = position;
            Forward = forward;
            Right = right;
            Width = width;
        }
    }

    #endregion
}