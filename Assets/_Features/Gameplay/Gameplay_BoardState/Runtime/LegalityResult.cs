using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    internal readonly struct LegalityResult
    {
        private static readonly IReadOnlyList<LegalityBlocker> EmptyBlockers = Array.Empty<LegalityBlocker>();

        public LegalityResult(
            LegalityDomain domain,
            LegalityVerdict verdict,
            SurfaceCell cell,
            CubeTopologyState topology,
            ReservationStatus reservation,
            IReadOnlyList<LegalityBlocker> blockers)
        {
            Domain = domain;
            Verdict = verdict;
            Cell = cell;
            Topology = topology;
            Reservation = reservation;
            Blockers = blockers ?? EmptyBlockers;
        }

        public LegalityDomain Domain { get; }

        public LegalityVerdict Verdict { get; }

        public SurfaceCell Cell { get; }

        public CubeTopologyState Topology { get; }

        public ReservationStatus Reservation { get; }

        public IReadOnlyList<LegalityBlocker> Blockers { get; }

        public static LegalityResult Allowed(
            LegalityDomain domain,
            SurfaceCell cell,
            CubeTopologyState topology,
            ReservationStatus reservation = ReservationStatus.None)
        {
            return new LegalityResult(domain, LegalityVerdict.Allowed, cell, topology, reservation, EmptyBlockers);
        }
    }
}
