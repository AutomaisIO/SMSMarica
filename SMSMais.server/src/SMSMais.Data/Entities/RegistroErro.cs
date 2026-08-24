namespace SMSMais.Data.Entities;

/// <summary>
/// Registro de um erro não tratado (500) capturado pelo middleware. Guarda o trace técnico
/// (tipo, mensagem, stack, path, usuário) associado a um <see cref="CodigoReferencia"/> curto
/// que é mostrado ao usuário final. Assim o suporte pesquisa pelo código sem que a infra seja
/// exposta na resposta HTTP.
///
/// <para>NÃO é mais append-only: erros idênticos (mesma <see cref="Assinatura"/>) são
/// deduplicados — reusam o mesmo código e apenas incrementam <see cref="Ocorrencias"/>. Um erro
/// pode ser marcado como resolvido (<see cref="ResolvidoEm"/>); a partir daí uma reincidência
/// gera um código novo (sinal de regressão), pois o dedup só casa erros em aberto.</para>
///
/// Não guarda corpo da requisição (evita PII/LGPD); só metadados de diagnóstico.
/// </summary>
public sealed class RegistroErro
{
    public Guid Id { get; set; }

    /// <summary>Código curto exibido ao usuário (ex.: "ERRO-4F9C2A"). Indexado p/ busca.</summary>
    public string CodigoReferencia { get; set; } = string.Empty;

    /// <summary>
    /// Impressão digital do erro (SHA-256 hex de método+caminho+status+tipo+mensagem). Erros com a
    /// mesma assinatura e ainda em aberto são deduplicados no mesmo registro. Indexada.
    /// </summary>
    public string Assinatura { get; set; } = string.Empty;

    /// <summary>Quantas vezes este mesmo erro (assinatura) ocorreu.</summary>
    public int Ocorrencias { get; set; } = 1;

    /// <summary>Data/hora da primeira ocorrência.</summary>
    public DateTime CriadoEm { get; set; }

    /// <summary>Data/hora da ocorrência mais recente (atualizada a cada dedup).</summary>
    public DateTime UltimaOcorrenciaEm { get; set; }

    /// <summary>Quando marcado como resolvido (null = em aberto). Marcado pela skill de suporte.</summary>
    public DateTime? ResolvidoEm { get; set; }

    /// <summary>Quem/como resolveu (ex.: commit, autor). Texto livre curto.</summary>
    public string? ResolvidoPor { get; set; }

    /// <summary>Nota da resolução (o que foi feito).</summary>
    public string? ResolucaoNota { get; set; }

    /// <summary>Método HTTP (GET/POST/PUT…).</summary>
    public string Metodo { get; set; } = string.Empty;

    /// <summary>Caminho da requisição (sem query string).</summary>
    public string Caminho { get; set; } = string.Empty;

    public string? QueryString { get; set; }

    public int StatusCode { get; set; }

    /// <summary>Nome completo do tipo da exceção.</summary>
    public string TipoExcecao { get; set; } = string.Empty;

    public string Mensagem { get; set; } = string.Empty;

    public string? StackTrace { get; set; }

    /// <summary>Resumo da exceção interna (tipo: mensagem), se houver.</summary>
    public string? Interna { get; set; }

    /// <summary>Correlação de trace (HttpContext.TraceIdentifier / Activity).</summary>
    public string? TraceId { get; set; }

    public Guid? UsuarioId { get; set; }

    public string? UsuarioNome { get; set; }

    public string? UserAgent { get; set; }
}
