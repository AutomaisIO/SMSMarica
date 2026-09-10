namespace SMSMais.Core.Integracoes.SisregWeb.Ofertas.Dtos;

/// <summary>
/// Um bloco de agenda que passou a existir no SISREG dentro da janela olhada.
/// </summary>
/// <param name="Blocos">Quantas linhas de escala formam esta agenda (uma por dia da semana, às
/// vezes uma por profissional). Aparece na tela porque "1 bloco" e "7 blocos" com as mesmas vagas
/// são ofertas diferentes: a segunda é recorrente.</param>
/// <param name="DiasSemana">Dias em que a agenda abre, como <c>DayOfWeek</c> (0 = domingo).</param>
/// <param name="VistaEm">Quando NÓS vimos esta agenda pela primeira vez. Não é quando ela nasceu no
/// SISREG — o export não diz isso, e a diferença entre as duas é justamente o atraso que
/// sincronizar mais vezes por dia reduz.</param>
/// <param name="EsperaMedianaDias">Mediana de espera de quem já foi atendido neste procedimento.
/// Nulo quando não há histórico suficiente.</param>
public sealed record AgendaNovaDto(
    Guid UnidadeId,
    string UnidadeNome,
    string ProcedimentoCodigo,
    string ProcedimentoNome,
    string? CboDescricao,
    int Blocos,
    int Vagas,
    DateOnly VigenciaInicio,
    DateOnly VigenciaFim,
    IReadOnlyList<int> DiasSemana,
    DateTime VistaEm,
    int? EsperaMedianaDias);

/// <summary>
/// Um agendamento que sumiu do SISREG e cuja data ainda não passou — candidato a vaga livre.
/// </summary>
/// <param name="AlteracaoId">A linha da fila de alterações que originou isto. É a mesma coisa que a
/// tela de alterações mostra como "sumiu do SISREG"; aqui aparece pelo lado da oportunidade.</param>
/// <param name="DetectadaEm">Quando a varredura percebeu a ausência.</param>
public sealed record VagaLiberadaDto(
    Guid AlteracaoId,
    Guid SolicitacaoId,
    Guid? UnidadeId,
    string? UnidadeNome,
    string? ProcedimentoCodigo,
    string? ProcedimentoNome,
    DateTime DataAgendada,
    DateTime DetectadaEm,
    int? EsperaMedianaDias);

/// <summary>Uma pessoa esperando por este procedimento.</summary>
/// <param name="EsperandoHaDias">Dias desde o pedido. É a conta que a tela ordena por padrão —
/// quem espera há mais tempo primeiro.</param>
/// <param name="Risco">0 = vermelho (mais urgente) … 3 = azul; nulo quando o SISREG não
/// classificou.</param>
public sealed record PessoaNaFilaDto(
    string CodigoSolicitacao,
    DateOnly? DataSolicitacao,
    int? EsperandoHaDias,
    int? Risco,
    string? Nome,
    int? IdadeAnos,
    DateOnly? DataNascimento,
    string? Cns,
    string? Telefone,
    string? UnidadeSolicitante,
    string? CidCodigo);

/// <param name="Total">Quantas pessoas esperam por este procedimento, além da página devolvida.</param>
/// <param name="EsperaP50Dias">Mediana da espera de <b>quem ainda está na fila</b> — diferente da
/// espera mostrada na oferta, que é a de quem já conseguiu data.</param>
public sealed record FilaDaOfertaDto(
    string ProcedimentoNome,
    int Total,
    int? EsperaP50Dias,
    int? EsperaMaxDias,
    IReadOnlyDictionary<string, int> PorRisco,
    IReadOnlyList<PessoaNaFilaDto> Pessoas);

/// <param name="JanelaDias">Janela de novidade efetivamente usada (o pedido é limitado a 1–90).</param>
/// <param name="DiasEsperaUrgente">Acima disto a tela destaca a oferta.</param>
/// <param name="DiasVagaPerecivel">Vaga para daqui a menos que isto exige ação hoje.</param>
public sealed record OfertasSisregDto(
    IReadOnlyList<AgendaNovaDto> AgendasNovas,
    IReadOnlyList<VagaLiberadaDto> VagasLiberadas,
    int JanelaDias,
    int DiasEsperaUrgente,
    int DiasVagaPerecivel);
