using System;
using System.Collections.Generic;
using HarmonyLib;
using SpaceCraft;
using UnityEngine;

namespace WardensLogistics.Logistica
{
    /// <summary>
    /// Produção por minuto: conta cada item que uma máquina cria (extratores, geradores,
    /// colmeias, fazendas, autocrafters, conversores, cultivadores, desintegrador) nos
    /// últimos minutos. O jogo cria esses itens sempre pelo mesmo caminho
    /// (InventoriesHandler.AddItemToInventory com um tipo de item); itens criados na mochila
    /// (craft do jogador) e comprados nos foguetes de troca não contam.
    /// As máquinas rodam em quem hospeda, então a contagem existe no jogo solo e no anfitrião.
    /// </summary>
    internal static class Production
    {
        public const float WindowSeconds = 300f;
        private static readonly Dictionary<Group, Queue<float>> Events = new Dictionary<Group, Queue<float>>();
        private static float _since = -1f;

        public struct Rate { public Group Group; public float PerMinute; public int Total; }

        private static void Record(Group group)
        {
            if (group == null) return;
            float now = Time.time;
            if (_since < 0f || now < _since) _since = now;
            if (!Events.TryGetValue(group, out var q)) Events[group] = q = new Queue<float>();
            q.Enqueue(now);
            Prune(q, now);
        }

        private static void Prune(Queue<float> q, float now)
        {
            while (q.Count > 0 && (now - q.Peek() > WindowSeconds || q.Peek() > now)) q.Dequeue();
        }

        /// <summary>Há quanto tempo está medindo (até a janela), em segundos.</summary>
        public static float MeasuredSeconds => _since < 0f ? 0f : Mathf.Min(WindowSeconds, Time.time - _since);

        public static List<Rate> Rates()
        {
            var list = new List<Rate>();
            float now = Time.time;
            float minutes = Mathf.Max(MeasuredSeconds, 30f) / 60f; // evita números absurdos no começo
            foreach (var kv in Events)
            {
                Prune(kv.Value, now);
                if (kv.Value.Count == 0) continue;
                list.Add(new Rate { Group = kv.Key, PerMinute = kv.Value.Count / minutes, Total = kv.Value.Count });
            }
            list.Sort((a, b) => b.PerMinute.CompareTo(a.PerMinute));
            return list;
        }

        public static void Clear()
        {
            Events.Clear();
            _since = -1f;
        }

        [HarmonyPatch(typeof(InventoriesHandler), nameof(InventoriesHandler.AddItemToInventory),
            new[] { typeof(Group), typeof(Inventory), typeof(Action<bool, int>) })]
        private static class CreatePatch
        {
            private static void Prefix(Group group, Inventory inventory, ref Action<bool, int> result)
            {
                if (group == null || inventory == null || Pilhas.Stacks.IsRocketOrBackpack(inventory.GetId())) return;
                var original = result;
                result = (ok, id) =>
                {
                    if (ok) Record(group);
                    original?.Invoke(ok, id);
                };
            }
        }

        [HarmonyPatch(typeof(PlanetLoader), "HandleDataAfterLoad")]
        private static class ResetOnLoad
        {
            private static void Prefix() => Clear();
        }
    }
}
