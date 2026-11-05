# AHC Moldes

Add-on para **CorelDRAW 2026** que monta moldes graduados para uniformes sublimados.

- **Definir tamanhos:** clique nas partes do molde e elas viram `M 1`, `M 2`… e `MATRIZ 1`, `MATRIZ 2`…
- **Aplicar matrizes:** copia a arte de cada matriz para todos os tamanhos, ajustando a graduação.
- **Montar pedido:** lê a planilha ou o pedido online e cria as peças **embaixo do molde de cada tamanho**, com nome e número. Os textos `NOME` e `10` são reconhecidos sozinhos, acompanham o tamanho e diminuem para caber na peça.
- **Ferramentas, Encaixe sobre outro, Exportar (JPG/PDF/TIF/PNG) e Configurações.**
- **Pedidos online:** formulário web onde o cliente preenche o pedido (`AhcMoldes/web`), com banco no Supabase.

## Instalar
Botão direito em `AhcMoldes/instalar.ps1` → **Executar com o PowerShell**. Depois abra o Corel: aparece a barra **AHC Moldes** no topo e o menu **Janela > AHC Moldes**.

Guia de uso: [AhcMoldes/COMO-USAR.md](AhcMoldes/COMO-USAR.md) · Pedidos online: [AhcMoldes/web/CONFIGURAR-PEDIDOS-ONLINE.md](AhcMoldes/web/CONFIGURAR-PEDIDOS-ONLINE.md)

## Compilar
Requisitos: .NET SDK e CorelDRAW 2026 instalado (usa `Corel.Interop.VGCore.dll` da pasta do Corel).

```
cd AhcMoldes/src/AhcMoldes
dotnet build -c Release
```

Os ícones e os arquivos da barra (`Addon/AppUI.xslt`, `UserUI.xslt`, `config.xml`, `AhcMoldesRes.dll`) são gerados por `AhcMoldes/tools/gerar_barra.py` (requer Pillow).

## Estrutura
```
AhcMoldes/
  src/AhcMoldes/Engine   lógica (moldes, montagem, encaixe, exportação, pedidos online)
  src/AhcMoldes/UI       painéis WPF (tema escuro) e janela de pedidos online
  src/AhcMoldes/Addon    registro no Corel (menu, barra, ícones)
  dist/                  arquivos prontos para instalar
  web/                   formulário de pedido + SQL do Supabase
  tests/                 teste de ponta a ponta via COM
  tools/                 gerador da barra de ícones
```
