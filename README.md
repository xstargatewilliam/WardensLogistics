# Wardens Logistics

**Logística com tecnologia Warden para The Planet Crafter.**
*Warden-grade logistics for The Planet Crafter.*

Os Wardens foram uma civilização avançada. Este mod traz essa tecnologia para a sua base: os baús passam a trabalhar juntos, você guarda e encontra itens com uma tecla e constrói usando o que está guardado, sem ficar indo e voltando.

> 🇬🇧 English summary at the end.

## Funções

| Módulo | Função | Tecla |
|---|---|---|
| **Núcleo** | Índice único de baús, compartilhado por todos os módulos. Reconhece baús pelo comportamento, então baús de outros mods entram automaticamente. | — |
| **Armazém** | Guardar rápido: cada item da mochila vai para o baú próximo que já tem o mesmo item ou cujo nome combina. | K |
| **Armazém** | Depositar no baú aberto: com a janela de um baú aberta, guarda nele só o que combina. | K |
| **Armazém** | Construir e craftar puxando ingredientes dos baús próximos. | automático |
| **Busca** | Onde está meu item: com uma janela aberta, J sobre um item marca os baús que o têm. No menu de construção, J sobre a receita procura os ingredientes que faltam. | J |
| **Busca** | Caixa de busca: sem janela aberta, J abre um campo para digitar o nome do item. | J |
| **Pilhas** | Itens iguais empilham até 250 por espaço na mochila, nos baús, nas máquinas e nos foguetes de troca; drones respeitam as pilhas. Número da pilha no canto; clicar move a pilha inteira. | automático |
| **Armazém** | Etiquetar automaticamente: baú sem nome com um só tipo de item recebe o nome do item ao fechar. | automático |
| **Armazém** | Baú buffer: pede e oferece os mesmos itens (estoque intermediário para os drones). | B no baú |
| **Armazém** | Copiar e colar a configuração de logística entre baús. | Ctrl+C / Ctrl+V no baú |
| **Raio-x da logística** | Painel com o estado dos drones e das entregas, que aponta a causa dos gargalos (sem estação, sem energia, pedido sem fornecedor, fornecedor vazio, baú de oferta encalhado) e marca os baús envolvidos. Rotas das entregas desenhadas no mundo. Solo ou anfitrião. | N |
| **Central Warden** | Janela com abas: Terraformação (valores, ritmo, próximo desbloqueio, tempo estimado, o que construir), Produção (itens por minuto das máquinas) e Drones (problemas com explicação e solução, tabela por item). | Ctrl+N |
| **Timelapse** | Grava um ponto de vista e fotografa a evolução do planeta a cada 5 min de jogo, em `Timelapse\` na pasta do jogo. Para virar vídeo: `ffmpeg -framerate 24 -pattern_type glob -i "*.png" timelapse.mp4` (ou qualquer editor). | Ctrl+T |

Diferenças em relação aos mods que inspiraram o projeto:
- usa as transferências oficiais do jogo, que sincronizam em multiplayer;
- não varre o mapa inteiro a cada checagem de ingredientes (índice em cache);
- uma configuração só, sem atalhos em conflito;
- textos em vários idiomas.

## Progressão

Por padrão as funções liberam conforme o jogo avança (opção `Progression`, em `1. General`; `false` libera tudo):

| Quando | Libera |
|---|---|
| Início | Guardar (K), buscar (J), assistente de terraformação, timelapse |
| Primeiro baú construído | Construir puxando dos baús |
| Etapas de terraformação | Pilhas crescem: 10% → 40% → 100% do `StackSize` (25 → 100 → 250) |
| Estação de drones liberada | Raio-x, rotas, produção, baú buffer, copiar logística |

## Instalação

1. Instale a [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (versão `win_x64`) na pasta do jogo.
2. Copie `WardensLogistics.dll` para `BepInEx\plugins\WardensLogistics\`.
3. Abra o jogo uma vez. A configuração é criada em `BepInEx\config\wardenslogistics.theplanetcrafter.cfg`.

## Configuração

Principais opções (todas com descrição em português e inglês no arquivo):

- `Language`: `auto` segue o idioma do jogo; ou `portuguese`, `english`, `spanish`...
- `PullRange`: raio dos baús usados para construir e craftar, em metros (padrão 200).
- `StoreRangePercent`: raio para guardar com K, em % do `PullRange` (padrão 50 = 100 m).
- `IncludeWorldContainers`: usar também as caixas que vêm com o mapa (padrão: não).
- `Key` (Quick Store / Item Finder): teclas K e J.
- `ExcludedItemIds`: itens que nunca são guardados (ex.: `OxygenCapsule1,WaterBottle1`).

## Antes de desinstalar

Com as pilhas, um baú pode guardar muito mais itens do que o jogo original permite. Se você desinstalar o mod, nada é apagado na hora, mas o jogo original só mostra os primeiros itens de cada inventário e não consegue carregar mais de 8000 itens num mesmo inventário. **Esvazie as pilhas grandes antes de desinstalar.**

## Traduções

Português e inglês vêm embutidos. Para traduzir ou ajustar textos:
1. Copie um modelo de `BepInEx\plugins\WardensLogistics\lang\templates\` para a pasta `lang\`.
2. Renomeie com o identificador do idioma do jogo (`spanish.txt`, `french.txt`, `german.txt`...).
3. Edite as linhas `chave = texto`. Não precisa recompilar.

## Compilar

O repositório não inclui DLLs do jogo nem da BepInEx (não podem ser redistribuídas). Use as da sua instalação.

**Windows (Visual Studio, Rider ou `dotnet`):**
```
dotnet build -c Release -p:GamePath="C:\caminho\para\The Planet Crafter"
```
Ou crie um `GamePath.props` (ignorado pelo git) com `<Project><PropertyGroup><GamePath>...</GamePath></PropertyGroup></Project>`.

**Linux (sem NuGet), compilando contra as bibliotecas do próprio jogo:**
1. Copie para `refs/`: `mscorlib`, `System`, `System.Core`, `netstandard`, `Assembly-CSharp`, `Assembly-CSharp-firstpass`, `UnityEngine`, `UnityEngine.CoreModule`, `UnityEngine.IMGUIModule`, `UnityEngine.TextRenderingModule`, `UnityEngine.UI`, `UnityEngine.UIModule`, `UnityEngine.InputLegacyModule`, `UnityEngine.PhysicsModule`, `Unity.InputSystem`, `Unity.Netcode.Runtime`, `Unity.TextMeshPro` (de `Planet Crafter_Data\Managed`) e `BepInEx`, `0Harmony` (de `BepInEx\core`).
2. Rode `./build.sh`. A DLL sai em `bin/WardensLogistics.dll`.

## Estrutura

```
src/
  Plugin.cs          ponto de entrada e configuração
  Input.cs           todos os atalhos
  Core/              índice de baús, textos/idiomas, acesso ao jogo, rastreamento
  Armazem/           guardar rápido, depositar, puxar dos baús
  Busca/             onde está meu item, caixa de busca, marcadores
  Logistica/         raio-x da logística de drones
  Pilhas/            empilhamento de itens
