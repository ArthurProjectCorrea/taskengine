# Instalar o TaskEngine (V1)

1. Baixar/copiar `TaskEngineSetup.exe`.
2. Executar `TaskEngineSetup.exe` e clicar em "Avançar" até o fim (não exige privilégios de administrador — instala só para o usuário atual, sem UAC).

Na primeira execução o Windows SmartScreen pode mostrar o aviso "Windows protegeu seu PC", porque o instalador não tem certificado de uma autoridade paga. Isso é esperado: clicar em **"Mais informações"** e depois em **"Executar assim mesmo"**. Não é necessário PowerShell nem importar nenhum certificado.

O instalador cria um atalho no Menu Iniciar (e, opcionalmente, na Área de Trabalho) e registra o desinstalador em "Aplicativos e Recursos". O app é self-contained (inclui o .NET e o Windows App Runtime), então não depende de nada pré-instalado na máquina.

Versão: `1.0.0.1` (x64). Gerado a partir da branch `28-v1-fluxo-manual-completo-tarefa-provedor-tracking-humano`.
