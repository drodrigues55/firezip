# Plano de Implementação: Adequação de Estrutura, Higienização de Tamanho e Publicação no GitHub Releases

> **Versão Alvo:** 1.0.0 (General Availability)  
> **Status:** Pronto para Execução 📋  
> **Objetivo:** Adequar a estrutura de pastas do projeto, isolar arquivos binários pesados (> 100 MB) para não violar as restrições de push do GitHub, higienizar o repositório Git e automatizar a geração do kit oficial de release para publicação no GitHub Releases e Winget.

---

## 1. Diagnóstico e Auditoria Atual

### A. Violações de Limite de Arquivo do GitHub (Hard Limit de 100 MB)
O GitHub rejeita commits que contenham arquivos individuais superiores a **100 MB** (`remote: error: GH001: Large files detected`). Atualmente, os seguintes arquivos estão desprotegidos na raiz ou em pastas não ignoradas:

| Arquivo / Localização | Tamanho Atual | Situação no Git | Risco de Rejeição pelo GitHub |
|---|---|---|---|
| `Firezip.UI.exe` (na raiz) | **129,40 MB** | Untracked (`??`) | ❌ **BLOQUEIA O PUSH** |
| `MyUnzip.exe` (na raiz) | **129,40 MB** | Untracked (`??`) | ❌ **BLOQUEIA O PUSH** |
| `dist\FirezipSetup-x64-v1.0.0.exe` | **162,36 MB** | Untracked (`??`) | ❌ **BLOQUEIA O PUSH** |
| `dist\FirezipSetup-x64-v1.0.11.exe` | **82,28 MB** | Untracked (`??`) | ⚠️ Próximo do limite |
| `src\Firezip.Launcher\bin\...` | 170,47 MB | Ignorado por `.gitignore` | ✅ Seguro (já ignorado) |
| `.packages\...` (NuGet cache) | 161,80 MB | Ignorado por `.gitignore` | ✅ Seguro (já ignorado) |

### B. Princípio Fundamental de Arquitetura de Repositório
* **Git Repository (Código):** Deve conter **apenas** código-fonte, configurações, scripts, manifestos e documentação (~5 MB a 10 MB total).
* **GitHub Releases (Binários):** Local oficial para onde os executáveis pesados (`.exe`, `.zip`) devem ser enviados como **Release Assets** anexados (o GitHub permite arquivos de até **2 GB** por anexo de release).

---

## 2. Etapas de Execução

```
 ┌────────────────────────────────────────────────────────────────────────┐
 │                      FLUXO DE ADEQUAÇÃO E RELEASE                      │
 └───────────────────────────────────┬────────────────────────────────────┘
                                     │
         ┌───────────────────────────┼───────────────────────────┐
         ▼                           ▼                           ▼
┌──────────────────┐        ┌──────────────────┐        ┌──────────────────┐
│   FASE 1:        │        │   FASE 2:        │        │   FASE 3:        │
│ Higienização do  │        │ Kit de Release   │        │ Publicação       │
│ Repositório Git  │        │ Automatizado     │        │ Oficial          │
│                  │        │                  │        │                  │
│ • Ignorar *.exe  │        │ • Script Único   │        │ • Commit & Push  │
│   na raiz        │        │ • Setup 1.0.0    │        │ • GitHub Release │
│ • Ignorar dist/  │        │ • Portable .zip  │        │   com Anexos     │
│ • Limpeza temp   │        │ • Hashes SHA-256 │        │ • Submissão      │
│ • Git < 10 MB    │        │ • Manifest RSA   │        │   Winget 1.0.0   │
└──────────────────┘        └──────────────────┘        └──────────────────┘
```

---

## FASE 1: Higienização e Adequação do Git

### 1.1 Atualização das Regras do `.gitignore`
Adicionar regras explícitas para garantir que nenhum executável compilado gerado na raiz ou na pasta de distribuição entre no histórico de commits:
```gitignore
# Root build executables & aliases
/*.exe
dist/
*.temp.config
```

### 1.2 Limpeza de Arquivos Temporários
* Remover `.nuget.temp.config` (redundante com `NuGet/NuGet.Config`).
* Manter os `.exe` na raiz e em `dist/` apenas como artefatos locais para uso/testes, porém 100% invisíveis ao `git add .`.

---

## FASE 2: Geração do Kit Oficial de Release (Release Assets)

Criar o script unificado `packaging/prepare_github_release.ps1` que prepara e organiza todos os artefatos em `dist/release/`:

1. **Instalador Oficial:**
   * `FirezipSetup-x64-v1.0.0.exe` (~162 MB)
   * `FirezipSetup-x64-v1.0.0.exe.sha256`
2. **Pacote Portátil Oficial (Standalone):**
   * Compactar `Firezip.UI.exe`, `FirezipUpdater.exe` e `THIRD-PARTY-NOTICES.md` em `Firezip-v1.0.0-windows-x64-portable.zip` (~40-50 MB comprimido).
   * `Firezip-v1.0.0-windows-x64-portable.zip.sha256`
3. **Manifesto do Auto-Updater:**
   * `manifest.json` assinado com RSA-SHA256 para permitir que o updater automático detecte e valide a v1.0.0.
4. **Relatório de Verificação:**
   * Tabela com todos os hashes SHA-256 calculados pronta para colar nas notas de lançamento do GitHub.

---

## FASE 3: Publicação no GitHub e Distribuição Pública

### 3.1 Subir o Código Limpo para o Repositório
No terminal:
```powershell
git status  # Deve mostrar apenas código-fonte, sem arquivos > 100 MB
git add .
git commit -m "feat: Firezip v1.0.0 General Availability release"
git branch -M main
git remote add origin https://github.com/drodrigues55/firezip.git  # se ainda não configurado
git push -u origin main
```

### 3.2 Criar a Release no GitHub com os Binários Anexados
Existem duas opções práticas:

* **Opção A (Via Navegador / Interface Web):**
  1. Acessar `https://github.com/drodrigues55/firezip/releases/new`.
  2. Escolher a tag: `v1.0.0`.
  3. Título: `Firezip v1.0.0 (General Availability)`.
  4. Descrição: Colar as notas da versão e a tabela de hashes SHA-256.
  5. Arrastar os arquivos de `dist/release/`:
     * `FirezipSetup-x64-v1.0.0.exe`
     * `FirezipSetup-x64-v1.0.0.exe.sha256`
     * `Firezip-v1.0.0-windows-x64-portable.zip`
     * `Firezip-v1.0.0-windows-x64-portable.zip.sha256`
     * `manifest.json`
  6. Clicar em **Publish release**.

* **Opção B (Via Terminal com GitHub CLI `gh`):**
  ```powershell
  gh release create v1.0.0 dist\release\* --title "Firezip v1.0.0 (General Availability)" --notes-file packaging\RELEASE_NOTES_v1.0.0.md
  ```

### 3.3 Submissão ao Windows Package Manager (Winget)
Com o instalador hospedado na URL pública oficial do GitHub Releases:
```powershell
wingetcreate submit packaging\winget\manifests\f\Firezip\Firezip\1.0.0
```

---

## 3. Checklist de Validação

- [ ] `.gitignore` bloqueia `*.exe` na raiz e diretório `dist/`
- [ ] `git status` não lista nenhum arquivo acima de 100 MB
- [ ] Tamanho total do commit inicial é inferior a 15 MB
- [ ] `dist/release/` contém o instalador, o pacote portátil, hashes e manifest.json
- [ ] Manifestos do Winget v1.0.0 contêm o hash exato do instalador
- [ ] Todos os 121 testes automatizados continuam passando com 100% de sucesso
