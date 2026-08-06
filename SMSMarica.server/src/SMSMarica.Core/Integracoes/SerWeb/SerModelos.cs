using System.Text;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Integracoes.SerWeb;

/// <summary>
/// Resposta crua do SER. <b>Bytes, não string:</b> o botão Exportar devolve um <c>.xls</c> BIFF8
/// (OLE2) e decodificá-lo como UTF-8 destrói a planilha silenciosamente — sobram caracteres de
/// substituição no lugar dos bytes, e o arquivo deixa de abrir sem nenhum erro no caminho.
/// </summary>
public sealed record RespostaSer(byte[] Corpo, string? ContentType, string? NomeArquivo, string? Location)
{
    private string? _texto;

    /// <summary>Corpo decodificado como UTF-8. Só faz sentido quando <see cref="EhPlanilha"/> é falso.</summary>
    public string Texto => _texto ??= Encoding.UTF8.GetString(Corpo);

    /// <summary>Assinatura de arquivo composto OLE2 (<c>D0 CF 11 E0 A1 B1 1A E1</c>) — é o
    /// invólucro do BIFF8. Reconhecer pelo conteúdo, e não pelo <c>Content-Type</c>, porque o
    /// WildFly do SER responde a planilha com <c>text/html</c> quando a sessão azeda.</summary>
    public bool EhPlanilha =>
        Corpo.Length >= 8
        && Corpo[0] == 0xD0 && Corpo[1] == 0xCF && Corpo[2] == 0x11 && Corpo[3] == 0xE0
        && Corpo[4] == 0xA1 && Corpo[5] == 0xB1 && Corpo[6] == 0x1A && Corpo[7] == 0xE1;

    public bool EhTexto => !EhPlanilha;
}

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

    /// <summary>
    /// Onde o paciente vai ser atendido. <b>Só a tela de Histórico (a do export) traz isso</b> — a
    /// grade da tela de Solicitação não tem essa coluna, então fica nulo quando a linha veio de lá.
    /// </summary>
    public string? UnidadeExecutora { get; init; }
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

/// <summary>
/// Filtros da tela <b>Consulta → Histórico de Consulta/Exame</b> (<c>historico-pesquisar.seam</c>),
/// que é a que exporta 500 de uma vez.
///
/// <para>O eixo de recorte é <c>Data da Solicitação</c> (<c>form0:dataInicial/FinalInputDate</c>),
/// e não a data de agendamento: só a data da solicitação é imutável, então as mesmas fatias saem
/// iguais entre execuções.</para>
/// </summary>
public sealed record SerFiltroExport
{
    public required SituacaoSer Situacao { get; init; }
    public TipoRecursoSer? Tipo { get; init; }
    public required DateOnly DataSolicitacaoInicio { get; init; }
    public required DateOnly DataSolicitacaoFim { get; init; }

    /// <summary>
    /// Unidade solicitante, em <b>texto puro</b>, no campo <c>form0:suggUnidadeSol</c>. É o que
    /// recorta a consulta para Maricá. O hidden <c>_selection</c> do autocomplete fica vazio mesmo
    /// quando se clica na sugestão pelo navegador — medido em 06/08/2026 — então não o mandamos.
    ///
    /// <para><b>Não usar <c>form0:municipio</c>:</b> aquele campo é <i>Município do Paciente</i>, e
    /// paciente de outro município pode ter solicitação aberta por Maricá — filtrar por ele
    /// esconderia gente que é nossa.</para>
    /// </summary>
    public string UnidadeSolicitante { get; init; } = "GESTOR SMS MARICA";
}

/// <summary>Um lote exportado: as linhas da planilha + se o SER avisou que cortou em 500.</summary>
public sealed record LoteExportSer(IReadOnlyList<SerLinhaGrade> Linhas, bool Truncado)
{
    /// <summary>Maior <c>Data da Solicitação</c> do lote, quando legível.</summary>
    public DateOnly? MaiorDataSolicitacao { get; init; }

    /// <summary>O aviso de corte com as palavras do próprio SER, para mostrar a quem opera em vez
    /// de um "truncado: true" que não explica nada.</summary>
    public string? Aviso { get; init; }
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
