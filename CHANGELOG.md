# Histórico de versões

## 0.10.0
- **Progressão (nova, ligada por padrão):** a tecnologia Warden é redescoberta ao longo do jogo.
  - Desde o início: guardar (K), buscar (J), assistente de terraformação, timelapse.
  - Primeiro baú construído: construir puxando dos baús.
  - Pilhas crescem com as etapas de terraformação: 10% → 40% → 100% do `StackSize` (25 → 100 → 250 no padrão). Nunca diminuem; um save que já tinha pilhas grandes mantém o tamanho cheio.
  - Estação de drones liberada: Raio-x (N), rotas, abas Produção e Drones, baú buffer, copiar logística.
  - Aviso na tela quando algo é liberado; apertar uma tecla ainda bloqueada diz quando ela libera. Opção `Progression = false` libera tudo.

## 0.9.0
- **Central Warden (Ctrl+N):** a janela detalhada ganhou abas, na ordem em que o jogo libera cada assunto: Terraformação, Produção e Drones (o Raio-x de antes).
- **Assistente de terraformação (aba nova):** para oxigênio, calor, pressão, biomassa, plantas, insetos, animais e índice de terraformação mostra valor, ritmo por segundo, próximo desbloqueio (item ou etapa), em quanto tempo chega no ritmo atual e a melhor construção já liberada para acelerar. No topo, uma sugestão: o desbloqueio mais próximo e o que construir, ou a medida que está parada. Funciona também como convidado.
- **Produção por minuto (aba nova):** itens criados pelas máquinas por minuto e por hora (média dos últimos 5 minutos), com o estoque nos baús.
- **Rotas dos drones:** com o Raio-x (N) ligado, cada entrega aparece como uma linha com seta, do fornecedor ao baú que pede: azul indo buscar, verde levando, cinza esperando drone. Opção `ShowDroneRoutes`.
- **Etiquetar automaticamente:** ao fechar um baú sem nome que tem um só tipo de item, ele recebe o nome desse item. Opção `AutoName`.
- **Baú buffer (B no baú):** marca o baú com "[Buffer]"; ele pede os itens configurados e também os oferece a quem pede. Buffer não abastece buffer. Use prioridade baixa no buffer.
- **Copiar e colar logística (Ctrl+C / Ctrl+V no baú):** copia pedidos, ofertas e prioridade de um baú para outro.
- **Timelapse (Ctrl+T):** grava o ponto de vista atual; a cada 5 minutos de jogo uma câmera invisível fotografa esse ponto e salva um PNG em `Timelapse\<save>\<planeta>\` na pasta do jogo.

## 0.8.0
- **Pilhas fase 2 (máquinas):** extratores, geradores, colmeias e fazendas, autocrafters, conversores, desintegrador (entrada e saída), lixeira e otimizador passam a empilhar. O desintegrador confere o espaço livre da saída por pilhas. Não empilham: cultivadores (o espaço é da semente), colheitas que aparecem no mundo (cada fruto é um objeto físico), estações de drones e equipamento. Opções `StackMachines` e `StackOptimizer` (o otimizador conta cada fusível, então pilhas multiplicam o efeito).
- **Pilhas fase 3 (drones):** baús de pedido continuam recebendo entregas depois de passar do número de espaços; a conta de "quantas entregas cabem" considera as pilhas e os itens pedidos. Drone que precisa redirecionar um item procura baús com pilha aberta desse item antes de descartá-lo.
- **Pilhas fase 4 (foguetes):** foguetes de troca e de troca interplanetária empilham; o carrinho de compra aceita itens enquanto couberem nas pilhas, e o contador mostra os espaços ocupados. Depósitos interplanetários e baús de veículos já empilham como baús. Opção `StackRockets`.

## 0.7.0
- **Raio-x detalhado (Ctrl+N):** janela que se comporta como as do jogo (para o personagem, mostra o cursor, fecha com Esc), com visão geral explicada (drones, estações, entregas, energia), todos os problemas com "por quê", "como resolver" e os baús envolvidos (nome e distância), e uma tabela por item (pedem, oferecem, estoque, em entrega, situação). Rolagem pela roda do mouse; atualiza a cada 3 segundos.

## 0.6.3
- **Atalhos sempre visíveis:** uma linha discreta logo acima da versão do jogo, no canto inferior direito, com a mesma fonte; o conteúdo muda conforme a janela aberta. Substitui a barra de atalhos e o lembrete ao carregar.
- **Raio-x:** o fundo do painel volta a ser escuro (a borda passou a ser desenhada como quatro linhas finas, sem vazar branco pelo fundo translúcido).

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
