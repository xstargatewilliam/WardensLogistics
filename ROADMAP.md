# Roteiro — Wardens Logistics

## Objetivo

Juntar num mod só o que hoje está espalhado em uns 12 mods de armazenamento e
logística do Planet Crafter, corrigir os problemas encontrados neles (multiplayer,
desempenho, teclas em conflito, baús de outros mods ignorados) e acrescentar o que
nenhum faz: entender e planejar a base — diagnóstico da logística de drones,
assistente de terraformação e registro da evolução do planeta.

Identidade: os Wardens foram uma civilização avançada; o mod é tecnologia Warden
aplicada à sua base.

Página no Nexus: https://www.nexusmods.com/planetcrafter/mods/269

## Estado (0.10.0)

| Módulo | Situação |
|---|---|
| Núcleo: índice de baús | ✅ testado |
| Armazém: guardar (K), depositar no baú aberto, construir puxando dos baús | ✅ testado |
| Busca: mouse, receita, caixa de busca (J) | ✅ testado |
| Raio-x: painel (N) | ✅ testado |
| Pilhas fase 1: mochila e baús | ✅ testado (inclui salvar/carregar) |
| Atalhos no canto · Raio-x detalhado (Ctrl+N) | 🧪 a testar |
| Pilhas fases 2 a 4: máquinas, drones, foguetes | 🧪 a testar |
| Etiquetar automático · buffer · copiar logística | 🧪 a testar |
| Rotas dos drones · produção por minuto · assistente de terraformação · timelapse | 🧪 a testar |

## Rodadas até a 1.0 (A a G implementadas; falta testar)

| Rodada | Conteúdo |
|---|---|
| A | Pilhas fase 2: máquinas (extratores, geradores, autocrafters, cultivadores, biodomos, colmeias, desintegrador, otimizador) |
| B | Pilhas fase 3: drones e logística respeitando pilhas · Pilhas fase 4: foguetes de troca, depósitos interplanetários, veículos |
| C | Armazém: etiquetar baú automaticamente · baú buffer · copiar configuração de logística entre baús |
| D | Raio-x: rotas dos drones · produção por minuto de cada item |
| E | Assistente de terraformação (aba do Ctrl+N antes da aba de drones) |
| F | Timelapse da terraformação |
| G | Progressão (desbloqueio das funções ao longo do jogo) — implementada na 0.10.0 |
| — | Beta pública no Nexus quando A e B estiverem testadas |
| 1.0 | Teste em multiplayer e em base grande, compatibilidade com mods populares |

Pendente para a 1.0: teste em multiplayer (todos com o mod),
teste de desempenho em base grande, compatibilidade (Containers Plus, Storage Display,
Auto-Logistics, Storage Buffer), compilação pelo Visual Studio no Windows.

## Progressão (rodada G, confirmada)

As funções são tecnologia Warden "redescoberta" aos poucos. Uma opção na configuração
desliga a progressão e libera tudo desde o início.

| Fase do jogo | Libera |
|---|---|
| Início | Guardar (K), depositar, busca (J), assistente de terraformação, timelapse |
| Primeiros baús construídos | Construir puxando dos baús |
| Progressão de pilhas | Tamanho da pilha cresce com a terraformação (ex.: 25 → 100 → 250) |
| Estação de drones desbloqueada | Raio-x, rotas, produção por minuto, baú buffer, copiar logística |

## Depois da 1.0

Ideias que cabem no escopo: ver oferta/pedido ao mirar num baú, abrir baús à
distância, conteúdo visível por fora do baú.

Próximo mod da série: suíte de qualidade de vida (jetpack, mineração automática,
lanterna e afins) com uma configuração só.
