# Histórico de versões

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
