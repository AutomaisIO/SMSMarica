namespace SMSMarica.Core.Common.Excecoes;

/// <summary>
/// Falha no armazenamento de arquivos (DigitalOcean Spaces / S3 indisponível ou não
/// configurado). É erro de infraestrutura — nunca há gravação local de fallback: a
/// operação falha e o usuário deve procurar o suporte. Mapeado para 503 no middleware.
/// </summary>
public sealed class ArmazenamentoIndisponivelException(string mensagem, Exception? innerException = null)
    : Exception(mensagem, innerException)
{
    public string Codigo { get; } = "armazenamento.indisponivel";
}
