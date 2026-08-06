using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Integracoes.SerWeb;

/// <summary>Uma linha da grade "Solicitações de Consulta ou Exame" do SER, ainda crua.</summary>
public sealed record SerLinhaGrade
{
    public string IdSer { get; init; } = string.Empty;
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
}

/// <summary>Resultado de uma pesquisa: as linhas da página + quantas páginas o scroller expõe.</summary>
public sealed record SerPaginaGrade(IReadOnlyList<SerLinhaGrade> Linhas, int Paginas);

/// <summary>Um evento da trilha do histórico, ainda cru.</summary>
public sealed record SerEventoLido
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
public sealed record SerHistorico(
    IReadOnlyDictionary<string, string> Paciente,
    IReadOnlyList<SerEventoLido> Eventos)
{
    /// <summary>ID da solicitação conforme a PRÓPRIA tela de histórico. Serve de conferência:
    /// se não bater com o pedido, o SER devolveu a tela de outra solicitação.</summary>
    public string? IdSolicitacao => Paciente.TryGetValue("ID Solicitação", out var v) ? v : null;
}

/// <summary>Filtros da tela de pesquisa do SER. <see cref="Situacao"/> é obrigatória —
/// pesquisar sem ela devolve zero.</summary>
public sealed record SerFiltroPesquisa
{
    public required SituacaoSer Situacao { get; init; }
    public TipoRecursoSer? Tipo { get; init; }
    public DateOnly? DataSolicitacaoInicio { get; init; }
    public DateOnly? DataSolicitacaoFim { get; init; }
    public string? Cpf { get; init; }
    public string? Nome { get; init; }
    public string? Cns { get; init; }
    public string? IdSolicitacao { get; init; }
}

/// <summary>Tradução entre os enums do domínio e os <c>value</c> dos combos do SER.</summary>
public static class SerCodigos
{
    /// <summary>Valores do combo <c>form0:j_id75</c>.</summary>
    public static string Codigo(SituacaoSer situacao) => situacao switch
    {
        SituacaoSer.EmFila => "EM_FILA",
        SituacaoSer.Pendente => "PENDENTE",
        SituacaoSer.Agendada => "AGENDADA",
        SituacaoSer.ChegadaNaoConfirmada => "CHEGADA_NAO_CONFIRMADA",
        SituacaoSer.ChegadaConfirmada => "CHEGADA_CONFIRMADA",
        SituacaoSer.Cancelada => "CANCELADA",
        SituacaoSer.Alta => "ALTA",
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, "situação sem código no SER"),
    };

    public static string Codigo(TipoRecursoSer tipo) => tipo switch
    {
        TipoRecursoSer.Consulta => "CONSULTA",
        TipoRecursoSer.Exame => "EXAME",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "tipo sem código no SER"),
    };

    /// <summary>Texto da coluna "Situação" da grade → enum. Comparação sem acento/caixa porque
    /// o SER escreve "Chegada Não Confirmada" com acento e a grade às vezes normaliza.</summary>
    public static SituacaoSer? DoTextoSituacao(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = Normalizar(texto);
        return t switch
        {
            "em fila" => SituacaoSer.EmFila,
            "pendente" => SituacaoSer.Pendente,
            "agendada" => SituacaoSer.Agendada,
            "chegada nao confirmada" => SituacaoSer.ChegadaNaoConfirmada,
            "chegada confirmada" => SituacaoSer.ChegadaConfirmada,
            "cancelada" => SituacaoSer.Cancelada,
            "alta" => SituacaoSer.Alta,
            _ => null,
        };
    }

    public static TipoRecursoSer? DoTextoTipo(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return Normalizar(texto) switch
        {
            "consulta" => TipoRecursoSer.Consulta,
            "exame" => TipoRecursoSer.Exame,
            _ => null,
        };
    }

    private static string Normalizar(string texto)
    {
        var semAcento = new string(
            texto.Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                            != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray());
        return semAcento.Trim().ToLowerInvariant();
    }
}
