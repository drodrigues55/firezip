# Firezip v1.0.1 — Bandizip Extraction Experience & Polish Release 🚀

A versão **v1.0.1** do Firezip eleva a experiência de uso a um novo patamar de produtividade e flexibilidade, trazendo **extração em dois cliques estilo Bandizip**, tela de progresso avançada com **dupla barra de progresso**, detalhes analíticos de **taxa de compressão**, ações pós-extração, arquitetura de **configurações isoladas em Roaming** e integração total com as diretrizes do Windows Explorer / Windows 11.

---

## ✨ O que há de novo na Versão 1.0.1

### ⚡ Extração Rápida com Dois Cliques (Estilo Bandizip)
- **Ação Configurável via Configurações:** Defina exatamente o comportamento ao clicar duas vezes em um arquivo compactado no Windows Explorer:
  - `Abrir na Interface do Firezip` (Padrão)
  - `Extrair na mesma pasta (Aqui)`
  - `Extrair em uma nova subpasta com o nome do arquivo`
  - `Extrair em uma pasta pré-definida` (com seleção de caminho customizável)
  - `Perguntar destino sempre` (abre seletor nativo do Explorer sem abrir a janela principal)
- **Execução Leve e Instantânea:** Ao extrair via duplo clique, a interface principal não é inicializada desnecessariamente, disparando diretamente a janela de progresso de tarefa dedicada.

### 📊 Interface de Extração Avançada (`TaskProgressWindow`)
- **Dupla Barra de Progresso:**
  - **Progresso por Item:** Visualização em tempo real do item individual sendo extraído (`ItemProgressBar`).
  - **Progresso Geral do Arquivo:** Barra de progresso acumulado de todo o arquivo compactado (`TaskProgressBar`).
- **Dropdown / Expander de Estatísticas de Compressão:**
  - Taxa de compressão detalhada (`Ratio %`).
  - Tamanho original vs. tamanho compactado.
  - Espaço economizado em disco.
  - Velocidade em tempo real (MB/s), tempo decorrido e estimativa restante (ETA).
- **Caixas de Seleção Rápidas (Tickers Y/N):**
  - `Abrir pasta após conclusão`: abre a pasta de destino automaticamente no Explorer.
  - `Manter janela aberta após conclusão`: permite auditar o relatório final sem fechar repentinamente.
  - Sincronização bidirecional instantânea com as preferências do aplicativo.
- **Ações Rápidas Pós-Extração (quando a janela permanece aberta):**
  - 📁 **Abrir Pasta**: Acessa a pasta recém-extraída com um clique.
  - 🗑️ **Excluir arquivo de origem (.zip)**: Apaga com segurança o arquivo compactado original após a extração com feedback de status.
  - ✕ **Fechar**: Encerra a janela de tarefas.

### 🛡️ Isolamento Completo das Configurações do Usuário
- **Armazenamento em `%APPDATA%\Firezip\settings.json` (Roaming):**
  - As configurações do usuário agora são mantidas completamente desacopladas do diretório de instalação do aplicativo (`Program Files`).
  - **Imunidade a Reinstalações e Atualizações:** Reinstalar do zero, atualizar pelo instalador ou pelo Winget nunca apagará ou corromperá as preferências do usuário.
  - **Migração Transparente:** Migra automaticamente configurações herdadas de `%LocalAppData%`.

### 🗂️ Registro de Associação de Arquivos no Windows 11 / Explorer
- **Registro Oficial de `Capabilities` e `RegisteredApplications`:**
  - Total conformidade com a política moderna de `UserChoice` e integridade do Windows 10/11.
  - Firezip agora é registrado formalmente como aplicativo manipulador de arquivos compactados.
  - Botão integrado em **Configurações > Abrir Configurações de Aplicativos Padrão do Windows** para associar com 1 clique nas opções do sistema.
  - Notificação de shell via `SHChangeNotify(SHCNE_ASSOCCHANGED)` para atualização imediata dos ícones no Explorer.

### 💎 Diálogo Nativo "Sobre o Firezip" (AboutDialog) & i18n Dinâmica
- Diálogo estilizado em WinUI 3 Fluent exibindo versão dinâmica, plataforma (.NET 10, Windows App SDK), licença MIT e links.
- Tradução imediata dos rótulos da barra de ferramentas ao alterar idiomas (pt-BR / en-US) sem reiniciar o app.

---

## 📥 Como Instalar

### Opção 1: Instalador Oficial (Recomendado)
Baixe o instalador **`FirezipSetup-x64-v1.0.1.exe`** anexado nesta release e execute a instalação assistida.

Para instalação silenciosa via script ou linha de comando:
```powershell
FirezipSetup-x64-v1.0.1.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-
```

### Opção 2: Versão Portátil (Standalone)
Baixe **`Firezip-v1.0.1-windows-x64-portable.zip`**, descompacte em qualquer pasta e execute `Firezip.UI.exe`.

### Opção 3: Atualização Automática
Se você já possui a versão v1.0.0 instalada, o aplicativo verificará o canal de atualização e notificará a disponibilidade da versão 1.0.1 de forma segura via HTTPS com validação criptográfica RSA-SHA256.

---

## 🛡️ Tabela Oficial de Hashes (SHA-256)

| Arquivo | Tamanho | Checksum SHA-256 |
| :--- | :--- | :--- |
| `FirezipSetup-x64-v1.0.1.exe` | 162.39 MB | `5CCA810923B35D3763777041F123E0D7C0D3CC69BD3BBDA507268F4223FD93E6` |
| `Firezip-v1.0.1-windows-x64-portable.zip` | 88.56 MB | `7F546A6401979038AE18F10FEA1D8AA7803AB1F69B346404AC4A257E2B035C20` |
| `manifest.json` | 712 B | `C2953BC61336FD86994A3ED8730F988D3FD4895591BC6AC968F29AD1AF744ABD` |
