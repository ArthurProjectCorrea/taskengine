# Instalar o TaskEngine (V1)

Pacote assinado com certificado autoassinado (`CN=Arthur de Paula Correa`) — o Windows só confia nele depois de importado uma vez.

1. Abrir PowerShell **como Administrador** e importar o certificado público:
   ```powershell
   Import-Certificate -FilePath ".\TaskEngine.Desktop.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```
2. Instalar o pacote (inclui a dependência do Windows App Runtime, caso ainda não esteja instalada nesta máquina):
   ```powershell
   Add-AppxPackage -Path ".\TaskEngine.Desktop_1.0.0.1_x64.msix" -DependencyPath ".\Microsoft.WindowsAppRuntime.1.7.msix"
   ```
   Alternativa: clicar duas vezes em `TaskEngine.Desktop_1.0.0.1_x64.msix` depois do passo 1 (o instalador gráfico do Windows resolve a dependência automaticamente se ela já estiver disponível/baixável).

Versão: `1.0.0.1` (x64). Gerado a partir da branch `28-v1-fluxo-manual-completo-tarefa-provedor-tracking-humano`.
