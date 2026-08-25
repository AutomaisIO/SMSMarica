using System.Text;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Integracoes.SernitWeb;

/// <summary>
/// Resposta crua do SERNIT. Mantida em <b>bytes</b> por paridade com o SER-RJ (e robustez): a
/// leitura decide se decodifica como texto. O SERNIT não tem Exportar, então na prática a resposta
/// é sempre HTML — mas guardar bytes evita corromper qualquer conteúdo binário eventual.
/// </summary>
public sealed record RespostaSernit(byte[] Corpo, string? ContentType, string? NomeArquivo, string? Location)
{
    private string? _texto;

    /// <summary>Corpo decodificado como UTF-8.</summary>
    public string Texto => _texto ??= Encoding.UTF8.GetString(Corpo);

    public bool EhTexto => true;
}

/// <summary>Uma linha da grade "Solicitações de Consulta ou Exame" do SERNIT, ainda crua.
/// A grade do SERNIT NÃO traz CPF/Solicitante/Município (a conta já é escopada a Maricá).</summary>
public sealed record SernitLinhaGrade
{
    public string IdSernit { get; init; } = string.Empty;
    public string? Tipo { get; init; }
    public string? Recurso { get; init; }
    public string? DataSolicitacao { get; init; }
    public string? Paciente { get; init; }
    public string? Idade { get; init; }
    public string? Cpf { get; init; }
    public string? Cns { get; init; }
    public string? Cid { get; init; }
    public string? Solicitante { get; init; }
    public string? MunicipioSolicitante { get; init; }
    public string? AgendadoPara { get; init; }
    public string? Situacao { get; init; }
    public string? UnidadeExecutora { get; init; }
}

/// <summary>
/// Resultado de uma pesquisa: linhas da página + páginas do scroller + total real informado.
///
/// <para>No SERNIT a grade lê no máximo 100 (5 páginas × 20), mas o box <c>form0:msgErro</c>
/// informa o <c>Total de resultados encontrados: N</c> quando passa disso. <see cref="TotalReal"/>
/// é esse número (null quando a mensagem não veio, ou seja, a janela cabe); <see cref="Capada"/>
/// indica que a grade foi truncada em 100.</para>
/// </summary>
public sealed record SernitPaginaGrade(
    IReadOnlyList<SernitLinhaGrade> Linhas, int Paginas, int? TotalReal, bool Capada);

/// <summary>Um evento da trilha do histórico, ainda cru.</summary>
public sealed record SernitEventoLido
{
    public string? Data { get; init; }
    public string? Evento { get; init; }
    public string? EstadoAnterior { get; init; }
    public string? EstadoAtual { get; init; }
    public string? CentralRegulacao { get; init; }
    public string? UnidadeExecutora { get; init; }
    public string? Usuario { get; init; }
    public string? LotacaoEvento { get; init; }
    public string? Ip { get; init; }
    public string? Observacao { get; init; }
}

/// <summary>A tela de histórico inteira: dados do paciente + trilha.</summary>
public sealed record SernitHistorico(
    IReadOnlyDictionary<string, string> Paciente,
    IReadOnlyList<SernitEventoLido> Eventos)
{
    /// <summary>ID da solicitação conforme a PRÓPRIA tela de histórico — conferência de que o
    /// SERNIT não devolveu a tela de outra solicitação.</summary>
    public string? IdSolicitacao => Paciente.TryGetValue("ID Solicitação", out var v) ? v : null;
}

/// <summary>Filtros da tela de pesquisa do SERNIT. <see cref="Situacao"/> é obrigatória —
/// pesquisar sem ela devolve zero. O eixo de fatiamento é <c>Data da Solicitação</c> (imutável).</summary>
public sealed record SernitFiltroPesquisa
{
    public required SituacaoSernit Situacao { get; init; }
    public TipoRecursoSernit? Tipo { get; init; }
    public DateOnly? DataSolicitacaoInicio { get; init; }
    public DateOnly? DataSolicitacaoFim { get; init; }
    public string? Cpf { get; init; }
    public string? Nome { get; init; }
    public string? Cns { get; init; }
    public string? IdSolicitacao { get; init; }
}

/// <summary>
/// Os telefones da tela de edição do SERNIT, como ela os renderiza. Os ids são posicionais/voláteis
/// (medido: Residencial <c>form0:j_id157</c>, Celular <c>form0:j_id159</c>) — o nome JSF vem junto
/// do valor porque é resolvido pelo RÓTULO a cada leitura, nunca chumbado.
/// </summary>
public sealed record SernitContatosDaTela(
    (string Nome, string Valor)? Residencial,
    (string Nome, string Valor)? WhatsApp,
    (string Nome, string Valor)? Contato);

/// <summary>Tradução entre os enums do domínio e os <c>value</c> dos combos do SERNIT (idênticos
/// aos do SER-RJ; o combo de Situação é <c>form0:j_id54</c>, id volátil).</summary>
public static class SernitCodigos
{
    public static string Codigo(SituacaoSernit situacao) => situacao switch
    {
        SituacaoSernit.EmFila => "EM_FILA",
        SituacaoSernit.Pendente => "PENDENTE",
        SituacaoSernit.Agendada => "AGENDADA",
        SituacaoSernit.ChegadaNaoConfirmada => "CHEGADA_NAO_CONFIRMADA",
        SituacaoSernit.ChegadaConfirmada => "CHEGADA_CONFIRMADA",
        SituacaoSernit.Cancelada => "CANCELADA",
        SituacaoSernit.Alta => "ALTA",
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, "situação sem código no SERNIT"),
    };

    public static string Codigo(TipoRecursoSernit tipo) => tipo switch
    {
        TipoRecursoSernit.Consulta => "CONSULTA",
        TipoRecursoSernit.Exame => "EXAME",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "tipo sem código no SERNIT"),
    };

    /// <summary>Texto da coluna "Situação" da grade → enum (sem acento/caixa).</summary>
    public static SituacaoSernit? DoTextoSituacao(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return Normalizar(texto) switch
        {
            "em fila" => SituacaoSernit.EmFila,
            "pendente" => SituacaoSernit.Pendente,
            "agendada" => SituacaoSernit.Agendada,
            "chegada nao confirmada" => SituacaoSernit.ChegadaNaoConfirmada,
            "chegada confirmada" => SituacaoSernit.ChegadaConfirmada,
            "cancelada" => SituacaoSernit.Cancelada,
            "alta" => SituacaoSernit.Alta,
            _ => null,
        };
    }

    public static TipoRecursoSernit? DoTextoTipo(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return Normalizar(texto) switch
        {
            "consulta" => TipoRecursoSernit.Consulta,
            "exame" => TipoRecursoSernit.Exame,
            _ => null,
        };
    }

    private static string Normalizar(string texto)
    {
        var semAcento = new string(
            texto.Normalize(NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                            != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray());
        return semAcento.Trim().ToLowerInvariant();
    }
}
