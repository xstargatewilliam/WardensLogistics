using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;

namespace WardensLogistics.Core
{
    /// <summary>
    /// Textos da suíte em vários idiomas.
    ///
    /// - Idioma: o mesmo do jogo (Language = auto) ou um escolhido na configuração,
    ///   usando os identificadores do jogo: portuguese, english, spanish, french, german,
    ///   italian, polish, russian, japanese, korean, schinese, tchinese, turk.
    /// - Português e inglês vêm embutidos.
    /// - Traduções extras (ou ajustes) entram por arquivo, sem recompilar:
    ///   BepInEx/plugins/WardensLogistics/lang/&lt;idioma&gt;.txt, linhas "chave = texto".
    /// - Modelos com todos os textos atuais ficam em lang/templates/ e são regravados a
    ///   cada abertura do jogo; eles não são lidos como tradução. Para traduzir, copie um
    ///   modelo para a pasta lang/ com o nome do idioma.
    /// - Texto que falta num idioma cai para o inglês e, por fim, para a própria chave.
    /// </summary>
    internal static class Text
    {
        public const string Fallback = "english";

        // Cabeçalho que versões 0.1.x–0.3.0 gravavam nos modelos dentro de lang/ (lidos por engano).
        private static readonly string[] OldGeneratedHeaders =
        {
            "# Logistics Suite — translation file",
            "# Wardens Logistics — translation file",
        };

        private static readonly Dictionary<string, Dictionary<string, string>> BuiltIn =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["portuguese"] = new Dictionary<string, string>
                {
                    ["store.no_containers"] = "Nenhum baú por perto",
                    ["store.out_of_range"] = "Nenhum baú a até {0} m (o mais perto está a {1} m)",
                    ["store.nothing"] = "Nada para guardar nos baús próximos",
                    ["store.item"] = "{0} x{1} guardado(s)",
                    ["deposit.nothing"] = "Nenhum item da mochila combina com este baú",
                    ["find.hover_first"] = "Passe o mouse sobre um item e aperte {0}",
                    ["find.none"] = "{0}: não está em nenhum baú",
                    ["find.result"] = "{0}: {1} em {2} baú(s)",
                    ["find.recipe"] = "Ingredientes para {0}:",
                    ["find.close_to_see"] = "Feche a janela para ver os baús marcados",
                    ["search.title"] = "Buscar item: digite o nome · Enter procura o 1º · clique para escolher · Esc cancela",
                    ["search.empty"] = "Nenhum item com esse nome",
                    ["search.in_containers"] = "{0} nos baús",
                    ["search.none_stored"] = "nenhum nos baús",
                    ["pull.on"] = "Puxar dos baús: LIGADO",
                    ["pull.off"] = "Puxar dos baús: DESLIGADO",
                },
                ["english"] = new Dictionary<string, string>
                {
                    ["store.no_containers"] = "No containers nearby",
                    ["store.out_of_range"] = "No containers within {0} m (nearest is {1} m away)",
                    ["store.nothing"] = "Nothing to store in nearby containers",
                    ["store.item"] = "{0} x{1} stored",
                    ["deposit.nothing"] = "Nothing in your backpack matches this container",
                    ["find.hover_first"] = "Hover an item and press {0}",
                    ["find.none"] = "{0}: not in any container",
                    ["find.result"] = "{0}: {1} in {2} container(s)",
                    ["find.recipe"] = "Ingredients for {0}:",
                    ["find.close_to_see"] = "Close the window to see the marked containers",
                    ["search.title"] = "Find item: type a name · Enter finds the 1st · click to choose · Esc cancels",
                    ["search.empty"] = "No item with that name",
                    ["search.in_containers"] = "{0} in containers",
                    ["search.none_stored"] = "none in containers",
                    ["pull.on"] = "Pull from containers: ON",
                    ["pull.off"] = "Pull from containers: OFF",
                },
            };

        private static readonly Dictionary<string, Dictionary<string, string>> Languages =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public static void Init(string pluginFolder)
        {
            foreach (var pair in BuiltIn)
                Languages[pair.Key] = new Dictionary<string, string>(pair.Value);

            string langFolder = Path.Combine(pluginFolder, "lang");
            string templates = Path.Combine(langFolder, "templates");
            try
            {
                Directory.CreateDirectory(templates);
                foreach (var pair in BuiltIn)
                    WriteTemplate(Path.Combine(templates, pair.Key + ".txt"), pair.Value);

                foreach (var file in Directory.GetFiles(langFolder, "*.txt"))
                {
                    if (IsOldGeneratedTemplate(file))
                    {
                        File.Delete(file); // modelo antigo gravado pelo próprio mod: não é tradução
                        continue;
                    }
                    Load(file);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Could not load translation files from {langFolder}: {ex.Message}");
            }
        }

        /// <summary>Texto traduzido para o idioma atual, com {0}, {1}... preenchidos.</summary>
        public static string Get(string key, params object[] args)
        {
            string template = Lookup(CurrentLanguage(), key) ?? Lookup(Fallback, key) ?? key;
            return args.Length == 0 ? template : string.Format(template, args);
        }

        private static string Lookup(string language, string key)
        {
            return Languages.TryGetValue(language, out var dict) && dict.TryGetValue(key, out var value) ? value : null;
        }

        private static string CurrentLanguage()
        {
            string chosen = (Plugin.Language.Value ?? "auto").Trim();
            if (!chosen.Equals("auto", StringComparison.OrdinalIgnoreCase)) return chosen;
            try
            {
                var lang = Traverse.Create(typeof(SpaceCraft.Localization)).Field("currentLangage").GetValue<string>();
                return string.IsNullOrEmpty(lang) ? Fallback : lang;
            }
            catch
            {
                return Fallback;
            }
        }

        private static bool IsOldGeneratedTemplate(string path)
        {
            string language = Path.GetFileNameWithoutExtension(path);
            if (!BuiltIn.ContainsKey(language)) return false;
            using (var reader = new StreamReader(path))
            {
                string first = reader.ReadLine() ?? "";
                foreach (var header in OldGeneratedHeaders)
                    if (first.StartsWith(header, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static void WriteTemplate(string path, Dictionary<string, string> texts)
        {
            var lines = new List<string>
            {
                "# Wardens Logistics — template (regenerated on every launch; edits here are lost)",
                "# To translate or adjust texts: copy this file to the lang folder (one level up),",
                "# named after the game's language id (e.g. spanish.txt), and edit the copy.",
                "# Format: key = text. {0}, {1} are filled in by the mod. Lines starting with # are ignored.",
                "",
            };
            foreach (var pair in texts) lines.Add($"{pair.Key} = {pair.Value}");
            File.WriteAllLines(path, lines);
        }

        private static void Load(string path)
        {
            string language = Path.GetFileNameWithoutExtension(path);
            if (!Languages.TryGetValue(language, out var dict))
                Languages[language] = dict = new Dictionary<string, string>();

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (key.Length > 0 && value.Length > 0) dict[key] = value;
            }
        }
    }
}
