using System.Net;
using System.Text.Json;

namespace SMSMais.Core.Cidadao.Push;

/// <summary>Quanto o desfecho de um envio pesa no log: <see cref="Erro"/> vira aviso no celular.</summary>
public enum GravidadeFalhaFcm
{
    Nenhuma,
    Aviso,
    Erro,
}

/// <summary>
/// Desfecho de um envio a UM aparelho. <see cref="Detalhe"/> vai para o painel;
/// <see cref="Resumo"/> compõe a falha do histórico; <see cref="Tecnico"/> só vai para o log.
/// </summary>
public sealed record DesfechoEnvioFcm(
    bool Entregue,
    string? Detalhe,
    bool AparelhoRemovido,
    GravidadeFalhaFcm Gravidade,
    string? Resumo,
    string? Tecnico)
{
    public static readonly DesfechoEnvioFcm Aceito = new(true, null, false, GravidadeFalhaFcm.Nenhuma, null, null);

    public bool CredencialRecusada => Detalhe == DetalheCredencial;

    public const string DetalheAparelhoRemovido =
        "O app foi desinstalado ou as notificações foram desligadas neste aparelho.";
    public const string DetalheApns =
        "O Firebase recusou a chave da Apple (APNs). Confira a configuração do projeto.";
    public const string DetalheCredencial =
        "O Firebase recusou a credencial do servidor. Confira Integrações → Firebase.";
    public const string DetalheIndisponivel =
        "O Firebase não respondeu agora. Tente de novo em alguns minutos.";
    public const string DetalheOutroProjeto =
        "Este aparelho está ligado a outro projeto do Firebase. O app precisa ser gerado com o mesmo projeto da chave em Integrações → Firebase.";

    public static DesfechoEnvioFcm Credencial(string tecnico) =>
        new(false, DetalheCredencial, false, GravidadeFalhaFcm.Erro, "credencial do servidor recusada", tecnico);

    public static DesfechoEnvioFcm Indisponivel(string tecnico) =>
        new(false, DetalheIndisponivel, false, GravidadeFalhaFcm.Aviso, "Firebase indisponível", tecnico);

    /// <summary>
    /// <c>SENDER_ID_MISMATCH</c>: o app foi gerado com um projeto do Firebase e a chave em Integrações
    /// é de outro. Erro de configuração, não de aparelho — o token continua bom para o projeto certo,
    /// então não é apagado.
    /// </summary>
    public static DesfechoEnvioFcm OutroProjeto(string tecnico) =>
        new(false, DetalheOutroProjeto, false, GravidadeFalhaFcm.Erro, "app ligado a outro projeto do Firebase", tecnico);
}

/// <summary>
/// Desfecho do envio de validação (<c>validate_only</c>) do "Testar". <see cref="Mensagem"/> vai para a
/// tela de Integrações; <see cref="Tecnico"/> só para o log.
/// </summary>
public sealed record ValidacaoEnvioFcm(bool Ok, string Mensagem, string? Tecnico);

/// <summary>
/// O Google não entregou o access token (credencial recusada ou serviço fora). A mensagem é a do
/// Google, para o "Testar" da tela de Integrações; o <see cref="Desfecho"/> é o que cada aparelho
/// recebe quando isso acontece no meio de um envio.
/// </summary>
public sealed class FalhaTokenFcmException(string mensagem, DesfechoEnvioFcm desfecho) : Exception(mensagem)
{
    public DesfechoEnvioFcm Desfecho { get; } = desfecho;
}

/// <summary>
/// Leitura das respostas de erro do FCM HTTP v1. O motivo de verdade está em
/// <c>error.details[]</c> (<c>FcmError.errorCode</c>), não no status HTTP: um 401 pode ser a
/// credencial do servidor ou a chave da Apple, e só o <c>errorCode</c> separa os dois.
/// </summary>
internal static class InterpretadorRespostaFcm
{
    private const string TipoFcmError = "type.googleapis.com/google.firebase.fcm.v1.FcmError";
    private const string TipoBadRequest = "type.googleapis.com/google.rpc.BadRequest";
    private const string TipoErrorInfo = "type.googleapis.com/google.rpc.ErrorInfo";

    public static DesfechoEnvioFcm Interpretar(HttpStatusCode status, string? corpo)
    {
        var codigoHttp = (int)status;
        if (codigoHttp is >= 200 and < 300) return DesfechoEnvioFcm.Aceito;

        var (statusGoogle, mensagem, errorCode, citaToken, _) = Ler(corpo);
        var tecnico = $"FCM HTTP {codigoHttp} {errorCode ?? statusGoogle ?? "-"}: {mensagem ?? "(sem mensagem)"}";

        // Só estes dois dizem que o TOKEN morreu (é o que a documentação do FCM manda descartar).
        if (errorCode is "UNREGISTERED"
            || (status == HttpStatusCode.BadRequest && citaToken
                && (errorCode ?? statusGoogle) == "INVALID_ARGUMENT"))
            return new(false, DesfechoEnvioFcm.DetalheAparelhoRemovido, true,
                GravidadeFalhaFcm.Nenhuma, "app desinstalado", tecnico);

        // Vem como 403 PERMISSION_DENIED: precisa vir antes do teste de credencial logo abaixo.
        if (errorCode is "SENDER_ID_MISMATCH")
            return DesfechoEnvioFcm.OutroProjeto(tecnico);

        // APNS_AUTH_ERROR é o nome antigo do mesmo erro (o SDK oficial ainda mapeia os dois).
        if (errorCode is "THIRD_PARTY_AUTH_ERROR" or "APNS_AUTH_ERROR")
            return new(false, DesfechoEnvioFcm.DetalheApns, false,
                GravidadeFalhaFcm.Erro, "chave da Apple (APNs) recusada", tecnico);

        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || statusGoogle is "PERMISSION_DENIED" or "UNAUTHENTICATED")
            return DesfechoEnvioFcm.Credencial(tecnico);

