# Compatibilidade e releases

Meta solicitada: CorelDRAW versão interna **20 (2018) em diante**, **Windows x64**. O setup detecta automaticamente as instalações 64-bit e contém um só plugin, com tipos COM usados pelo YUNO incorporados durante a compilação. O computador do comprador não precisa fornecer `Corel.Interop.VGCore.dll` nem buscar um pacote individual.

O instalador identifica instalações pelas inscrições COM e pelas pastas usuais, inclusive instalações em caminhos personalizados. A tela permite escolher o Corel. O build recusa empacotar o plugin se ele mantiver uma dependência em tempo de execução de `Corel.Interop.VGCore.dll`. A incorporação remove essa dependência de versão; cada versão principal ainda precisa passar por teste funcional.

| Corel | Versão interna | Estado |
|---|---|---|
| 2018 | 20 | Detectado; teste funcional no host pendente |
| 2019 | 21 | Detectado; teste funcional no host pendente |
| 2020 | 22 | Detectado; teste funcional no host pendente |
| 2021 | 23 | Referência de compilação e teste no host pendentes |
| 2022 | 24.0–24.2 | Referência de compilação e teste no host pendentes |
| 2023 | 24.3+ | Referência de compilação e teste no host pendentes |
| 2024 | 25 | Referência de compilação e teste no host pendentes |
| 2025 | 26 | Referência de compilação e teste no host pendentes |
| 2026 | 27 | Compilação com 27.1.0.129; teste no host pendente |

O SDK de uma instalação legítima só é necessário na máquina de build. Não redistribuir a DLL interop do Corel junto do YUNO. Cada versão principal ainda deve passar pelo teste COM antes de ser anunciada como suportada. Versões futuras não recebem suporte automaticamente.

## Pedidos nesta versão

O servidor web e o Pedido Nuvem web estão desativados nesta versão para manter o complemento mais leve e estável. Os pedidos ficam na Nuvem Local, em arquivos separados na pasta configurável, e podem usar uma pasta compartilhada da rede. O fluxo disponível é importar uma planilha Excel/CSV, revisar as linhas, salvar o pedido localmente e montar.

## Compilar

Em PowerShell, forneça as pastas Programs64 de cada instalação disponível:

```powershell
.\Yuno\build.ps1 -CorelDirs @('C:\Program Files\Corel\CorelDRAW Graphics Suite\27\Programs64')
```

O staging é novo a cada compilação para evitar misturar arquivos antigos. O resultado de desenvolvimento fica em `Yuno/release/development`, acompanhado de SHA-256 e `release.json`. O manifesto registra o SDK de build e que a validação funcional em versões anteriores ainda está pendente.

## Assinatura e antivírus

O setup anterior e o plugin local estavam sem assinatura. Não foi possível consultar o histórico de detecções neste ambiente; a causa da remoção do setup ainda não foi confirmada. Obter o nome da ameaça, produto antivírus e hash do arquivo afetado antes de concluir que é falso positivo.

Para gerar uma release assinada, instalar um certificado de assinatura de código confiável com chave privada no repositório de certificados do Windows e executar:

```powershell
.\Yuno\build.ps1 -CorelDirs @('CAMINHO\Programs64') -CertificateThumbprint 'THUMBPRINT' -ForDistribution
```

O processo assina `Yuno.dll` e `YunoRes.dll` antes de embuti-las no instalador, assina o setup e exige assinatura válida com carimbo de tempo. O hash é calculado depois da assinatura. Sem certificado, o modo de distribuição falha. Não há certificado disponível no ambiente atual; o caminho de assinatura ainda precisa de teste com um certificado real. Para HSM ou serviço de assinatura remoto, adaptar `Sign-Artifact` ao fornecedor.

A assinatura identifica o editor e a integridade; não garante reputação imediata no SmartScreen nem ausência de detecções. Se a análise confirmar um falso positivo, submeter o arquivo à Microsoft como desenvolvedor. Não desativar o antivírus, criar exclusões ou ocultar o executável para distribuir.

- Microsoft: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation
- Análise de arquivos: https://www.microsoft.com/en-us/wdsi/filesubmission
- APIs por versão do Corel: https://community.coreldraw.com/sdk/api/draw/22

## Validação antes de publicar

1. Em cada Corel alvo, compilar `Yuno/tests/Yuno.Tests` com `-p:CorelDir=...` e executar com a pasta de saída e o ProgID do host como argumentos, por exemplo `Yuno.Tests.exe C:\YunoTest CorelDRAW.Application.22`.
2. Testar instalação, barra/painéis, definição/aplicação de matrizes, montagem, encaixe, exportação e desinstalação em cada atualização declarada.
3. Validar a release assinada, verificar o SHA-256 e analisar a detecção do antivírus. Só então publicar.

Os testes COM anteriores foram bloqueados por `E_ACCESSDENIED` neste ambiente. Compilar com sucesso não equivale a validar o funcionamento dentro do Corel.

## Serial temporário do instalador

A instalação interativa exige a chave no campo protegido. O verificador usa PBKDF2-HMAC-SHA256, salt aleatório e 210.000 iterações; o serial original não fica no repositório nem no executável. A validação ocorre também em `Installer.Instalar`, antes de qualquer escrita. A desinstalação continua livre de chave.

No modo silencioso, fornecer a variável de ambiente do processo `YUNO_INSTALL_SERIAL` (não um argumento da linha de comando). Chave ausente/incorreta resulta em código de saída 1, sem instalar. Não persistir essa variável no ambiente do Windows. Para os testes positivos, fornecer `YUNO_TEST_SERIAL` apenas ao processo de teste.

Esta é uma barreira temporária de instalação, não DRM: não limita máquinas, não revoga acessos, não impede cópia das DLLs instaladas ou extração do payload e não protege versões antigas sem serial. Como o código-fonte está disponível, alguém pode remover a validação e recompilar. Controle de uso no plugin, contas por e-mail e ativação/revogação por servidor são trabalho futuro. Não anunciar esta versão como proteção contra pirataria.

O serial privado é entregue separadamente ao proprietário; não deve acompanhar a release pública. Trocar o serial exige gerar outro verificador e outro instalador. Não reutilizar a chave temporária como senha de conta.
