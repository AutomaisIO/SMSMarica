namespace SMSMais.Core.Common.Excecoes;

/// <summary>
/// A VM de telefonia (Automais.Pabx) não respondeu, respondeu erro interno ou não está
/// configurada. Erro de infraestrutura: nada é gravado do lado de cá. Mapeado para 503.
/// </summary>
public sealed class TelefoniaIndisponivelException(string mensagem, Exception? innerException = null)
    : Exception(mensagem, innerException)
{
    public string Codigo { get; } = "telefonia.indisponivel";
}
