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
                    ["w.title"] = "Central Warden",
                    ["tl.set"] = "Câmera do timelapse gravada aqui. Uma foto a cada {0} min de jogo",
                    ["tl.first"] = "Timelapse: fotos em {0}",
                    ["legend.timelapse"] = "timelapse",
                    ["w.tab.terraform"] = "Terraformação",
                    ["w.tab.production"] = "Produção",
                    ["w.tab.drones"] = "Drones",
                    ["tf.loading"] = "Aguardando o planeta carregar...",
                    ["tf.suggestion"] = "Sugestão",
                    ["tf.soonest"] = "Próximo desbloqueio: <b>{0}</b> em {1} ({2}).",
                    ["tf.speed_up"] = "Para acelerar: construa mais <b>{0}</b> ({1} cada).",
                    ["tf.stalled"] = "{0} está parado e ainda tem desbloqueios pela frente. Construa <b>{1}</b> para ele voltar a subir.",
                    ["tf.any_builder"] = "algo que produza essa medida",
                    ["tf.nothing_next"] = "Nada pendente no ritmo atual. Os Wardens estariam orgulhosos.",
                    ["tf.units"] = "Medidas do planeta",
                    ["tf.col_unit"] = "Medida",
                    ["tf.col_value"] = "Valor",
                    ["tf.col_rate"] = "Ritmo",
                    ["tf.col_next"] = "Próximo desbloqueio",
                    ["tf.col_eta"] = "Chega em",
                    ["tf.col_best"] = "Melhor construção",
                    ["tf.stopped"] = "parado",
                    ["tf.and_more"] = "{0} e mais {1}",
                    ["tf.note"] = "Os tempos usam o ritmo atual; cada máquina nova encurta a espera. \"Melhor construção\" é a já liberada que mais gera aquela medida.",
                    ["prod.title"] = "Produção das máquinas",
                    ["prod.explain"] = "Itens criados por extratores, geradores, colmeias, fazendas, autocrafters e afins. Medindo há {0} (média dos últimos {1} min).",
                    ["prod.none"] = "Nenhuma máquina produziu desde que o mundo carregou. Espere um pouco e volte aqui.",
                    ["prod.host_only"] = "As máquinas trabalham em quem hospeda a partida; a produção aparece no jogo solo e para o anfitrião.",
                    ["prod.col_rate"] = "Por minuto",
                    ["prod.col_hour"] = "Por hora",
                    ["xray.title"] = "Raio-x da logística",
                    ["xray.host_only"] = "Só para quem hospeda a partida",
                    ["xray.ok"] = "Logística em ordem",
                    ["xray.more"] = "+{0} problemas",
                    ["xray.container"] = "baú",
                    ["xray.s.no_station"] = "Sem estação de drones",
                    ["xray.s.no_energy"] = "Energia insuficiente",
                    ["xray.s.no_supplier"] = "{0} · ninguém oferece",
                    ["xray.s.supplier_empty"] = "{0} · fornecedores vazios",
                    ["xray.s.need_drones"] = "{0} entregas sem drone",
                    ["xray.s.supply_full"] = "{0} encalhado · {1} cheio",
                    ["legend.store"] = "guardar",
                    ["legend.find"] = "buscar",
                    ["legend.xray"] = "raio-x",
                    ["legend.xray_detail"] = "central",
                    ["legend.deposit"] = "depositar",
                    ["legend.click"] = "Clique",
                    ["legend.move_stack"] = "mover pilha",
                    ["legend.move_all"] = "mover tudo",
                    ["legend.where"] = "onde está",
                    ["legend.ingredients"] = "ingredientes",
                    ["legend.buffer"] = "buffer",
                    ["legend.copy_logistics"] = "copiar/colar logística",
                    ["tools.copied"] = "Logística copiada: {0} pedidos, {1} ofertas",
                    ["tools.pasted"] = "Logística colada: {0} pedidos, {1} ofertas",
                    ["tools.nothing_copied"] = "Nada copiado. Abra um baú configurado e use Ctrl+C",
                    ["tools.buffer_on"] = "Modo buffer ligado: este baú pede e oferece os mesmos itens",
                    ["tools.buffer_off"] = "Modo buffer desligado",
                    ["xray.t.no_station"] = "Nenhuma estação de drones",
                    ["xray.why.no_station"] = "Há baús pedindo itens, mas sem estação não existem drones para fazer as entregas.",
                    ["xray.fix.no_station"] = "Construa uma estação de drones e coloque drones dentro dela.",
                    ["xray.t.no_energy"] = "Energia insuficiente",
                    ["xray.why.no_energy"] = "Os drones só voam quando a energia produzida no planeta cobre o consumo. Enquanto falta energia, todas as entregas ficam paradas.",
                    ["xray.fix.no_energy"] = "Construa mais geradores ou desligue máquinas que consomem muita energia.",
                    ["xray.t.no_supplier"] = "{0}: ninguém oferece",
                    ["xray.why.no_supplier"] = "{1} baú(s) pedem {0}, mas nenhum baú está configurado para oferecer esse item. Sem oferta, os drones não têm de onde buscar.",
                    ["xray.fix.no_supplier"] = "Num baú que guarde {0}, abra a logística e marque {0} como oferta.",
                    ["xray.t.supplier_empty"] = "{0}: fornecedores vazios",
                    ["xray.why.supplier_empty"] = "{1} baú(s) oferecem {0}, mas estão vazios, então os pedidos não são atendidos.",
                    ["xray.fix.supplier_empty"] = "Coloque {0} num desses baús, ou ligue a eles uma máquina que produza esse item.",
                    ["xray.t.need_drones"] = "Entregas sem drone livre",
                    ["xray.why.need_drones"] = "{0} entregas estão esperando e não há nenhum drone parado nas estações para fazê-las.",
                    ["xray.fix.need_drones"] = "Fabrique mais drones e coloque nas estações, ou construa outra estação mais perto dos baús.",
                    ["xray.t.supply_full"] = "{0} encalhado",
                    ["xray.why.supply_full"] = "{1} oferece {0} e está cheio, mas nenhum baú pede esse item. Ele só acumula.",
                    ["xray.fix.supply_full"] = "Configure algum baú para pedir {0}, ou tire {0} da oferta desse baú.",
                    ["xray.w.close"] = "Esc ou Ctrl+{0} fecha",
                    ["xray.w.host_only"] = "O Raio-x lê a logística no servidor da partida. Ele funciona no jogo solo e para quem hospeda; como convidado, peça ao anfitrião para abrir.",
                    ["xray.w.overview"] = "Visão geral",
                    ["xray.w.drones"] = "Drones",
                    ["xray.w.drones_d"] = "{0} voando · {1} parados nas estações. Os parados ficam livres para novas entregas.",
                    ["xray.w.stations"] = "Estações",
                    ["xray.w.stations_d"] = "Guardam os drones, recarregam e soltam um quando surge uma entrega.",
                    ["xray.w.tasks"] = "Entregas",
                    ["xray.w.tasks_d"] = "{0} esperando drone · {1} sendo buscadas · {2} a caminho do pedido.",
                    ["xray.w.energy"] = "Energia",
                    ["xray.w.energy_ok"] = "Suficiente: os drones podem voar.",
                    ["xray.w.energy_low"] = "Insuficiente: os drones param até a energia voltar.",
                    ["xray.w.energy_na"] = "Sem estação de drones, a energia não afeta a logística.",
                    ["xray.w.containers"] = "Baús com pedido: {0} · com oferta: {1}. Um baú com pedido recebe itens; um com oferta cede itens para os drones levarem.",
                    ["xray.w.problems"] = "Problemas ({0})",
                    ["xray.w.all_good"] = "Nenhum problema: os pedidos estão encontrando ofertas e há drones para as entregas.",
                    ["xray.w.no_logistics"] = "Nenhum baú usa a logística ainda. Abra um baú e use o botão de logística para definir o que ele pede ou oferece.",
                    ["xray.w.why"] = "Por quê:",
                    ["xray.w.fix"] = "Como resolver:",
                    ["xray.w.where"] = "Baús:",
                    ["xray.w.and_more"] = "e mais {0}",
                    ["xray.w.markers_hint"] = "Dica: com o painel do {0} aberto, os baús envolvidos ficam marcados no mundo.",
                    ["xray.w.items"] = "Itens na logística",
                    ["xray.w.no_items"] = "Nenhum item com pedido ou oferta.",
                    ["xray.w.col_item"] = "Item",
                    ["xray.w.col_demand"] = "Pedem",
                    ["xray.w.col_supply"] = "Oferecem",
                    ["xray.w.col_stock"] = "Estoque",
                    ["xray.w.col_delivery"] = "Em entrega",
                    ["xray.w.col_status"] = "Situação",
                    ["xray.w.st_ok"] = "Funcionando",
                    ["xray.w.st_no_supplier"] = "Ninguém oferece",
                    ["xray.w.st_empty"] = "Fornecedores vazios",
                    ["xray.w.st_stuck"] = "Encalhado",
                    ["xray.w.st_idle"] = "Só oferta, sem pedido",
                    ["stack.conflict"] = "Outro mod de empilhamento instalado: {0}. Desinstale-o; dois ao mesmo tempo podem corromper inventários.",
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
                    ["w.title"] = "Warden Hub",
                    ["tl.set"] = "Timelapse camera saved here. One photo every {0} game minutes",
                    ["tl.first"] = "Timelapse: photos in {0}",
                    ["legend.timelapse"] = "timelapse",
                    ["w.tab.terraform"] = "Terraforming",
                    ["w.tab.production"] = "Production",
                    ["w.tab.drones"] = "Drones",
                    ["tf.loading"] = "Waiting for the planet to load...",
                    ["tf.suggestion"] = "Suggestion",
                    ["tf.soonest"] = "Next unlock: <b>{0}</b> in {1} ({2}).",
                    ["tf.speed_up"] = "To speed it up: build more <b>{0}</b> ({1} each).",
                    ["tf.stalled"] = "{0} is not growing and still has unlocks ahead. Build <b>{1}</b> to get it moving.",
                    ["tf.any_builder"] = "something that produces it",
                    ["tf.nothing_next"] = "Nothing pending at the current pace. The Wardens would be proud.",
                    ["tf.units"] = "Planet measures",
                    ["tf.col_unit"] = "Measure",
                    ["tf.col_value"] = "Value",
                    ["tf.col_rate"] = "Pace",
                    ["tf.col_next"] = "Next unlock",
                    ["tf.col_eta"] = "Arrives in",
                    ["tf.col_best"] = "Best building",
                    ["tf.stopped"] = "stalled",
                    ["tf.and_more"] = "{0} and {1} more",
                    ["tf.note"] = "Times use the current pace; every new machine shortens the wait. \"Best building\" is the unlocked one that generates the most of that measure.",
                    ["prod.title"] = "Machine production",
                    ["prod.explain"] = "Items created by extractors, generators, hives, farms, auto-crafters and the like. Measuring for {0} (average of the last {1} min).",
                    ["prod.none"] = "No machine has produced anything since the world loaded. Give it a moment and come back.",
                    ["prod.host_only"] = "Machines run on the host; production shows in single player and for the host.",
                    ["prod.col_rate"] = "Per minute",
                    ["prod.col_hour"] = "Per hour",
                    ["xray.title"] = "Logistics X-Ray",
                    ["xray.host_only"] = "Host only",
                    ["xray.ok"] = "Logistics running fine",
                    ["xray.more"] = "+{0} problems",
                    ["xray.container"] = "container",
                    ["xray.s.no_station"] = "No drone station",
                    ["xray.s.no_energy"] = "Not enough energy",
                    ["xray.s.no_supplier"] = "{0} · nobody supplies it",
                    ["xray.s.supplier_empty"] = "{0} · suppliers empty",
                    ["xray.s.need_drones"] = "{0} deliveries without a drone",
                    ["xray.s.supply_full"] = "{0} stuck · {1} full",
                    ["legend.store"] = "store",
                    ["legend.find"] = "find",
                    ["legend.xray"] = "X-ray",
                    ["legend.xray_detail"] = "hub",
                    ["legend.deposit"] = "deposit",
                    ["legend.click"] = "Click",
                    ["legend.move_stack"] = "move stack",
                    ["legend.move_all"] = "move all",
                    ["legend.where"] = "find item",
                    ["legend.ingredients"] = "ingredients",
                    ["legend.buffer"] = "buffer",
                    ["legend.copy_logistics"] = "copy/paste logistics",
                    ["tools.copied"] = "Logistics copied: {0} demands, {1} supplies",
                    ["tools.pasted"] = "Logistics pasted: {0} demands, {1} supplies",
                    ["tools.nothing_copied"] = "Nothing copied. Open a configured container and press Ctrl+C",
                    ["tools.buffer_on"] = "Buffer mode on: this container demands and supplies the same items",
                    ["tools.buffer_off"] = "Buffer mode off",
                    ["xray.t.no_station"] = "No drone station",
                    ["xray.why.no_station"] = "Containers are demanding items, but without a station there are no drones to deliver them.",
                    ["xray.fix.no_station"] = "Build a drone station and put drones in it.",
                    ["xray.t.no_energy"] = "Not enough energy",
                    ["xray.why.no_energy"] = "Drones only fly while the planet's energy production covers consumption. Until then, every delivery is on hold.",
                    ["xray.fix.no_energy"] = "Build more generators or turn off power-hungry machines.",
                    ["xray.t.no_supplier"] = "{0}: nobody supplies it",
                    ["xray.why.no_supplier"] = "{1} container(s) demand {0}, but no container is set to supply it, so drones have nowhere to pick it up.",
                    ["xray.fix.no_supplier"] = "In a container that holds {0}, open logistics and mark {0} as supply.",
                    ["xray.t.supplier_empty"] = "{0}: suppliers empty",
                    ["xray.why.supplier_empty"] = "{1} container(s) supply {0}, but they are empty, so demands are not met.",
                    ["xray.fix.supplier_empty"] = "Put {0} in one of those containers, or connect a machine that produces it.",
                    ["xray.t.need_drones"] = "Deliveries without a free drone",
                    ["xray.why.need_drones"] = "{0} deliveries are waiting and no drone is idle in the stations to take them.",
                    ["xray.fix.need_drones"] = "Craft more drones and place them in stations, or build another station closer to the containers.",
                    ["xray.t.supply_full"] = "{0} stuck",
                    ["xray.why.supply_full"] = "{1} supplies {0} and is full, but no container demands it. It just piles up.",
                    ["xray.fix.supply_full"] = "Set some container to demand {0}, or remove {0} from that container's supply.",
                    ["xray.w.close"] = "Esc or Ctrl+{0} closes",
                    ["xray.w.host_only"] = "The X-Ray reads logistics on the game's server. It works in single player and for the host; as a guest, ask the host to open it.",
                    ["xray.w.overview"] = "Overview",
                    ["xray.w.drones"] = "Drones",
                    ["xray.w.drones_d"] = "{0} flying · {1} idle in stations. Idle drones are free for new deliveries.",
                    ["xray.w.stations"] = "Stations",
                    ["xray.w.stations_d"] = "Store and recharge drones, and release one when a delivery appears.",
                    ["xray.w.tasks"] = "Deliveries",
                    ["xray.w.tasks_d"] = "{0} waiting for a drone · {1} being picked up · {2} on the way.",
                    ["xray.w.energy"] = "Energy",
                    ["xray.w.energy_ok"] = "Enough: drones can fly.",
                    ["xray.w.energy_low"] = "Not enough: drones stop until energy returns.",
                    ["xray.w.energy_na"] = "With no drone station, energy does not affect logistics.",
                    ["xray.w.containers"] = "Containers with demand: {0} · with supply: {1}. Demand containers receive items; supply containers give items for drones to carry.",
                    ["xray.w.problems"] = "Problems ({0})",
                    ["xray.w.all_good"] = "No problems: demands are finding supplies and there are drones for the deliveries.",
                    ["xray.w.no_logistics"] = "No container uses logistics yet. Open a container and use its logistics button to set what it demands or supplies.",
                    ["xray.w.why"] = "Why:",
                    ["xray.w.fix"] = "How to fix:",
                    ["xray.w.where"] = "Containers:",
                    ["xray.w.and_more"] = "and {0} more",
                    ["xray.w.markers_hint"] = "Tip: with the {0} panel open, the containers involved are marked in the world.",
                    ["xray.w.items"] = "Items in logistics",
                    ["xray.w.no_items"] = "No item with demand or supply.",
                    ["xray.w.col_item"] = "Item",
                    ["xray.w.col_demand"] = "Demanded",
                    ["xray.w.col_supply"] = "Supplied",
                    ["xray.w.col_stock"] = "Stock",
                    ["xray.w.col_delivery"] = "In delivery",
                    ["xray.w.col_status"] = "Status",
                    ["xray.w.st_ok"] = "Working",
                    ["xray.w.st_no_supplier"] = "Nobody supplies",
                    ["xray.w.st_empty"] = "Suppliers empty",
                    ["xray.w.st_stuck"] = "Stuck",
                    ["xray.w.st_idle"] = "Supply only, no demand",
                    ["stack.conflict"] = "Another stacking mod is installed: {0}. Uninstall it; two at once can corrupt inventories.",
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
