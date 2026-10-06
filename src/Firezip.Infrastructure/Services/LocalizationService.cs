using System.Globalization;
using Firezip.Core.Interfaces;

namespace Firezip.Infrastructure.Services;

/// <summary>
/// Production-ready localization service supporting en-US and pt-BR with graceful fallback.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private string _currentCulture = "System";
    private string _effectiveCulture = "en-US";

    public event EventHandler? CultureChanged;

    public IReadOnlyList<string> SupportedCultures { get; } = ["System", "en-US", "pt-BR"];

    public string CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture != value)
            {
                _currentCulture = value;
                ResolveEffectiveCulture();
                CultureChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string EffectiveCulture => _effectiveCulture;

    public LocalizationService(string initialCulture = "System")
    {
        _currentCulture = initialCulture;
        ResolveEffectiveCulture();
    }

    private void ResolveEffectiveCulture()
    {
        if (string.Equals(_currentCulture, "System", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(_currentCulture))
        {
            var systemCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            _effectiveCulture = systemCulture.Equals("pt", StringComparison.OrdinalIgnoreCase) ? "pt-BR" : "en-US";
        }
        else if (string.Equals(_currentCulture, "pt-BR", StringComparison.OrdinalIgnoreCase) || _currentCulture.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            _effectiveCulture = "pt-BR";
        }
        else
        {
            _effectiveCulture = "en-US";
        }
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        // Try effective culture first
        if (StringCatalogs.TryGetValue(_effectiveCulture, out var catalog) && catalog.TryGetValue(key, out var val))
        {
            return val;
        }

        // Fallback to en-US
        if (StringCatalogs["en-US"].TryGetValue(key, out var fallbackVal))
        {
            return fallbackVal;
        }

        // Return key if not found
        return key;
    }

    public string GetString(string key, params object[] args)
    {
        var raw = GetString(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, raw, args);
        }
        catch
        {
            return raw;
        }
    }

    public string this[string key] => GetString(key);

    private static readonly Dictionary<string, Dictionary<string, string>> StringCatalogs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en-US"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AppName"] = "Firezip",
            ["Action_Open"] = "Open",
            ["Action_New"] = "New",
            ["Action_Extract"] = "Extract",
            ["Action_Test"] = "Test",
            ["Action_Delete"] = "Delete",
            ["Action_Refresh"] = "Refresh",
            ["Action_Settings"] = "Settings",
            ["Action_Help"] = "Help",
            ["Action_About"] = "About",
            ["Action_Cancel"] = "Cancel",
            ["Action_Save"] = "Save",
            ["Action_Close"] = "Close",
            ["Action_Browse"] = "Browse...",
            ["Action_OK"] = "OK",

            ["Column_Name"] = "Name",
            ["Column_Size"] = "Original Size",
            ["Column_CompressedSize"] = "Compressed Size",
            ["Column_Modified"] = "Date Modified",
            ["Column_Ratio"] = "Ratio",
            ["Column_Encrypted"] = "Encrypted",

            ["Status_Ready"] = "Ready",
            ["Status_ItemsCount"] = "{0} items",
            ["Status_TotalSize"] = "Total: {0}",
            ["Status_Selected"] = "{0} selected",

            ["Progress_Compressing"] = "Compressing...",
            ["Progress_Extracting"] = "Extracting...",
            ["Progress_Testing"] = "Testing archive integrity...",
            ["Progress_Speed"] = "{0:F2} MB/s",
            ["Progress_Elapsed"] = "Elapsed: {0}",
            ["Progress_Remaining"] = "Remaining: {0}",

            ["Settings_Title"] = "Preferences",
            ["Settings_General"] = "General",
            ["Settings_Language"] = "Language",
            ["Settings_Language_System"] = "System default",
            ["Settings_Language_En"] = "English (en-US)",
            ["Settings_Language_Pt"] = "Portuguese (pt-BR)",
            ["Settings_Theme"] = "Theme",
            ["Settings_Theme_System"] = "System",
            ["Settings_Theme_Light"] = "Light",
            ["Settings_Theme_Dark"] = "Dark",
            ["Settings_DefaultExtractFolder"] = "Default extraction folder",
            ["Settings_OpenFolderAfter"] = "Open folder after extraction",
            ["Settings_ConfirmOverwrite"] = "Confirm before overwriting files",
            ["Settings_ConfirmDelete"] = "Confirm before deleting",
            ["Settings_AutoCloseTask"] = "Auto-close progress window when completed",
            ["Settings_NotifyCompletion"] = "Show notification on completion",
            ["Settings_ExplorerIntegration"] = "Windows Explorer Integration",
            ["Settings_EnableContextMenu"] = "Enable right-click context menu",
            ["Settings_CascadingMenu"] = "Use cascading submenu (Firezip >)",
            ["Settings_Updates"] = "Updates",
            ["Settings_AutoCheckUpdates"] = "Check for updates automatically",
            ["Settings_UpdateFrequency"] = "Check frequency",
            ["Settings_CurrentVersion"] = "Current version: v{0}",
            ["Settings_LastCheck"] = "Last check: {0}",
            ["Settings_CheckNow"] = "Check now",
            ["Settings_InstallUpdate"] = "Install Update",

            ["Update_UpToDate"] = "You are using the latest version.",
            ["Update_Available"] = "New version available: v{0}",
            ["Update_Checking"] = "Checking for updates...",
            ["Update_Offline"] = "Could not check for updates. The application works normally offline.",
            ["Update_Error"] = "Error checking updates: {0}",
            ["Update_SignatureInvalid"] = "Update rejected: invalid digital signature or tampered package.",
            ["Update_DowngradeRejected"] = "Remote version is older than current version (downgrade rejected).",

            ["Conflict_Title"] = "File already exists",
            ["Conflict_Overwrite"] = "Overwrite",
            ["Conflict_Skip"] = "Skip",
            ["Conflict_Rename"] = "Rename",
            ["Conflict_ApplyToAll"] = "Apply to all remaining items",

            ["Password_Title"] = "Password Required",
            ["Password_Prompt"] = "This archive is encrypted. Please enter the password:",
            ["Password_Show"] = "Show password",
            ["Password_Incorrect"] = "Incorrect password. Please try again.",

            ["Error_PasswordIncorrect"] = "The password may be incorrect, or this archive may use encryption that Firezip cannot read. Check the password and try again.",
            ["Error_SecurityViolation"] = "Firezip blocked an unsafe archive entry to protect files outside the selected destination.",
            ["Error_AccessDenied"] = "Windows denied access to a file or folder. Check its permissions and make sure it is not being used by another app.",
            ["Error_NotFound"] = "A file or folder needed for this operation could not be found. Check the selected location and try again.",
            ["Error_CorruptArchive"] = "Firezip could not read this archive. It may be damaged, incomplete, or in a format this build cannot open.",
            ["Error_SharingViolation"] = "A file needed for this operation is open in another app. Close it and try again.",
            ["Error_IntegrityFailed"] = "Firezip could not verify this archive. It may be damaged or incomplete.",
            ["Error_ExtractFailed"] = "Firezip could not finish extracting the selected files. Check the destination and archive, then try again.",
            ["Error_CompressFailed"] = "Firezip could not create the archive. Check the selected files and destination, then try again.",
            ["Error_Generic"] = "Firezip could not complete this operation. Check the file or folder and try again."
        },
        ["pt-BR"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AppName"] = "Firezip",
            ["Action_Open"] = "Abrir",
            ["Action_New"] = "Novo",
            ["Action_Extract"] = "Extrair",
            ["Action_Test"] = "Testar",
            ["Action_Delete"] = "Excluir",
            ["Action_Refresh"] = "Atualizar",
            ["Action_Settings"] = "Configurações",
            ["Action_Help"] = "Ajuda",
            ["Action_About"] = "Sobre",
            ["Action_Cancel"] = "Cancelar",
            ["Action_Save"] = "Salvar",
            ["Action_Close"] = "Fechar",
            ["Action_Browse"] = "Procurar...",
            ["Action_OK"] = "OK",

            ["Column_Name"] = "Nome",
            ["Column_Size"] = "Tamanho Original",
            ["Column_CompressedSize"] = "Tamanho Compactado",
            ["Column_Modified"] = "Data de Modificação",
            ["Column_Ratio"] = "Taxa",
            ["Column_Encrypted"] = "Criptografado",

            ["Status_Ready"] = "Pronto",
            ["Status_ItemsCount"] = "{0} itens",
            ["Status_TotalSize"] = "Total: {0}",
            ["Status_Selected"] = "{0} selecionados",

            ["Progress_Compressing"] = "Compactando...",
            ["Progress_Extracting"] = "Extraindo...",
            ["Progress_Testing"] = "Testando integridade do arquivo...",
            ["Progress_Speed"] = "{0:F2} MB/s",
            ["Progress_Elapsed"] = "Decorrido: {0}",
            ["Progress_Remaining"] = "Restante: {0}",

            ["Settings_Title"] = "Preferências",
            ["Settings_General"] = "Geral",
            ["Settings_Language"] = "Idioma",
            ["Settings_Language_System"] = "Padrão do sistema",
            ["Settings_Language_En"] = "Inglês (en-US)",
            ["Settings_Language_Pt"] = "Português (pt-BR)",
            ["Settings_Theme"] = "Tema",
            ["Settings_Theme_System"] = "Sistema",
            ["Settings_Theme_Light"] = "Claro",
            ["Settings_Theme_Dark"] = "Escuro",
            ["Settings_DefaultExtractFolder"] = "Pasta de extração padrão",
            ["Settings_OpenFolderAfter"] = "Abrir pasta após extração",
            ["Settings_ConfirmOverwrite"] = "Confirmar antes de sobrescrever arquivos",
            ["Settings_ConfirmDelete"] = "Confirmar antes de excluir",
            ["Settings_AutoCloseTask"] = "Fechar janela de progresso automaticamente ao concluir",
            ["Settings_NotifyCompletion"] = "Notificar ao concluir tarefa",
            ["Settings_ExplorerIntegration"] = "Integração com Windows Explorer",
            ["Settings_EnableContextMenu"] = "Habilitar menu de contexto do Windows Explorer",
            ["Settings_CascadingMenu"] = "Usar submenu em cascata (Firezip >)",
            ["Settings_Updates"] = "Atualizações",
            ["Settings_AutoCheckUpdates"] = "Verificar atualizações automaticamente",
            ["Settings_UpdateFrequency"] = "Frequência de verificação",
            ["Settings_CurrentVersion"] = "Versão atual: v{0}",
            ["Settings_LastCheck"] = "Última verificação: {0}",
            ["Settings_CheckNow"] = "Verificar agora",
            ["Settings_InstallUpdate"] = "Instalar Atualização",

            ["Update_UpToDate"] = "Você está usando a versão mais recente.",
            ["Update_Available"] = "Nova versão disponível: v{0}",
            ["Update_Checking"] = "Verificando atualizações...",
            ["Update_Offline"] = "Não foi possível verificar atualizações. O aplicativo funciona normalmente offline.",
            ["Update_Error"] = "Erro ao verificar: {0}",
            ["Update_SignatureInvalid"] = "Atualização recusada: assinatura digital inválida ou pacote adulterado.",
            ["Update_DowngradeRejected"] = "Versão remota inferior à versão atual (downgrade recusado).",

            ["Conflict_Title"] = "O arquivo já existe",
            ["Conflict_Overwrite"] = "Sobrescrever",
            ["Conflict_Skip"] = "Pular",
            ["Conflict_Rename"] = "Renomear",
            ["Conflict_ApplyToAll"] = "Aplicar a todos os itens restantes",

            ["Password_Title"] = "Senha Necessária",
            ["Password_Prompt"] = "Este arquivo está criptografado. Digite a senha:",
            ["Password_Show"] = "Mostrar senha",
            ["Password_Incorrect"] = "Senha incorreta. Tente novamente.",

            ["Error_PasswordIncorrect"] = "A senha pode estar incorreta, ou este arquivo pode usar criptografia incompatível com o Firezip. Verifique a senha e tente novamente.",
            ["Error_SecurityViolation"] = "O Firezip bloqueou uma entrada insegura do arquivo para proteger pastas fora do destino selecionado.",
            ["Error_AccessDenied"] = "O Windows negou acesso a um arquivo ou pasta. Verifique as permissões e certifique-se de que não está em uso por outro aplicativo.",
            ["Error_NotFound"] = "Um arquivo ou pasta necessário para esta operação não foi encontrado. Verifique o local selecionado e tente novamente.",
            ["Error_CorruptArchive"] = "O Firezip não conseguiu ler este arquivo. Ele pode estar danificado, incompleto ou em um formato incompatível.",
            ["Error_SharingViolation"] = "Um arquivo necessário para esta operação está aberto em outro aplicativo. Feche-o e tente novamente.",
            ["Error_IntegrityFailed"] = "O Firezip não conseguiu verificar este arquivo. Ele pode estar danificado ou incompleto.",
            ["Error_ExtractFailed"] = "O Firezip não conseguiu concluir a extração dos arquivos. Verifique o destino e o arquivo e tente novamente.",
            ["Error_CompressFailed"] = "O Firezip não conseguiu criar o arquivo. Verifique os arquivos selecionados e o destino e tente novamente.",
            ["Error_Generic"] = "O Firezip não conseguiu concluir esta operação. Verifique o arquivo ou pasta e tente novamente."
        }
    };
}
