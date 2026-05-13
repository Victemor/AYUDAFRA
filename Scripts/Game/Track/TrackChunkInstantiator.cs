using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Instancia los GameObjects visuales y físicos a partir de una lista de chunks de superficie.
///
/// Extrae de <see cref="TrackGeneratorController"/> toda la responsabilidad de
/// "convertir datos de chunk en GameObjects en escena", que es independiente
/// de la lógica de generación del mapa.
/// </summary>
public static class TrackChunkInstantiator
{
    #region Public API

    /// <summary>
    /// Instancia todos los chunks de una pista bajo el Transform padre indicado.
    /// </summary>
    /// <param name="surfaceChunks">Lista de chunks a instanciar.</param>
    /// <param name="parent">Transform padre que contendrá todos los GameObjects generados.</param>
    /// <param name="settings">Configuración de física, naming y layer.</param>
    /// <param name="visualProfile">Perfil visual activo. Si es null, no se generarán objetos.</param>
    public static void InstantiateChunks(
        IReadOnlyList<TrackSurfaceChunkDefinition> surfaceChunks,
        Transform parent,
        in TrackChunkInstantiatorSettings settings,
        TrackGenerationProfile visualProfile)
    {
        if (surfaceChunks == null || surfaceChunks.Count == 0 || parent == null || visualProfile == null)
        {
            return;
        }

        for (int i = 0; i < surfaceChunks.Count; i++)
        {
            CreateChunkObject(surfaceChunks[i], parent, in settings, visualProfile);
        }
    }

    #endregion

    #region Chunk Object Creation

    private static void CreateChunkObject(
        TrackSurfaceChunkDefinition chunk,
        Transform parent,
        in TrackChunkInstantiatorSettings settings,
        TrackGenerationProfile visualProfile)
    {
        TrackMeshBuilder.TrackMeshBuildResult result =
            TrackMeshBuilder.BuildChunkMesh(chunk, visualProfile);

        GameObject chunkObject = new GameObject($"{settings.ChunkObjectNamePrefix}{chunk.ChunkIndex:D2}");

        AssignLayer(chunkObject, settings.GeneratedTrackLayer);

        chunkObject.transform.SetParent(parent);
        chunkObject.transform.localPosition = Vector3.zero;
        chunkObject.transform.localRotation = Quaternion.identity;
        chunkObject.transform.localScale    = Vector3.one;

        MeshFilter   meshFilter   = chunkObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();

        meshFilter.sharedMesh          = result.Mesh;
        meshRenderer.sharedMaterials   = result.Materials;

        if (settings.GenerateColliders && result.Mesh != null)
        {
            CreateChunkPhysics(chunk, chunkObject, result.Mesh, in settings, visualProfile);
        }
    }

    #endregion

    #region Physics

    private static void CreateChunkPhysics(
        TrackSurfaceChunkDefinition chunk,
        GameObject chunkObject,
        Mesh mesh,
        in TrackChunkInstantiatorSettings settings,
        TrackGenerationProfile visualProfile)
    {
        if (chunk.StructureType == TrackStructureType.RailTrack && settings.UsePrimitiveRailColliders)
        {
            CreateRailPrimitiveColliders(chunk, chunkObject.transform, in settings, visualProfile);
            return;
        }

        MeshCollider meshCollider = chunkObject.AddComponent<MeshCollider>();
        meshCollider.cookingOptions =
            MeshColliderCookingOptions.EnableMeshCleaning   |
            MeshColliderCookingOptions.WeldColocatedVertices |
            MeshColliderCookingOptions.CookForFasterSimulation;
        meshCollider.sharedMesh = mesh;
    }

    private static void CreateRailPrimitiveColliders(
        TrackSurfaceChunkDefinition chunk,
        Transform parent,
        in TrackChunkInstantiatorSettings settings,
        TrackGenerationProfile visualProfile)
    {
        if (chunk?.Samples == null || chunk.Samples.Count < 2)
        {
            return;
        }

        CreateSingleRailColliders(chunk, parent, -1f, "Left",  in settings);
        CreateSingleRailColliders(chunk, parent,  1f, "Right", in settings);
    }

    private static void CreateSingleRailColliders(
        TrackSurfaceChunkDefinition chunk,
        Transform parent,
        float sideSign,
        string sideName,
        in TrackChunkInstantiatorSettings settings)
    {
        for (int i = 0; i < chunk.Samples.Count - 1; i++)
        {
            TrackLayoutSamplePoint startSample = chunk.Samples[i];
            TrackLayoutSamplePoint endSample   = chunk.Samples[i + 1];

            ResolveRailColliderFrame(startSample, sideSign,
                out Vector3 startCenter, out float startRawRadius);

            ResolveRailColliderFrame(endSample, sideSign,
                out Vector3 endCenter, out float endRawRadius);

            Vector3 segment       = endCenter - startCenter;
            float segmentLength   = segment.magnitude;

            if (segmentLength <= 0.05f)
            {
                continue;
            }

            float averageRawRadius = (startRawRadius + endRawRadius) * 0.5f;
            float finalRadius      = Mathf.Max(
                settings.MinimumRailColliderRadius,
                averageRawRadius * settings.RailColliderRadiusMultiplier);

            GameObject colliderObject = new GameObject($"Rail_{sideName}_{i:D3}");
            AssignLayer(colliderObject, settings.GeneratedTrackLayer);
            colliderObject.transform.SetParent(parent);
            colliderObject.transform.position = (startCenter + endCenter) * 0.5f;
            colliderObject.transform.rotation = Quaternion.LookRotation(segment.normalized, Vector3.up);
            colliderObject.transform.localScale = Vector3.one;

            CapsuleCollider capsule = colliderObject.AddComponent<CapsuleCollider>();
            capsule.direction = 2; // Z axis aligned with segment
            capsule.radius    = finalRadius;
            capsule.height    = segmentLength + finalRadius * 2f;

            if (settings.RailPhysicMaterial != null)
            {
                capsule.sharedMaterial = settings.RailPhysicMaterial;
            }
        }
    }

    /// <summary>
    /// Calcula el centro y radio raw de un punto de rail.
    /// El centro se desplaza del centro del track en la dirección Right según la separación de rieles.
    /// El radio es la mitad del ancho del riel.
    ///
    /// Los datos de geometría de rail se leen directamente del <see cref="TrackLayoutSamplePoint"/>,
    /// donde viven por diseño (cada sample ya los recibe del builder de secciones).
    /// </summary>
    private static void ResolveRailColliderFrame(
        TrackLayoutSamplePoint sample,
        float sideSign,
        out Vector3 center,
        out float rawRadius)
    {
        // Right se deriva del Forward del sample si no estuviera disponible por algún edge case.
        Vector3 right = sample.Right.sqrMagnitude > 0.0001f
            ? sample.Right
            : Vector3.Cross(Vector3.up, sample.Forward).normalized;

        center    = sample.Position + right * (sideSign * sample.RailSeparation * 0.5f);
        rawRadius = sample.RailWidth * 0.5f;
    }

    #endregion

    #region Helpers

    private static void AssignLayer(GameObject go, int layer)
    {
        if (layer < 0)
        {
            return;
        }

        go.layer = layer;
    }

    #endregion
}