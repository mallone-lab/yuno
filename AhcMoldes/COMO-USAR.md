# AHC Moldes — como usar

## Instalar
1. Feche o CorelDRAW.
2. Botão direito em `instalar.ps1` → **Executar com o PowerShell** → aceite o pedido de administrador.
3. Abra o CorelDRAW → **Janela > Dockers > AHC Moldes**.
   - Se não aparecer: segure **F8** ao abrir o Corel e confirme "restaurar área de trabalho padrão".

Para remover: `desinstalar.ps1`.

## Preparar o arquivo do modelo (uma vez por modelo)
1. Desenhe/importe os moldes de todos os tamanhos (curvas fechadas) + uma cópia para ser a **matriz** de cada parte.
2. Aba **Moldes**:
   - Parte nº `1`, apelido `FRENTE` → selecione a matriz → **Definir MATRIZ**.
   - Selecione o molde do P → tamanho `P` → **Definir tamanho** (o tamanho avança sozinho para o próximo).
   - Ou, mais rápido: selecione os moldes da parte em todos os tamanhos, abra **Automático por área**, marque os tamanhos (do menor ao maior) e clique **Definir por área**.
   - Repita para cada parte (2 = COSTAS, 3 = MANGA...). **Ver definições** confere tudo.
3. Coloque a arte **dentro da matriz** (PowerClip): fundos, faixas etc.
   - Logos/escudos que não devem crescer com o tamanho: selecione e **Marcar FIXO**.
4. Campos: selecione a matriz → **Inserir campos padrão** (ou crie textos seus e use **Campo NOME / Nº / ADIC.**).
   Ajuste fonte, cor e posição como quiser.
5. Opcional: desenhe retângulos onde o nome/número devem caber e marque como **Gabarito**. Um **Gabarito ID** define onde o ID é impresso.

## Montar um pedido
1. Aba **Montar** → digite o nº do **Pedido**.
2. No Excel, copie as linhas e clique **Colar** (ou **Planilha...** para abrir .xlsx/.csv).
   Colunas padrão: **A** = nome, **B** = tamanho, **C** = quantidade, **D** = número, **E** = adicional (mude em **Config**).
3. Confira a grade (dá para editar) → **MONTAR**.
   As peças aparecem à direita do desenho, uma linha por uniforme, com indicador em cima.

## Depois de montar
- **Encaixe**: importe o encaixe do seu software de nesting, selecione as formas-base + as peças montadas → **ENCAIXAR SELEÇÃO**.
- **Exportar**: selecione → escolha formato/tecido/pasta → **EXPORTAR SELEÇÃO**.
- **Ferramentas**: transferir arte, cópias, espelhar/girar/empilhar, texto em curvas, contorno, substituir cor, limpar, relatório.

Tudo vira **um único Ctrl+Z** por operação.
