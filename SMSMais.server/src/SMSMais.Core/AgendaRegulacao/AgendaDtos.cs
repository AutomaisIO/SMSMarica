// `AgendaRegulacao` e nao `Agenda`: isto NAO e uma agenda propria do municipio — e a leitura da
// agenda REGULADA pelo SISREG, que nos apenas observamos. O nome generico fica deliberadamente
// livre para o dia em que existir uma agenda de verdade, com marcacao nossa.
namespace SMSMais.Core.AgendaRegulacao;

/// <summary>
/// Recorte de toda consulta da Agenda. Datas em <b>dia de Brasília</b> — nunca <c>current_date</c>,
/// que às 21h já virou o dia seguinte em UTC e faria a tela mostrar números diferentes à noite.
/// </summary>
public sealed record AgendaFiltro(
    DateOnly De,
    DateOnly Ate,
    Guid? UnidadeId = null,
    /// <summary>CBO do profissional — é o que serve de "especialidade" (o SISREG não tem outra).</summary>
    string? Cbo = null,
    string? ProfissionalCpf = null,
    string? ProcedimentoCodigo = null);

/// <summary>Os números do topo da tela. Cada um responde a uma pergunta de gestão.</summary>
/// <param name="Vagas">Oferta do período: ocorrências de cada escala × vagas.</param>
/// <param name="Agendados">Ocupação: agendamentos importados no mesmo recorte.</param>
/// <param name="OcupacaoPercentual">Agendados ÷ vagas. Acima de 100 = encaixe além da vaga.</param>
/// <param name="DiasComOferta">Células (unidade × profissional × dia) com alguma vaga.</param>
/// <param name="DiasOciosos">Células com vaga e <b>nenhum</b> agendamento — onde está sobrando.</param>
/// <param name="DiasSobrecarregados">Células com mais agendamento que vaga — onde está faltando.</param>
/// <param name="AgendadosSemOferta">
/// Agendamentos sem escala do mesmo profissional naquele dia. Não é erro por si: pode ser unidade
/// que não publica escala, encaixe fora da grade, ou escala desatualizada no SISREG. É o número que
/// mede o quanto a grade descreve a realidade.
/// </param>
public sealed record AgendaResumoDto(
    int Vagas,
    int Agendados,
    int OcupacaoPercentual,
    int DiasComOferta,
    int DiasOciosos,
    int DiasSobrecarregados,
    int AgendadosSemOferta,
    int VagasPrimeiraVez,
    int VagasRetorno,
    int VagasReserva,
    int Profissionais,
    int Unidades);

/// <summary>Uma célula da agenda: o dia de um profissional numa unidade.</summary>
public sealed record AgendaDiaDto(
    DateOnly Data,
    Guid UnidadeId,
    string UnidadeNome,
    string ProfissionalCpf,
    string ProfissionalNome,
    string? Cbo,
    int Vagas,
    int Agendados,
    int Livres,
    /// <summary>Faixa do dia, juntando todos os blocos da escala.</summary>
    TimeOnly? HoraInicio,
    TimeOnly? HoraFim,
    int Blocos,
    /// <summary>Procedimentos ofertados no dia, para a linha dizer do que é a agenda.</summary>
    string Procedimentos);

public sealed record PaginaAgendaDto(int Total, IReadOnlyList<AgendaDiaDto> Itens);

/// <summary>Um bloco da escala dentro do dia — é o que o SISREG publica de verdade.</summary>
public sealed record BlocoEscalaDto(
    TimeOnly HoraInicio,
    TimeOnly HoraFim,
    string ProcedimentoCodigo,
    string ProcedimentoNome,
    int VagasPrimeiraVez,
    int VagasRetorno,
    int VagasReserva,
    /// <summary>
    /// Intervalo estimado entre pacientes: duração do bloco ÷ vagas. <b>Estimativa nossa</b>, não
    /// dado do SISREG — os minutos que ele informa vêm zerados em boa parte das linhas.
    /// </summary>
    int? MinutosPorVagaEstimado);

/// <summary>Quem ocupa uma vaga naquele dia.</summary>
public sealed record OcupanteDto(
    Guid SolicitacaoId,
    string? CodigoSolicitacao,
    DateTime DataAgendadaUtc,
    string? PacienteNome,
    string? ProcedimentoTexto,
    string? UnidadeSolicitanteNome,
    int StatusConfirmacao);

/// <summary>O detalhe de um dia: o que foi publicado, quem ocupa e a grade deduzida.</summary>
public sealed record AgendaDiaDetalheDto(
    DateOnly Data,
    string UnidadeNome,
    string ProfissionalNome,
    string? Cbo,
    int Vagas,
    int Agendados,
    IReadOnlyList<BlocoEscalaDto> Blocos,
    IReadOnlyList<OcupanteDto> Ocupantes);

/// <summary>Uma linha de ranking na tela de análise.</summary>
public sealed record AgendaRankingItemDto(
    string Chave,
    string Rotulo,
    int Vagas,
    int Agendados,
    int Livres,
    int OcupacaoPercentual);

/// <summary>Ocupação por dia da semana — onde a rede concentra oferta e onde ela sobra.</summary>
public sealed record AgendaPorDiaSemanaDto(
    int DiaSemana,
    string Rotulo,
    int Vagas,
    int Agendados,
    int OcupacaoPercentual);

/// <summary>Opções dos filtros, montadas do que existe de fato na grade.</summary>
public sealed record AgendaOpcoesDto(
    IReadOnlyList<OpcaoDto> Unidades,
    IReadOnlyList<OpcaoDto> Cbos,
    IReadOnlyList<OpcaoDto> Profissionais,
    IReadOnlyList<OpcaoDto> Procedimentos);

public sealed record OpcaoDto(string Valor, string Rotulo);
