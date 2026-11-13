# YUNO

Add-on que monta moldes graduados para uniformes sublimados. O setup detecta CorelDRAW versão interna **20 (2018) em diante, no Windows x64**, e instala um plugin com tipos COM incorporados, sem exigir a interop do Corel no computador do usuário. A compatibilidade funcional de cada versão ainda precisa ser validada. Consulte [compatibilidade e distribuição](Yuno/DISTRIBUICAO.md).

- **Definir tamanhos:** clique nas partes do molde e elas viram `M 1`, `M 2`… e `MATRIZ 1`, `MATRIZ 2`…
- **Aplicar matrizes:** copia a arte de cada matriz para todos os tamanhos, ajustando a graduação.
- **Pedido local:** importa planilhas Excel/CSV, edita linhas e guarda pedidos na Nuvem Local com os dados da OS e os caminhos dos arquivos associados.
- **Montar pedido:** lê os dados do pedido salvo e cria as peças **embaixo do molde de cada tamanho**, com nome e número. Os textos `NOME` e `10` são reconhecidos sozinhos, acompanham o tamanho e diminuem para caber na peça.
- **Ferramentas, Encaixe sobre outro, Exportar (JPG/PDF/TIF/PNG) e Configurações.**

## Instalar
O **YunoSetup.exe** detecta as instalações CorelDRAW compatíveis e permite escolher onde instalar. Salve os documentos e feche o Corel antes de instalar. A barra **YUNO** é ativada no topo e a desinstalação aparece em *Configurações > Aplicativos*. O build de desenvolvimento é gerado localmente em `Yuno/release/development`.

Para gerar o instalador de desenvolvimento: `powershell -ExecutionPolicy Bypass -File Yuno/build.ps1` → `Yuno/release/development/YunoSetup.exe`. Para distribuição assinada e compilação com várias versões, consulte [DISTRIBUICAO.md](Yuno/DISTRIBUICAO.md).

Marca: [Yuno/brand/MARCA.md](Yuno/brand/MARCA.md)

Guia de uso: [Yuno/COMO-USAR.md](Yuno/COMO-USAR.md)

## Compilar
Requisitos de build: .NET SDK e uma instalação x64 legítima do CorelDRAW para obter a referência COM. O plugin incorpora os tipos usados e não exige essa interop na máquina do usuário.

```
cd Yuno/src/Yuno
dotnet build -c Release
```

Os ícones e os arquivos da barra (`Addon/AppUI.xslt`, `UserUI.xslt`, `config.xml`, `YunoRes.dll`) são gerados por `Yuno/tools/gerar_barra.py` (requer Pillow).

## Estrutura
```
Yuno/
  src/Yuno/Engine   lógica (moldes, montagem, encaixe e exportação)
  src/Yuno/UI       painéis WPF (tema escuro)
  src/Yuno/Addon    registro no Corel (menu, barra, ícones)
  dist/                  arquivos prontos para instalar
  tests/                 teste de ponta a ponta via COM
  tools/                 gerador da barra de ícones
```
