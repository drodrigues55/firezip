# Sub-Plan: Milestone 11 — General Availability (GA) & Internationalization (i18n)

> **Status:** Concluído (Complete) ✅  
> **Versão Alvo:** 1.0.0 (General Availability)  
> **Objetivo:** Preparar e finalizar o produto para lançamento público oficial (GA): unificação de metadados de assembly e release para v1.0.0, sistema de internacionalização e localização (i18n) com suporte nativo a Português do Brasil (`pt-BR`) e Inglês (`en-US`), expansão de manifestos Winget multilíngues, documentação de distribuição e bateria de testes de validação.

---

## 1. Visão Geral das Entregas

```
                                ┌──────────────────────────────────────────────┐
                                │       Milestone 11 — General Availability    │
                                │                 (v1.0.0 GA)                  │
                                └──────────────────────┬───────────────────────┘
                                                       │
          ┌───────────────────┬────────────────────────┼───────────────────────┬───────────────────┐
          ▼                   ▼                        ▼                       ▼                   ▼
 ┌─────────────────┐ ┌─────────────────┐      ┌─────────────────┐     ┌─────────────────┐ ┌─────────────────┐
 │   Metadados &   │ │ Internacionali- │      │  Winget Locale  │     │   Documentação  │ │   Testes &      │
 │  Versão 1.0.0   │ │ zação (i18n)    │      │  Manifests      │     │   Oficial       │ │   Qualidade     │
 │ (Props, Company,│ │ (pt-BR, en-US,  │      │ (en-US, pt-BR,  │     │ (README, Badges,│ │ (121 Testes,    │
 │  Copyright, MIT)│ │  Fallback, UI)  │      │  YAML Schema)   │     │  Planos, Guias) │ │  100% Aprovados)│
 └─────────────────┘ └─────────────────┘      └─────────────────┘     └─────────────────┘ └─────────────────┘
```

---

## 2. Componentes e Tarefas Detalhadas

### A. Metadados e Identidade de Versão (v1.0.0 GA)
- Atualizado `Directory.Build.props` para versão unificada `1.0.0` com todos os metadados oficiais:
  - `Company`: Firezip Open Source Team
  - `Authors`: Firezip Authors
  - `Copyright`: Copyright (c) 2026 Firezip Authors
  - `PackageLicenseExpression`: MIT
  - `PackageProjectUrl` e `RepositoryUrl`
- Sincronizado Inno Setup (`firezip_setup.iss` -> `1.0.0`) e inicializadores.

### B. Sistema de Internacionalização (i18n)
- Criação de `ILocalizationService` em `Firezip.Core.Interfaces`.
- Implementação de `LocalizationService` em `Firezip.Infrastructure.Services` com catálogo de strings:
  - Detecção automática de cultura do sistema (`CultureInfo.CurrentUICulture`).
  - Suporte explícito a `en-US` e `pt-BR`.
  - Fallback gracioso para inglês em caso de chave ausente.
- Integração em `ISettingsService` com propriedade `Language` (`System`, `en-US`, `pt-BR`).
- Exposição da seleção de idioma em `SettingsViewModel` e `SettingsDialog.xaml`.
- Mensagens de erro padronizadas e localizadas em `ErrorMessageFormatter.cs`.

### C. Manifestos Oficiais do Winget v1.0.0
- Estruturação dos manifestos em `packaging/winget/manifests/f/Firezip/Firezip/1.0.0/`:
  - `Firezip.Firezip.yaml`
  - `Firezip.Firezip.installer.yaml`
  - `Firezip.Firezip.locale.en-US.yaml`
  - `Firezip.Firezip.locale.pt-BR.yaml` (Metadados em português)

### D. Documentação Oficial
- Atualizado `README.md` com selo de **General Availability (v1.0.0)** e contagem oficial de 121 testes.
- Atualizado `IMPLEMENTATION_PLAN.md` com a inclusão formal do Milestone 11.
- Documentado uso de idiomas e opções de distribuição.

### E. Testes de Validação e Homologação
- Testes automatizados dedicados em `LocalizationTests.cs`:
  - Alternância dinâmica de cultura e evento `CultureChanged`.
  - Integridade dos catálogos em ambos os idiomas (`en-US` e `pt-BR`).
  - Resiliência contra chaves nulas ou inexistentes com fallback sem exceções.
  - Formatação com argumentos em tempo de execução.
  - Persistência e restauração da configuração de idioma em `SettingsService`.
  - Verificação de metadados de versão `1.0.0`.
- **Resultado da Suíte Completa:** **121 testes executados, 121 aprovados (100%), 0 falhas.**

