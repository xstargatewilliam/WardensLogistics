using System.Collections.Generic;
using SpaceCraft;
using UnityEngine;

namespace WardensLogistics.Terraformacao
{
    /// <summary>
    /// Assistente de terraformação: para cada medida do planeta (oxigênio, calor, pressão,
    /// plantas, insetos, animais, biomassa e índice de terraformação) mostra o valor, quanto
    /// sobe por segundo, o próximo desbloqueio e em quanto tempo ele chega no ritmo atual,
    /// e a melhor construção já liberada para acelerar essa medida.
    /// Usa só valores que o jogo sincroniza para todos, então funciona também para quem
    /// entra no jogo de outra pessoa.
    /// </summary>
    internal static class Terraform
    {
        public static readonly DataConfig.WorldUnitType[] Units =
        {
            DataConfig.WorldUnitType.Terraformation,
            DataConfig.WorldUnitType.Oxygen,
            DataConfig.WorldUnitType.Heat,
            DataConfig.WorldUnitType.Pressure,
            DataConfig.WorldUnitType.Biomass,
            DataConfig.WorldUnitType.Plants,
            DataConfig.WorldUnitType.Insects,
            DataConfig.WorldUnitType.Animals,
        };

        public sealed class UnitRow
        {
            public DataConfig.WorldUnitType Type;
            public string Name;
            public string Value;
            public string Rate;
            public float PerSecond;
            public double Current;
            public double NextValue = -1;     // -1: nada mais para desbloquear
            public string NextValueText;
            public List<Group> NextGroups = new List<Group>();
            public string NextStage;          // etapa de terraformação (só no índice)
            public double Eta = -1;           // segundos; -1: parado ou sem próximo
            public Group BestBuilder;
            public float BestBuilderRate;
            public string BestBuilderRateText;
        }

        public sealed class Report
        {
            public bool Available;
            public List<UnitRow> Rows = new List<UnitRow>();
            public UnitRow Soonest;           // desbloqueio mais próximo no ritmo atual
            public List<UnitRow> Stalled = new List<UnitRow>(); // medidas paradas com desbloqueio pendente e algo para construir
        }

        public static Report Analyze()
        {
            var r = new Report();
            var handler = Managers.GetManager<WorldUnitsHandler>();
            if (handler == null || !handler.AreUnitsInited()) return r;
            r.Available = true;

            var groups = GroupsHandler.GetAllGroups();
            foreach (var type in Units)
            {
                var unit = handler.GetUnit(type);
                if (unit == null) continue;
                var row = new UnitRow
                {
                    Type = type,
                    Name = Readable.GetWorldUnitLabel(type),
                    Value = unit.GetValueString(),
                    PerSecond = unit.GetCurrentValuePersSec(),
                    Current = unit.GetValue(),
                };
                row.Rate = (row.PerSecond >= 0 ? "+" : "") + unit.GetDisplayStringForValue(row.PerSecond, false) + "/s";

                // Próximo desbloqueio desta medida
                double next = double.MaxValue;
                foreach (var g in groups)
                {
                    var info = g?.GetUnlockingInfos();
                    if (info == null || info.GetWorldUnit() != type) continue;
                    double v = info.GetUnlockingValue();
                    if (v <= row.Current || v > next) continue;
                    if (IsUnlocked(g) || !info.GetIsAvailableOnCurrentPlanet()) continue;
                    if (v < next) { next = v; row.NextGroups.Clear(); }
                    row.NextGroups.Add(g);
                }

                // Etapas de terraformação desta medida
                var planet = Managers.GetManager<PlanetLoader>()?.GetCurrentPlanetData();
                if (planet != null)
                {
                    TerraformStage stage = null;
                    foreach (var s in planet.GetPlanetTerraformationStages())
                    {
                        if (s == null || s.GetWorldUnitType() != type) continue;
                        double v = s.GetStageStartValue();
                        if (v > row.Current && (stage == null || v < stage.GetStageStartValue())) stage = s;
                    }
                    if (stage != null && stage.GetStageStartValue() <= next)
                    {
                        if (stage.GetStageStartValue() < next) row.NextGroups.Clear();
                        next = stage.GetStageStartValue();
                        row.NextStage = Readable.GetTerraformStageName(stage);
                    }
                }

                if (next < double.MaxValue)
                {
                    row.NextValue = next;
                    row.NextValueText = unit.GetDisplayStringForValue(next);
                    if (row.PerSecond > 0f) row.Eta = (next - row.Current) / row.PerSecond;
                }

                // Melhor construção liberada para esta medida
                foreach (var g in groups)
                {
                    if (!(g is GroupConstructible gc)) continue;
                    float gen = gc.GetGroupUnitGeneration(type);
                    if (gen <= row.BestBuilderRate || !IsUnlocked(g)) continue;
                    row.BestBuilder = g;
                    row.BestBuilderRate = gen;
                }
                if (row.BestBuilder != null)
                    row.BestBuilderRateText = "+" + unit.GetDisplayStringForValue(row.BestBuilderRate, false) + "/s";

                r.Rows.Add(row);
                if (row.Eta >= 0 && (r.Soonest == null || row.Eta < r.Soonest.Eta)) r.Soonest = row;
                if (row.NextValue >= 0 && row.PerSecond <= 0f && type != DataConfig.WorldUnitType.Terraformation && row.BestBuilder != null) r.Stalled.Add(row);
            }
            return r;
        }

        public static bool IsUnlocked(Group g)
        {
            var info = g.GetUnlockingInfos();
            if (info != null && info.GetIsUnlocked()) return true;
            var u = UnlockedGroupsHandler.Instance;
            return u != null && u.IsGloballyUnlocked(g);
        }

        /// <summary>Tempo legível: "45 s", "12 min", "3 h 20 min", "2 d 5 h".</summary>
        public static string Duration(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return "—";
            if (seconds < 60) return $"{seconds:0} s";
            double m = seconds / 60;
            if (m < 60) return $"{m:0} min";
            double h = m / 60;
            if (h < 24) return $"{(int)h} h {(int)(m % 60):00} min";
            double d = h / 24;
            if (d < 365) return $"{(int)d} d {(int)(h % 24)} h";
            return "> 1 a";
        }
    }
}
