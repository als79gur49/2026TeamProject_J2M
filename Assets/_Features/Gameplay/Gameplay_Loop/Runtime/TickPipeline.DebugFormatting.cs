using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Commit;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Movement.Sorting;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    public sealed partial class TickPipeline
    {
        private static SimulationVelocity2 CreateDebugKinematicVelocity(
            int stepDirectionX,
            int stepDirectionY,
            int totalTicks)
        {
            var unitsPerTick = Math.Max(
                1,
                KinematicProgressResolver.ResolveProgressUnits(
                    1,
                    Math.Max(2, totalTicks),
                    SimulationFixed.UnitsPerCell));
            return new SimulationVelocity2(
                SimulationFixed.FromRaw(stepDirectionX * unitsPerTick),
                SimulationFixed.FromRaw(stepDirectionY * unitsPerTick));
        }
        private static string FormatCell(SurfaceCell cell)
        {
            return cell.face == FaceId.Floor
                ? $"({cell.x},{cell.y})"
                : $"{cell.face}({cell.x},{cell.y})";
        }
        private static bool TryGetStructuredLogValue(string line, string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(key))
            {
                return false;
            }

            var token = key + "=";
            var startIndex = line.IndexOf(token, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return false;
            }

            startIndex += token.Length;
            var endIndex = line.IndexOf('|', startIndex);
            value = endIndex >= 0
                ? line.Substring(startIndex, endIndex - startIndex)
                : line.Substring(startIndex);
            return true;
        }
    }
}
