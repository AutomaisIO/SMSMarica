namespace SMSMarica.Data.Entities;

/// <summary>
/// Registro de um erro não tratado (500) capturado pelo middleware — append-only.
/// Guarda o trace técnico (tipo, mensagem, stack, path, usuário) associado a um
/// <see cref="CodigoReferencia"/> curto que é mostrado ao usuário final. Assim o
/// suporte pesquisa pelo código sem que a infra seja exposta na resposta HTTP.
///
/// Não guarda corpo da requisição (evita PII/LGPD); só metadados de diagnóstico.
/// </summary>
public sealed class RegistroErro
{
    public Guid Id { get; set; }

    /// <summary>Código curto exibido ao usuário (ex.: "ERRO-4F9C2A"). Indexado p/ busca.</summary>
    public string CodigoReferencia { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

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
