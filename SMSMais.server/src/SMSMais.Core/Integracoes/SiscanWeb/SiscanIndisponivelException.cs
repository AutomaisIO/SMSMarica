namespace SMSMais.Core.Integracoes.SiscanWeb;

/// <summary>
/// O SISCAN (DATASUS) não atendeu por instabilidade/congestionamento do <b>lado dele</b> — não é
/// erro nosso. Serve para separar, no log e na resposta, "o SISCAN caiu/está lento" de "um bug
/// nosso": o middleware a mapeia para <b>503 retryável</b>, sem registrar em <c>registro_erro</c>
/// nem alertar o celular (mesmo tratamento do Klinikos). A classificação fina vai em
/// <see cref="Motivo"/> e no log <c>SISCAN_INSTAVEL</c> do transporte.
///
/// <para>É esperada em manhãs de pico do SISCAN. A ação do usuário é tentar de novo.</para>
/// </summary>
public sealed class SiscanIndisponivelException(string motivo, string mensagem, Exception? innerException = null)
    : Exception(mensagem, innerException)
{
    /// <summary>Categoria da falha — ver <see cref="Motivos"/>. Vai na resposta e no log para a
    /// investigação distinguir timeout (lentidão) de reset (queda) de 5xx (indisponível).</summary>
    public string Motivo { get; } = motivo;

    public static class Motivos
    {
        /// <summary>O HttpClient estourou o timeout esperando a resposta — SISCAN lento/congestionado.</summary>
        public const string Timeout = "timeout";

        /// <summary>A conexão foi derrubada no meio (connection reset / broken pipe).</summary>
        public const string ConexaoResetada = "conexao_resetada";

        /// <summary>Outra falha de transporte (DNS, TLS, recusa de conexão).</summary>
        public const string Rede = "rede";

        /// <summary>O SISCAN respondeu HTTP 5xx (erro/indisponibilidade do servidor dele).</summary>
        public const string HttpServidor = "http_servidor";
    }
}