```

## Créditos

O código deste projeto foi escrito do zero, mas as ideias vêm de mods que a comunidade usa há anos. Obrigado aos autores:

- **aedenthorn**: Craft From Containers, Quick Store, Remote Storage Access, Storage Customization
- **jamarir**: Container Auto Deposit
- **xkursat**: ContainerFinder
- **valriz**: Storage Display, Containers Plus, Storage Buffer
- **rodikh**: Logistics Info
- **Nicki0**: (QoL) Auto-Logistics
- **Tjatja**: Auto Name Storage
- **akarnokd**: CheatInventoryStacking (Apache 2.0). O empilhamento do Wardens Logistics é uma implementação própria, mas o mapa dos pontos do jogo que precisam de atenção veio do estudo desse mod.

The Planet Crafter é um jogo da Miju Games. Este é um projeto de fã, sem vínculo com o estúdio.

## Licença

MIT. Veja [LICENSE](LICENSE).

---

## English summary

**Wardens Logistics** turns your containers into one network. Press **K** to store backpack items into nearby containers that already hold them (or, with a container open, into that container). Build and craft straight from nearby containers. Press **J** over an item or recipe to mark which containers hold it, or with no window open to type an item name. Press **N** for the Logistics X-Ray: drone and delivery status plus the root cause of bottlenecks, with problem containers marked in the world. Multiplayer-safe item transfers, a cached container index, one config file, and translatable texts (`lang/templates`).

Requires BepInEx 5. Install to `BepInEx\plugins\WardensLogistics\`.
