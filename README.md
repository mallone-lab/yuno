# YUNO

Add-on para **CorelDRAW 2026** que monta moldes graduados para uniformes sublimados.

- **Definir tamanhos:** clique nas partes do molde e elas viram `M 1`, `M 2`… e `MATRIZ 1`, `MATRIZ 2`…
- **Aplicar matrizes:** copia a arte de cada matriz para todos os tamanhos, ajustando a graduação.
- **Montar pedido:** lê a planilha ou o pedido online e cria as peças **embaixo do molde de cada tamanho**, com nome e número. Os textos `NOME` e `10` são reconhecidos sozinhos, acompanham o tamanho e diminuem para caber na peça.
- **Ferramentas, Encaixe sobre outro, Exportar (JPG/PDF/TIF/PNG) e Configurações.**
- **Pedidos online:** formulário web onde o cliente preenche o pedido (`Yuno/web`), com banco no Supabase.

## Instalar
Baixe o **YunoSetup.exe** (aba *Releases*), dê dois cliques e clique em **Instalar**. Ele encontra o CorelDRAW 2026, instala, ativa a barra **YUNO** no topo e aparece em *Configurações > Aplicativos* para desinstalar.

Para gerar o instalador: `powershell -ExecutionPolicy Bypass -File Yuno/build.ps1` → `Yuno/release/YunoSetup.exe`.

Marca: [Yuno/brand/MARCA.md](Yuno/brand/MARCA.md)

Guia de uso: [Yuno/COMO-USAR.md](Yuno/COMO-USAR.md) · Pedidos online: [Yuno/web/CONFIGURAR-PEDIDOS-ONLINE.md](Yuno/web/CONFIGURAR-PEDIDOS-ONLINE.md)

## Compilar
Requisitos: .NET SDK e CorelDRAW 2026 instalado (usa `Corel.Interop.VGCore.dll` da pasta do Corel).

```
cd Yuno/src/Yuno
dotnet build -c Release
```

Os ícones e os arquivos da barra (`Addon/AppUI.xslt`, `UserUI.xslt`, `config.xml`, `YunoRes.dll`) são gerados por `Yuno/tools/gerar_barra.py` (requer Pillow).

## Estrutura
```
Yuno/
  src/Yuno/Engine   lógica (moldes, montagem, encaixe, exportação, pedidos online)
  src/Yuno/UI       painéis WPF (tema escuro) e janela de pedidos online
  src/Yuno/Addon    registro no Corel (menu, barra, ícones)
  dist/                  arquivos prontos para instalar
  web/                   formulário de pedido + SQL do Supabase
  tests/                 teste de ponta a ponta via COM
  tools/                 gerador da barra de ícones
```
