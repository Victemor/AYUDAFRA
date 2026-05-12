using System.Collections.Generic;

/// <summary>
/// Prioridad usada para resolver conflictos entre reservas de contenido sobre la pista.
/// </summary>
public enum TrackSpawnPriority
{
    Low = 0,
    Medium = 10,
    High = 20,
    Critical = 30
}

/// <summary>
/// Registro de ocupación procedural sobre la pista.
/// </summary>
public sealed class TrackSpawnReservationMap
{
    private readonly List<Reservation> reservations = new List<Reservation>();

    /// <summary>
    /// Intenta reservar un área sobre la pista usando prioridad media.
    /// </summary>
    public bool TryReserve(
        float centerDistance,
        float centerLateral,
        float length,
        float width)
    {
        return TryReserve(centerDistance, centerLateral, length, width, TrackSpawnPriority.Medium);
    }

    /// <summary>
    /// Intenta reservar un área sobre la pista usando prioridad explícita.
    /// </summary>
    public bool TryReserve(
        float centerDistance,
        float centerLateral,
        float length,
        float width,
        TrackSpawnPriority priority)
    {
        float halfLength = length * 0.5f;
        float halfWidth = width * 0.5f;

        Reservation candidate = new Reservation(
            centerDistance - halfLength,
            centerDistance + halfLength,
            centerLateral - halfWidth,
            centerLateral + halfWidth,
            priority);

        for (int i = reservations.Count - 1; i >= 0; i--)
        {
            Reservation existing = reservations[i];

            if (!candidate.Overlaps(existing))
            {
                continue;
            }

            if (existing.Priority >= candidate.Priority)
            {
                return false;
            }

            reservations.RemoveAt(i);
        }

        reservations.Add(candidate);
        return true;
    }

    /// <summary>
    /// Limpia todas las reservas registradas.
    /// </summary>
    public void Clear()
    {
        reservations.Clear();
    }

    private readonly struct Reservation
    {
        private readonly float distanceMin;
        private readonly float distanceMax;
        private readonly float lateralMin;
        private readonly float lateralMax;

        public TrackSpawnPriority Priority { get; }

        public Reservation(
            float distanceMin,
            float distanceMax,
            float lateralMin,
            float lateralMax,
            TrackSpawnPriority priority)
        {
            this.distanceMin = distanceMin;
            this.distanceMax = distanceMax;
            this.lateralMin = lateralMin;
            this.lateralMax = lateralMax;
            Priority = priority;
        }

        public bool Overlaps(Reservation other)
        {
            bool overlapsDistance = distanceMin <= other.distanceMax && distanceMax >= other.distanceMin;
            bool overlapsLateral = lateralMin <= other.lateralMax && lateralMax >= other.lateralMin;

            return overlapsDistance && overlapsLateral;
        }
    }
}