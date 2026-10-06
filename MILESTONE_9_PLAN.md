# Sub-Plan: Milestone 9 — Installer, Winget Integration & Auto-Update

> **Status:** Concluído (Done)  
> **Versão Alvo:** 1.0.11 (x64 Native)  
> **Objetivo:** Fornecer um instalador profissional para Windows (`FirezipSetup-x64.exe`), distribuição oficial via **Winget** (`winget install Firezip`), distribuição portátil e mecanismo de verificação de atualizações no GitHub Releases.

---

## 1. Visão Geral da Arquitetura de Distribuição

```
                        ┌─────────────────────────────────────┐
                        │        Compilação Release           │
                        │    (Firezip.UI.exe + Dependências)  │
                        └──────────────────┬──────────────────┘
                                           │
                 ┌─────────────────────────┴─────────────────────────┐
                 │                                                   │
                 ▼                                                   ▼
      ┌─────────────────────┐                             ┌─────────────────────┐
      │  Standalone/Portable│                             │  Inno Setup Build   │
      │   Firezip.UI.exe    │                             │ FirezipSetup-x64.exe│
      └──────────┬──────────┘                             └──────────┬──────────┘
                 │                                                   │
                 │                ┌────────────────────────┐         │
                 │                │    Manifestos Winget   │◄────────┘
                 │                │ (YAML - Schema 1.9.0)  │
                 │                └───────────┬────────────┘
                 │                            │
                 ▼                            ▼
      ┌─────────────────────┐     ┌────────────────────────┐
      │   GitHub Releases   │◄────┤  winget-pkgs PR /      │
      │  (Assets + Hashes)  │     │  winget install local  │
      └─────────────────────┘     └────────────────────────┘
```

---

## 2. Componentes e Entregas

### A. Instalador Nativo Windows (`Inno Setup`)
- **Arquivo de Configuração:** `packaging/installer/firezip_setup.iss`
- **Características:**
  - Instalação no diretório padrão: `{autopf}\Firezip` (Program Files).
  - Criação de atalhos no Menu Iniciar e na Área de Trabalho (com opção desmarcada por padrão ou opcional).
  - Registro limpo no Painel de Controle / Configurações do Windows (*Adicionar ou Remover Programas* / `UninstallString`).
  - Registro de associações de arquivos (`.zip`, `.7z`, `.rar`, `.tar`, `.gz`, `.bz2`) com ícone do aplicativo.
  - Registro no menu de contexto do Windows Explorer com ícone nativo (propriedade Icon) e rótulos limpos sem "(Firezip)", disparando janelas de tarefa dedicadas e leves (`TaskProgressWindow`/`TaskOptionsWindow`) sem abrir a UI completa (`MainWindow`).
  - **Suporte obrigatório a flags silenciosas:** `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-` (requisito mandatório do Winget).
  - Desinstalador completo e limpo (`unins000.exe`), removendo todos os arquivos e chaves de registro.

### B. Distribuição via Windows Package Manager (`winget`)
- **Identificador do Pacote:** `Firezip.Firezip`
- **Estrutura de Pastas de Manifestos:**
  `packaging/winget/manifests/f/Firezip/Firezip/<version>/`
- **Arquivos de Manifesto (Schema v1.9.0):**
  1. `Firezip.Firezip.yaml`: Manifesto de versão geral.
  2. `Firezip.Firezip.installer.yaml`:
     - `Architecture: x64`
     - `InstallerType: inno`
     - `InstallerUrl: https://github.com/drodrigues55/firezip/releases/download/v1.0.11/FirezipSetup-x64-v1.0.11.exe`
     - `InstallerSha256: 08BB797B88B4CC1614C7B32241E132AA3F0ABFCAA2B1D848B0257881E25D247F`
     - `InstallerSwitches: /VERYSILENT /NORESTART /SUPPRESSMSGBOXES`
     - `UpgradeBehavior: install`
  3. `Firezip.Firezip.locale.en-US.yaml`:
     - Metadados, Publisher, Descrição, Licença (MIT), Tags (`zip`, `7z`, `rar`, `archive`, `compressor`), Release Notes.
- **Ferramentas e Scripts de Automação:**
  - `packaging/generate_winget_manifest.ps1`: Script PowerShell que calcula o hash SHA256 do instalador e gera/atualiza os 3 manifestos YAML automaticamente.
  - Validação via `winget validate --manifest ...` e teste local com `winget install --manifest ...`.

### C. Mecanismo de Atualização Automática / Check de Versão
- **Localização:** `src/Firezip.Infrastructure/Update/UpdateChecker.cs`
- **Fluxo:**
  - Consulta assíncrona ao endpoint da API do GitHub: `https://api.github.com/repos/drodrigues55/firezip/releases/latest`.
  - Comparação semântica (`SemVer`) entre a versão local (`1.0.11`) e a versão da release remota.
  - Se houver versão mais recente: exibe link direto para o instalador e release notes.
  - Totalmente seguro para uso offline (timeout rápido de 3 segundos, silêncio se não houver rede, nunca trava a inicialização da UI).
  - Respeita configuração do usuário (pode ser ativado/desativado em `SettingsService`).

---

## 3. Checklist de Execução Passo a Passo

- [x] **Passo 1:** Criar o script Inno Setup `packaging/installer/firezip_setup.iss` com todas as chaves de registro, associações de extensão e atalhos.
- [x] **Passo 2:** Criar o script de automação de build `packaging/build_installer.ps1` que compila o Release do app, chama o `ISCC.exe` (Inno Setup Compiler) e gera o binário `dist/FirezipSetup-x64-v1.0.11.exe`.
- [x] **Passo 3:** Garantir que o Inno Setup Compiler (`ISCC`) esteja disponível no ambiente e compilar o instalador.
- [x] **Passo 4:** Criar o script gerador de manifestos Winget `packaging/generate_winget_manifest.ps1` e gerar os manifestos oficiais na pasta `packaging/winget/manifests/f/Firezip/Firezip/1.0.11/`.
- [x] **Passo 5:** Validar os manifestos gerados usando o utilitário nativo `winget validate`.
- [x] **Passo 6:** Atualizar `UpdateChecker.cs` para suportar checagem contra releases do GitHub de forma segura e offline-resiliente.
- [x] **Passo 7:** Executar bateria completa de testes (`dotnet test` com 86/86 testes aprovados).

---

## 4. Guia para Retomada por Outro Modelo de IA

Se a quota de contexto for interrompida e outra sessão ou modelo de IA assumir:
1. Leia este documento (`MILESTONE_9_PLAN.md`) e o `IMPLEMENTATION_PLAN.md` na raiz do projeto.
2. Verifique o checklist acima na Seção 3 para ver quais passos já estão marcados com `[x]`.
3. Para compilar o instalador: execute `powershell -ExecutionPolicy Bypass -File packaging/build_installer.ps1`.
4. Para validar manifestos Winget: execute `winget validate --manifest packaging/winget/manifests/f/Firezip/Firezip/<version>/`.
5. Execute `dotnet test` para assegurar que os 84+ testes continuam passando.
