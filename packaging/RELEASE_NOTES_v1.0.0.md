# Firezip v1.0.0 — General Availability (GA) 🚀

Temos o prazer de anunciar o lançamento da primeira versão estável e oficial do **Firezip v1.0.0** — o gerenciador de arquivos compactados moderno, veloz e nativo projetado para o Windows 11 e Windows 10.

O Firezip foi desenvolvido para substituir gerenciadores de arquivos legados, trazendo uma interface moderna com material Mica, arquitetura em **.NET 10** e **WinUI 3**, total isolamento de rede no aplicativo principal e segurança robusta contra vulnerabilidades de descompressão.

---

## ✨ Destaques da Versão 1.0.0

### 🎨 Interface Moderna & Fluent Design
- **Backdrop Mica & Efeitos Visuais:** Integração perfeita com a linguagem visual moderna do Windows 11.
- **Modos Escuro e Claro:** Detecção automática do tema do sistema ou seleção manual instantânea.
- **Navegação Ultra-Rápida:** Indexação de diretórios virtuais em memória com busca e navegação $O(1)$, suportando arquivos com mais de 10.000 itens sem lentidão.
- **Atalhos de Teclado Completos:** `Ctrl+O` (Abrir), `Ctrl+N` (Novo), `Ctrl+E` (Extrair), `F5` (Atualizar), `Backspace` (Subir nível) e `Delete`.

### 🌐 Internacionalização & Suporte Multi-Idioma (i18n)
- Suporte nativo completo a **Português do Brasil (`pt-BR`)** e **Inglês (`en-US`)**.
- Alternância dinâmica de idioma na tela de preferências sem necessidade de reiniciar o aplicativo.
- Mensagens de erro em linguagem humana com detecção inteligente de falhas de permissão, arquivos corrompidos e senhas incorretas.

### 📦 Motor de Compressão e Formatos
- **Leitura e Criação:**
  - **ZIP:** Deflate, Deflate64, BZip2, LZMA e proteção por criptografia AES-256.
  - **7-Zip (.7z):** Algoritmos LZMA / LZMA2 de alta taxa de compressão e criptografia de cabeçalhos.
  - **TAR (.tar), GZip (.tar.gz, .tgz) e BZip2 (.tar.bz2, .tbz2)**: Suporte completo preservando caminhos relativos.
- **Leitura e Extração:**
  - **RAR:** Extração com suporte aos formatos RAR4 e RAR5.
  - **XZ (.tar.xz, .xz), ISO e CAB.**

### 🪟 Integração com Windows Explorer & Janelas Leves
- **Menu de Contexto:** Ações rápidas (*Extrair aqui*, *Extrair para pasta...*, *Compactar para .zip*, *Compactar para .7z*) com ícone oficial do aplicativo.
- **Micro Janelas de Progresso:** Operações pelo menu de contexto disparam uma janela compacta e leve exibindo throughput em tempo real (MB/s), tempo decorrido, previsão de término e contagem de itens, sem precisar carregar a interface pesada.
- **Resolução de Conflitos e Senhas:** Diálogos in-place com suporte a substituição, renomeação automática e alternância para exibir/ocultar senha.

### 🛡️ Segurança Rigorosa & Isolamento de Rede
- **Zero Acesso à Rede no App Principal:** `Firezip.UI.exe` opera com zero conexões TCP/UDP abertas. 100% funcional offline, sem telemetria, rastreamento ou chamadas externas.
- **Atualizador Separado (`FirezipUpdater.exe`):** Acesso estritamente HTTPS com proteção contra redirects inseguros, downloads em área temporária segura, verificação prévia de espaço em disco e cota máxima de 500 MB.
- **Assinatura Digital RSA-SHA256:** Todos os manifestos são criptograficamente validados contra uma chave raiz de confiança antes de qualquer instalação.
- **Hardening de Descompressão:** Proteção integrada contra ataques de *Zip Slip* (path traversal), neutralização de *Alternate Data Streams (ADS)* do NTFS, bloqueio de nomes de dispositivos reservados DOS (`CON`, `PRN`, `AUX`, `NUL`) e prevenção de bombas de descompressão (*Zip Bombs*).

---

## 📥 Como Instalar

### Opção 1: Instalador Oficial (Recomendado)
Baixe o instalador **`FirezipSetup-x64-v1.0.0.exe`** anexado nesta release e execute a instalação assistida.

Para instalação silenciosa via script ou linha de comando:
```powershell
FirezipSetup-x64-v1.0.0.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-
```

### Opção 2: Versão Portátil (Standalone)
Baixe **`Firezip-v1.0.0-windows-x64-portable.zip`**, descompacte em qualquer pasta e execute `Firezip.UI.exe`. Não requer privilégios de administrador.

### Opção 3: Windows Package Manager (Winget)
```powershell
winget install Firezip.Firezip
```

---

## 🛡️ Tabela Oficial de Hashes (SHA-256)

Utilize os hashes abaixo para verificar a integridade e autenticidade dos arquivos baixados:

| Arquivo | Tamanho | Checksum SHA-256 |
| :--- | :--- | :--- |
| `FirezipSetup-x64-v1.0.0.exe` | 162.36 MB | `C0EA4372C835D3B3AD3773306B5997084A7AED50A947234F04256C9132B8C17B` |
| `Firezip-v1.0.0-windows-x64-portable.zip` | 88.54 MB | `796911C5C7D74602388D452D7296254EB076BC26F641F4608E197D85001DDF49` |
| `manifest.json` | 709 B | `410EE3ADB75A209E187E1CA4721786851F324B8ED773198AA4FE88FECB9D5881` |

*(Para verificar localmente no PowerShell: `Get-FileHash -Algorithm SHA256 <arquivo>`)*
