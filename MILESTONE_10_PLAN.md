# Sub-Plan: Milestone 10 — Release Candidate (RC) & Final Quality Verification

> **Status:** Concluído (Complete) ✅  
> **Versão Alvo:** 1.0.11 (Release Candidate)  
> **Objetivo:** Executar a bateria completa de homologação do Release Candidate: testes de compatibilidade multi-formato (TAR, GZ, BZ2, Unicode, hierarquias profundas), testes de regressão de segurança, benchmarks de performance, verificação de clean-install / uninstall silencioso e documentação oficial (README.md).

---

## 1. Matriz de Homologação do Release Candidate

```
                               ┌──────────────────────────────────────────────┐
                               │       Milestone 10 — Release Candidate       │
                               │                (v1.0.11)                     │
                               └──────────────────────┬───────────────────────┘
                                                      │
         ┌───────────────────┬────────────────────────┼───────────────────────┬───────────────────┐
         ▼                   ▼                        ▼                       ▼                   ▼
┌─────────────────┐ ┌─────────────────┐      ┌─────────────────┐     ┌─────────────────┐ ┌─────────────────┐
│  Compatibilidade│ │  Segurança &    │      │  Performance &  │     │  Instalador &   │ │  Documentação & │
│  Multi-Formato  │ │  Hardening      │      │  Benchmarks     │     │  Clean-Uninstall│ │  Metadados      │
│ (TAR, GZ, BZ2,  │ │ (Zip Slip, ADS, │      │ (Taxas MB/s,    │     │ (Inno Setup,    │ │ (README.md,     │
│  Unicode, Deep) │ │  Bombs, Corrupt)│      │  RAM, O(1) Nav) │     │  Winget, Assoc) │ │  Notices, CI)   │
└─────────────────┘ └─────────────────┘      └─────────────────┘     └─────────────────┘ └─────────────────┘
```

---

## 2. Pilares de Testes e Validação

### A. Testes de Compatibilidade Multi-Formato & Corpus
- **Formatos:**
  - `TAR` (`.tar`): Criação, enumeração e extração preservando caminhos relativos. (Aprovado)
  - `GZip / TarGz` (`.tar.gz`, `.tgz`): Extração e empacotamento com streaming. (Aprovado)
  - `BZip2 / TarBz2` (`.tar.bz2`, `.tbz2`): Extração com compressão BZip2. (Aprovado)
  - `7Z` & `ZIP`: Validação cruzada com proteção por senha e algoritmos LZMA/Deflate. (Aprovado)
- **Estruturas de Arquivos:**
  - Nomes com caracteres **Unicode / CJK / Emojis / Acentuação** (`arquivo_teste_ação_日本語_🚀.txt`). (Aprovado)
  - Hierarquias com **mais de 15 níveis de subpastas**. (Aprovado)
  - Arquivos vazios (0 bytes) e diretórios vazios. (Aprovado)
  - Arquivos com espaços e caracteres especiais seguros. (Aprovado)

### B. Bateria Completa de Segurança (Zero Regressões)
- **Zip Slip / Path Traversal:** Rejeição rigorosa de `../`, `..\`, caminhos absolutos (`C:\...`) e UNC (`\\share\...`). (Aprovado)
- **Alternate Data Streams (ADS):** Rejeição de `arquivo.txt:stream`. (Aprovado)
- **Nomes de Dispositivos Reservados DOS:** Bloqueio de `CON`, `PRN`, `AUX`, `NUL`, `COM1-9`, `LPT1-9`. (Aprovado)
- **Bombas de Descompressão (Zip Bombs):** Verificação de streaming em tempo real com aborto imediato ao ultrapassar limites de taxa/tamanho. (Aprovado)
- **Arquivos Malformados / Truncados:** Falha graciosa sem crash ou travamento de processo. (Aprovado)

### C. Performance & Eficiência de Memória (Resultados Oficiais RC)

```
| Operation                 | Format | Elapsed (ms) | Throughput / Stats| Memory (MB) |
|---------------------------|--------|--------------|-------------------|-------------|
| Compress Dataset          | ZIP    |          345 |         34,60 MB/s |       53,34 |
| Open & Enumerate Entries  | ZIP    |           47 |        501 entries |             |
| Extract Full Dataset      | ZIP    |          585 |         20,42 MB/s |             |
| Compress Dataset          | 7Z     |          185 |         64,52 MB/s |             |
| Open & Enumerate Entries  | 7Z     |           33 |        501 entries |             |
| Extract Full Dataset      | 7Z     |         1198 |          9,97 MB/s |             |
| Index 10,000 Entries      | Memory |            1 |      10000 entries |             |
| O(1) Dir Queries (x100)   | Memory |            1 |       12,9 us/query|             |
```

- Compressão 7Z: **64.52 MB/s** (Meta > 50 MB/s superada).
- Compressão ZIP: **34.60 MB/s** (Meta > 15 MB/s superada).
- Navegação O(1) com 10.000 itens: **12.9 µs / consulta** (Meta < 50 µs superada).

### D. Distribuição & Instalação
- Verificação do instalador `dist/FirezipSetup-x64-v1.0.11.exe` (SHA256: `9D22907123E80EEFC161A676088FFBBF305D3CC3456AF17418BAFD6FB14A04BD`).
- Instalação silenciosa em sandbox com `/CURRENTUSER`: Aprovado (ExitCode 0).
- Desinstalação silenciosa e limpeza completa: Aprovado (ExitCode 0).
- Validação dos manifestos Winget: `winget validate --manifest ...` aprovado com sucesso.
- Script de automação `packaging/verify_release_candidate.ps1`: 100% de sucesso.

### E. Documentação Oficial
- `README.md` completo publicado na raiz do repositório contendo badges, arquitetura, recursos, instalação via Winget e guia de compilação.

---

## 3. Checklist de Execução

- [x] **Passo 1:** Criar `CompatibilityTests.cs` cobrindo TAR, GZ, BZ2, nomes Unicode, caminhos longos, pastas vazias e zero-byte.
- [x] **Passo 2:** Executar suíte completa de testes (`dotnet test`) e garantir 100% de aprovação (93/93 testes aprovados).
- [x] **Passo 3:** Executar `Firezip.Benchmarks` e registrar métricas oficiais do Release Candidate.
- [x] **Passo 4:** Criar script `packaging/verify_release_candidate.ps1` para validar o instalador, flags silenciosas e manifestos Winget.
- [x] **Passo 5:** Criar `README.md` completo na raiz do repositório.
- [x] **Passo 6:** Atualizar `MILESTONE_10_PLAN.md` com status de conclusão de Milestone 10.
