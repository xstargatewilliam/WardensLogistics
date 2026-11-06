# Histórico de versões

## 0.6.2
- **Atalhos na interface:** barra discreta embaixo da tela, no estilo do jogo, com os atalhos que valem para a janela aberta (baú, inventário, construção). Lembrete único com as teclas principais ao carregar o mundo. Opção `ShowKeyHints`.
- **Raio-x:** ícones próprios (drone, estação, entregas, energia, crítico, aviso), no estilo hexagonal do HUD, incluídos em `icons/`.

## 0.6.1
- **Raio-x:** painel redesenhado no estilo do HUD do jogo (caixa escura com borda branca, fonte do jogo), com indicadores por ícone (drones, estações, entregas, energia) e problemas em linhas curtas. Energia só aparece quando há estação de drones. Ícones personalizáveis por PNG na pasta `icons/`.

## 0.6.0
- **Pilhas (novo módulo, fase 1):** itens iguais empilham até 250 por espaço (`StackSize`) na mochila e nos baús. O número da pilha aparece no canto do espaço; clicar numa pilha dentro da janela de baú move a pilha inteira (Shift continua movendo todos do tipo). Características genéticas, sequências de DNA e plantas de projeto só empilham com cópias idênticas.
- **Correção de segurança:** ao carregar o save, o jogo cortava a lista de itens de um inventário em 8000 partes; a leitura agora não tem limite.
- Guardar rápido e depositar passam a calcular o espaço livre por pilha.
- Aviso quando outro mod de empilhamento está instalado.

## 0.5.1
- **Núcleo:** dois raios. `PullRange` (padrão 200 m) para construir e craftar puxando dos baús; guardar (K) usa `StoreRangePercent` desse raio (padrão 50%, ou seja, 100 m). A opção `Range` deixou de existir.

## 0.5.0
- **Raio-x da logística (novo módulo):** a tecla N abre um painel com drones (voando e nas estações), entregas por estado, baús com pedido/oferta e energia, e aponta a causa dos problemas: sem estação de drones, sem energia, item pedido que ninguém oferece, fornecedores vazios, entregas sem drone livre e baús de oferta cheios do que ninguém pede. Os baús envolvidos ganham marcadores coloridos no mundo. Disponível no jogo solo e para o anfitrião.

## 0.4.1
- **Busca:** a lista de sugestões aparece logo abaixo da caixa de texto do jogo (antes cobria a caixa), em qualquer resolução.

## 0.4.0
- **Busca:** caixa de busca. Sem janela aberta, a tecla J abre um campo para digitar o nome do item, com lista de sugestões (ignora maiúsculas e acentos) e quantidade guardada nos baús. Enter procura o primeiro, clique escolhe, Esc cancela.

## 0.3.1
- **Núcleo:** raio padrão de 40 m (antes 20 m).
- **Armazém:** quando não há baú no alcance, o aviso mostra a distância do mais próximo.
- **Busca:** no menu de construção ou numa bancada, J sobre a receita procura os ingredientes que faltam na mochila. Marcadores com vários itens por baú.
- **Textos:** modelos de tradução movidos para `lang/templates`; modelos antigos gravados por engano em `lang/` são removidos.

## 0.3.0
- **Busca:** "onde está meu item?". Com uma janela aberta, J sobre um item marca os baús que o têm, com ícone, quantidade e distância, visíveis através das paredes.

## 0.2.1
- **Armazém:** com a janela de um baú aberta, a tecla K deposita nele só os itens que combinam (substitui o Container Auto Deposit, sem a trava global).

## 0.2.0
- Nome definitivo: **Wardens Logistics** (identificador `wardenslogistics.theplanetcrafter`).

## 0.1.3
- Rastreamento detalhado do fluxo de construção no log (com `DebugLog = true`).

## 0.1.2
- **Núcleo:** baús construídos (que usam o componente de rede do jogo) passam a ser reconhecidos.

## 0.1.1
- Textos em português, com suporte a outros idiomas.
- "Puxar dos baús" fica inativo no modo criativo.
- Diagnóstico no log quando nenhum baú é encontrado.

## 0.1.0
- Primeira versão: índice de baús compartilhado, guardar rápido (K) e construir/craftar puxando dos baús.
