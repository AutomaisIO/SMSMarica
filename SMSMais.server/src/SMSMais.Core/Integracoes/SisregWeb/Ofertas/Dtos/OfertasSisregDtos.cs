namespace SMSMais.Core.Integracoes.SisregWeb.Ofertas.Dtos;

/// <summary>
/// Um bloco de agenda que passou a existir no SISREG dentro da janela olhada.
/// </summary>
/// <param name="Blocos">Quantas linhas de escala formam esta agenda (uma por dia da semana, às
/// vezes uma por profissional). Aparece na tela porque "1 bloco" e "7 blocos" com as mesmas vagas
/// são ofertas diferentes: a segunda é recorrente.</param>
/// <param name="VigenciaInicio">Validade do bloco semanal no SISREG. <b>Não é</b> "o período em que
/// há vaga" — o bloco pode estar lotado até novembro e a vigência dizer julho a dezembro. Quem
/// responde "quando há vaga" são as datas da oferta (<see cref="DatasDaOfertaDto"/>).</param>
/// <param name="DiasSemana">Dias em que a agenda abre, como <c>DayOfWeek</c> (0 = domingo).</param>
/// <param name="VistaEm">Quando NÓS vimos esta agenda pela primeira vez. Não é quando ela nasceu no
/// SISREG — o export não diz isso, e a diferença entre as duas é justamente o atraso que
/// sincronizar mais vezes por dia reduz.</param>
/// <param name="EsperaMedianaDias">Mediana de espera de quem já foi atendido neste procedimento.
/// Nulo quando não há histórico suficiente.</param>
/// <param name="AgendaLocal">A unidade marca direto nestas vagas; elas não passam pela
/// regulação e o regulador nunca as vê.</param>
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
    int? EsperaMedianaDias,
    bool AgendaLocal);

/// <summary>
/// Um agendamento que sumiu do SISREG e cuja data ainda não passou — candidato a vaga livre.
/// </summary>
/// <param name="AlteracaoId">A linha da fila de alterações que originou isto. É a mesma coisa que a
/// tela de alterações mostra como "sumiu do SISREG"; aqui aparece pelo lado da oportunidade.</param>
/// <param name="DetectadaEm">Quando a varredura percebeu a ausência.</param>
/// <param name="AgendaLocal">A escala da unidade para este procedimento é agenda local: a vaga
/// volta para a unidade, não para a regulação. Nulo quando não há escala vigente para dizer.</param>
public sealed record VagaLiberadaDto(
    Guid AlteracaoId,
    Guid SolicitacaoId,
    Guid? UnidadeId,
    string? UnidadeNome,
    string? ProcedimentoCodigo,
    string? ProcedimentoNome,
    DateTime DataAgendada,
    DateTime DetectadaEm,
    int? EsperaMedianaDias,
    bool? AgendaLocal);

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
/// <param name="ProcedimentosIncluidos">Os nomes que entraram na conta. Vaga de grupo serve a quem
/// pediu qualquer item do grupo, e vaga de item serve a quem pediu o grupo — a tela diz quais para
/// ninguém achar que a lista misturou filas.</param>
public sealed record FilaDaOfertaDto(
    string ProcedimentoNome,
    int Total,
    int? EsperaP50Dias,
    int? EsperaMaxDias,
    IReadOnlyDictionary<string, int> PorRisco,
    IReadOnlyList<PessoaNaFilaDto> Pessoas,
    IReadOnlyList<string> ProcedimentosIncluidos);

/// <summary>Um dia de agenda da unidade para o procedimento.</summary>
/// <param name="Vagas">Vagas de <b>primeira vez</b> — é o que a regulação marca. Retorno e reserva
/// são da unidade.</param>
/// <param name="Agendados">Agendamentos que já importamos para o dia (sem os cancelados).</param>
/// <param name="Livres">Estimativa: o que sobra da agenda do dia, limitado às vagas de primeira
/// vez. É dedução nossa (escala − agendados); a verdade é a grade do SISREG.</param>
public sealed record DiaDaOfertaDto(
    DateOnly Data,
    TimeOnly HoraInicio,
    TimeOnly HoraFim,
    int Vagas,
    int Agendados,
    int Livres,
    IReadOnlyList<string> Profissionais);

/// <summary>Uma unidade que executa o procedimento, com as datas dela.</summary>
/// <param name="PrimeiraVagaLivre">O primeiro dia com vaga de primeira vez sobrando. É a resposta
/// para "quando dá para marcar" — e não a vigência da escala.</param>
/// <param name="SemAgendamentoFuturo">A escala diz que há vaga, mas não existe <b>nenhum</b>
/// agendamento futuro nela, e ela não é nova. Visto no CDT/ECG em 10/09/2026: 280 vagas por
/// semana declaradas, zero marcações desde que o SISREG deixou de ofertá-las. A escala sozinha não
/// prova vaga; a tela avisa em vez de anunciar.</param>
public sealed record UnidadeDaOfertaDto(
    Guid UnidadeId,
    string UnidadeNome,
    bool AgendaLocal,
    DateOnly? PrimeiraVagaLivre,
    int VagasLivres,
    int AgendadosFuturos,
    bool SemAgendamentoFuturo,
    IReadOnlyList<DiaDaOfertaDto> Dias);

/// <summary>As datas de um procedimento, por unidade — o que o SISREG mostra ao autorizar.</summary>
public sealed record DatasDaOfertaDto(
    string ProcedimentoCodigo,
    string? ProcedimentoNome,
    DateOnly De,
    DateOnly Ate,
    IReadOnlyList<UnidadeDaOfertaDto> Unidades);

/// <param name="JanelaDias">Janela de novidade efetivamente usada (o pedido é limitado a 1–90).</param>
/// <param name="DiasEsperaUrgente">Acima disto a tela destaca a oferta.</param>
/// <param name="DiasVagaPerecivel">Vaga para daqui a menos que isto exige ação hoje.</param>
public sealed record OfertasSisregDto(
    IReadOnlyList<AgendaNovaDto> AgendasNovas,
    IReadOnlyList<VagaLiberadaDto> VagasLiberadas,
    int JanelaDias,
    int DiasEsperaUrgente,
    int DiasVagaPerecivel);