        if (status == HttpStatusCode.TooManyRequests || codigoHttp >= 500
            || errorCode is "QUOTA_EXCEEDED" or "UNAVAILABLE" or "INTERNAL")
            return DesfechoEnvioFcm.Indisponivel(tecnico);

        var resumo = Encurtar(mensagem ?? $"HTTP {codigoHttp}", 200);
        return new(false, $"O Firebase recusou a mensagem: {resumo}", false,
            GravidadeFalhaFcm.Erro, resumo, tecnico);
    }

    /// <summary>
    /// Resposta do envio de validação do "Testar" → o que a tela diz. Nunca devolve o corpo do Google
    /// para a tela: ele vai inteiro só no <see cref="ValidacaoEnvioFcm.Tecnico"/>.
    /// </summary>
    public static ValidacaoEnvioFcm InterpretarValidacao(HttpStatusCode status, string? corpo, string projectId)
    {
        var codigoHttp = (int)status;
        if (codigoHttp is >= 200 and < 300)
            return new(true,
                $"Credencial válida: o servidor autenticou e o Firebase aceita envios do projeto {projectId}.", null);

        var (statusGoogle, mensagem, errorCode, _, razao) = Ler(corpo);
        var tecnico = $"FCM validate_only HTTP {codigoHttp} {razao ?? errorCode ?? statusGoogle ?? "-"}: {mensagem ?? "(sem mensagem)"}";

        // API "Firebase Cloud Messaging (V1)" desligada no Google Cloud: o token sai, o envio não. O
        // ErrorInfo é o sinal estável; o texto é a rede de segurança para resposta sem details.
        var apiDesligada = razao == "SERVICE_DISABLED"
            || (status == HttpStatusCode.Forbidden && mensagem is not null
                && (mensagem.Contains("has not been used", StringComparison.OrdinalIgnoreCase)
                    || mensagem.Contains("is disabled", StringComparison.OrdinalIgnoreCase)));
        if (apiDesligada)
            return new(false,
                $"A API Firebase Cloud Messaging (V1) está desligada no projeto {projectId}. Ative no Google Cloud (APIs e serviços) e teste de novo.",
                tecnico);

        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || statusGoogle is "PERMISSION_DENIED" or "UNAUTHENTICATED")
            return new(false,
                $"A conta de serviço não tem permissão de envio no projeto {projectId} (papel Firebase Cloud Messaging Admin ou Firebase Admin SDK).",
                tecnico);

        if (status == HttpStatusCode.NotFound)
            return new(false,
                $"O Firebase não encontrou o projeto {projectId}. Confira se o JSON é da conta de serviço do projeto certo e cole de novo.",
                tecnico);

        if (status == HttpStatusCode.TooManyRequests || codigoHttp >= 500)
            return new(false, $"O Firebase não respondeu agora (HTTP {codigoHttp}). Tente de novo em alguns minutos.", tecnico);

        return new(false, $"O Firebase recusou o envio de teste do projeto {projectId} (HTTP {codigoHttp}).", tecnico);
    }

    private static (string? Status, string? Mensagem, string? ErrorCode, bool CitaToken, string? Razao) Ler(string? corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo)) return (null, null, null, false, null);
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("error", out var erro)
                || erro.ValueKind != JsonValueKind.Object)
                return (null, null, null, false, null);

            string? errorCode = null;
            string? razao = null;
            var citaToken = false;
            if (erro.TryGetProperty("details", out var detalhes) && detalhes.ValueKind == JsonValueKind.Array)
            {
                foreach (var d in detalhes.EnumerateArray())
                {
                    if (d.ValueKind != JsonValueKind.Object) continue;
                    var tipo = Texto(d, "@type");
                    if (tipo == TipoFcmError) errorCode ??= Texto(d, "errorCode");
                    // Erro genérico das APIs do Google (ex.: SERVICE_DISABLED), não do FCM.
                    if (tipo == TipoErrorInfo) razao ??= Texto(d, "reason");
                    if (tipo == TipoBadRequest
                        && d.TryGetProperty("fieldViolations", out var violacoes)
                        && violacoes.ValueKind == JsonValueKind.Array)
                    {
                        citaToken |= violacoes.EnumerateArray().Any(v =>
                            v.ValueKind == JsonValueKind.Object
                            && Texto(v, "field") is { } campo
                            && campo.Contains("token", StringComparison.OrdinalIgnoreCase));
                    }
                }
            }

            return (Texto(erro, "status"), Texto(erro, "message"), errorCode, citaToken, razao);
        }
        catch (JsonException)
        {
            return (null, Encurtar(corpo, 200), null, false, null);
        }
    }

    private static string? Texto(JsonElement e, string campo) =>
        e.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    internal static string Encurtar(string texto, int max)
    {
        var limpo = texto.ReplaceLineEndings(" ").Trim();
        return limpo.Length <= max ? limpo : limpo[..(max - 1)] + "…";
    }
}
