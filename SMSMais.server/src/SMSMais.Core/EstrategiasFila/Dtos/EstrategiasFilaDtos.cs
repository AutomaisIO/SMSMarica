using SMSMais.Core.AgendaRegulacao;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.EstrategiasFila.Dtos;

// ---------------------------------------------------------------------------------------------
// Lista de procedimentos
// ---------------------------------------------------------------------------------------------

/// <summary>Uma linha da lista de procedimentos: o que se escolhe para simular, já com o tamanho
/// da fila ao lado do nome.</summary>
/// <param name="Codigo">Código do SISREG (<c>pa</c>). Nulo = só existe na fila, sem escala.</param>
/// <param name="NaFila">Pessoas esperando hoje (família inteira: grupo + itens).</param>
/// <param name="EsperaMedianaDias">Mediana de dias esperando, de quem ainda espera.</param>
/// <param name="VagasRegulacaoSemana">Vagas de regulação (1ª vez + reserva) por semana, na escala vigente.</param>
/// <param name="TemEstrategia">Já existe estratégia viva para este procedimento.</param>
public sealed record ProcedimentoComFilaDto(
    string? Codigo,
    string Nome,
    string? NomeCanonico,
    bool EhGrupo,
    int NaFila,
    int? EsperaMedianaDias,
    int VagasRegulacaoSemana,
    int Unidades,
    int Profissionais,
    bool TemEstrategia);

// ---------------------------------------------------------------------------------------------
// Parâmetros
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Um parâmetro numérico da estratégia, com a <b>trava</b>.
///
/// <para>Travado = o agente recebe como fato e o backend rejeita proposta que o altere. Livre = o
/// agente pode escolher dentro de <see cref="Min"/>..<see cref="Max"/> (quando informados).</para>
/// </summary>
public sealed record ParametroNumero(double Valor, bool Travado = false, double? Min = null, double? Max = null)
{
    public ParametroNumero Com(double valor) => this with { Valor = valor };
}

/// <summary>Vagas extras numa semana específica (mutirão, hora extra pontual).</summary>
public sealed record MutiraoDto(int Semana, int Vagas, string? Descricao = null);

/// <summary>Objetivo da estratégia — o que o agente otimiza.</summary>
public static class ObjetivoEstrategia
{
    /// <summary>Zerar a fila até <c>PrazoAlvoSemanas</c>, com o menor acréscimo de recursos.</summary>
    public const string ZerarEmSemanas = "zerar_em_semanas";

    /// <summary>Parar de crescer: capacidade ≥ entrada, sem compromisso de zerar.</summary>
    public const string Equilibrio = "equilibrio";

    /// <summary>Zerar no menor prazo possível com os recursos travados (o que der).</summary>
    public const string MinimoRecursos = "minimo_recursos";

    public static readonly IReadOnlyList<string> Todos = [ZerarEmSemanas, Equilibrio, MinimoRecursos];
}

/// <summary>
/// Uma linha do <b>quadro</b> de simulação: um profissional (real, vindo da escala, ou de
/// simulação) com os dias da semana em que atende e quantas vagas de regulação rende por turno.
///
/// <para>Para o profissional real, <see cref="DiasReais"/> guarda os dias que a escala publica hoje
/// (o que se mostra "aceso" de partida) e <see cref="OutrasEscalas"/> os dias em que ele já tem
/// escala de OUTRO procedimento ou unidade — o agente não pode acender esses; a tela avisa.</para>
/// </summary>
/// <param name="Id">Estável e sem PII: hash do CPF para o real, <c>sim-…</c> para o simulado.</param>
public sealed record LinhaQuadro(
    string Id,
    string Nome,
    bool Simulado,
    Guid? UnidadeId,
    string Unidade,
    /// <summary>Dias da semana acesos (0=domingo … 6=sábado).</summary>
    IReadOnlyList<int> Dias,
    double AtendimentosPorTurno,
    /// <summary>Travado = o agente não mexe nesta linha.</summary>
    bool Travado,
    IReadOnlyList<int> DiasReais,
    IReadOnlyDictionary<int, string> OutrasEscalas)
{
    public int Turnos => Dias?.Where(d => d is >= 0 and <= 6).Distinct().Count() ?? 0;
    public double VagasSemana => Turnos * Math.Max(0, AtendimentosPorTurno);
}

/// <summary>
/// Os parâmetros da simulação. O centro é o <see cref="Quadro"/>: quem atende, em que dias, com
/// quantas vagas por turno. Todos têm valor inicial tirado do cenário atual, de modo que "rodar
/// sem mudar nada" reproduz a oferta de hoje.
///
/// <para><b>Capacidade por semana</b> = <c>Σ(linha: dias acesos × atendimentos por turno) ×
/// Aproveitamento</c> + mutirões. <b>Turno</b> = um profissional num dia com escala para o
/// procedimento. Não há "horas" de propósito: a hora de início/fim da escala do SISREG não é tempo
/// de trabalho (a ultrassonografia tem blocos de 5 minutos com 125 vagas); o que a escala diz de
/// verdade é <i>quantos dias</i> e <i>quantas vagas por dia</i>.</para>
/// </summary>
public sealed record ParametrosEstrategia(
    string Objetivo,
    int? PrazoAlvoSemanas,
    IReadOnlyList<LinhaQuadro> Quadro,
    /// <summary>Unidades que só existem na simulação ("Unidade simulação", ou um nome dado pelo gestor).</summary>
    IReadOnlyList<string> UnidadesSimuladas,
    /// <summary>O agente pode acrescentar médicos de simulação?</summary>
    bool PermitirNovosProfissionais,
    int MaxNovosProfissionais,
    /// <summary>Fração das vagas ofertadas que viram atendimento (0..1). Medido no cenário.</summary>
    ParametroNumero Aproveitamento,
    /// <summary>Pessoas novas por semana.</summary>
    ParametroNumero EntradaSemanal,
    IReadOnlyList<MutiraoDto> Mutiroes,
    bool MutiroesTravados,
    int HorizonteSemanas)
{
    public const int HorizonteMaximo = 156;
    public const int HorizontePadrao = 104;
    public const int MaxNovosPadrao = 5;
    public const double AtendimentosPorTurnoPadraoSemOferta = 10;

    /// <summary>Turnos por semana na rede toda.</summary>
    public double TurnosSemanais() => Quadro?.Sum(l => l.Turnos) ?? 0;

    /// <summary>Vagas ofertadas por semana (antes do aproveitamento).</summary>
    public double VagasSemanais() => Quadro?.Sum(l => l.VagasSemana) ?? 0;

    /// <summary>Capacidade semanal sem mutirões.</summary>
    public double CapacidadeSemanal() => VagasSemanais() * Math.Clamp(Aproveitamento.Valor, 0, 1);

    /// <summary>Atendimentos por turno para um médico novo: a média do quadro, ou o padrão sem oferta.</summary>
    public double AtendimentosPorTurnoPadrao()
    {
        var comTurno = Quadro?.Where(l => l.Turnos > 0).ToList() ?? [];
        return comTurno.Count == 0
            ? AtendimentosPorTurnoPadraoSemOferta
            : Math.Round(comTurno.Sum(l => l.VagasSemana) / comTurno.Sum(l => l.Turnos), 2);
    }

    public IEnumerable<(string Nome, ParametroNumero Parametro)> Numericos()
    {
        yield return (nameof(Aproveitamento), Aproveitamento);
        yield return (nameof(EntradaSemanal), EntradaSemanal);
    }

    /// <summary>Um campo gravado antes de existir (JSON antigo) chega nulo; vira vazio/zero em vez
    /// de derrubar a tela — o operador vê e usa "voltar para hoje".</summary>
    public ParametrosEstrategia Sanear() => this with
    {
        Quadro = (Quadro ?? []).Select(l => l with
        {
            Dias = l.Dias ?? [],
            DiasReais = l.DiasReais ?? [],
            OutrasEscalas = l.OutrasEscalas ?? new Dictionary<int, string>(),
            Unidade = l.Unidade ?? string.Empty,
            Nome = l.Nome ?? string.Empty,
            Id = l.Id ?? Guid.NewGuid().ToString("N")[..12],
        }).ToList(),
        UnidadesSimuladas = UnidadesSimuladas ?? [],
        MaxNovosProfissionais = MaxNovosProfissionais <= 0 ? MaxNovosPadrao : MaxNovosProfissionais,
        Aproveitamento = Aproveitamento ?? new ParametroNumero(1),
        EntradaSemanal = EntradaSemanal ?? new ParametroNumero(0),
        Mutiroes = Mutiroes ?? [],
        Objetivo = Objetivo ?? ObjetivoEstrategia.ZerarEmSemanas,
    };
}

// ---------------------------------------------------------------------------------------------
// Cenário atual
// ---------------------------------------------------------------------------------------------

public sealed record FilaCenarioDto(
    int Total,
    IReadOnlyDictionary<string, int> PorRisco,
    int? EsperaMedianaDias,
    int? EsperaP90Dias,
    int? EsperaMaxDias,
    DateOnly? MaisAntigoEm,
    IReadOnlyList<DemandaFaixaEsperaDto> Faixas);

/// <summary>Um ponto da série semanal (segunda-feira da semana, em dia de Brasília).</summary>
public sealed record SemanaDto(DateOnly Semana, int Quantidade);

/// <summary>Ritmo de entrada ou de saída, medido em semanas cheias.</summary>
/// <param name="MediaSemanal12">Média das últimas 12 semanas.</param>
/// <param name="MediaSemanal26">Média das últimas 26 semanas.</param>
/// <param name="Tendencia">Razão média(4 últimas) ÷ média(12): &gt;1 acelerando, &lt;1 desacelerando.</param>
public sealed record RitmoDto(
    double MediaSemanal12,
    double MediaSemanal26,
    double? Tendencia,
    IReadOnlyList<SemanaDto> Serie);

public sealed record UnidadeOfertaDto(
    Guid UnidadeId,
    string Nome,
    string? Cnes,
    bool AgendaLocal,
    int Profissionais,
    int VagasRegulacaoSemana,
    int VagasTotalSemana);

/// <summary>Profissional na escala vigente. Sem CPF de propósito: o cenário vai ao modelo e à
/// tela; o <see cref="Id"/> é um hash estável do CPF.</summary>
/// <param name="OutrasEscalas">Dias (0..6) em que o profissional já tem escala de OUTRO
/// procedimento ou unidade, com o rótulo "UNIDADE · PROCEDIMENTO".</param>
public sealed record ProfissionalOfertaDto(
    string Id,
    string Nome,
    string? Cbo,
    Guid? UnidadeId,
    string Unidade,
    /// <summary>Dias da semana (0=domingo … 6=sábado) em que tem bloco.</summary>
    IReadOnlyList<int> Dias,
    int VagasRegulacaoSemana,
    /// <summary>Vagas de regulação por turno deste profissional (vagas ÷ turnos nas 4 semanas).</summary>
    double AtendimentosPorTurno,
    IReadOnlyDictionary<int, string> OutrasEscalas);

public sealed record OfertaCenarioDto(
    IReadOnlyList<UnidadeOfertaDto> Unidades,
    IReadOnlyList<ProfissionalOfertaDto> Profissionais,
    /// <summary>Dias da semana com oferta (0=domingo … 6=sábado).</summary>
    IReadOnlyList<int> DiasSemana,
    TimeOnly? HoraInicioTipica,
    TimeOnly? HoraFimTipica,
    int Blocos,
    int VagasPrimeiraVezSemana,
    int VagasRetornoSemana,
    int VagasReservaSemana,
    /// <summary>1ª vez + reserva: a vaga que a regulação usa (regra medida em 10/09/2026).</summary>
    int VagasRegulacaoSemana,
    int VagasTotalSemana,
    /// <summary>Turnos (profissional-dias) por semana na rede, para o procedimento.</summary>
    double TurnosSemana,
    /// <summary>Média de turnos por semana por profissional.</summary>
    double TurnosPorProfissionalSemana,
    /// <summary>Vagas de regulação por turno — derivado para a identidade
    /// <c>profissionais × turnos × atend/turno = vagas/semana</c> fechar.</summary>
    double AtendimentosPorTurno,
    /// <summary>Horas declaradas na escala por semana. <b>Só informação</b>: o SISREG cadastra
    /// blocos de minutos com dezenas de vagas; isto não é tempo de trabalho.</summary>
    double HorasDeclaradasSemana,
    /// <summary>Escalas de agenda local (que a regulação não vê) foram deixadas de fora da
    /// capacidade; quantas vagas/semana elas somam, só para informação.</summary>
    int VagasAgendaLocalSemana);

/// <summary>Quanto da oferta virou marcação nas últimas semanas.</summary>
public sealed record OcupacaoCenarioDto(
    int SemanasMedidas,
    int VagasRegulacaoOfertadas,
    int Agendados,
    /// <summary>Agendados ÷ ofertadas, 0..1 (limitado a 1). Nulo sem oferta no período.</summary>
    double? Aproveitamento);

public sealed record ProcedimentoCenarioDto(
    string? Codigo,
    string Nome,
    string? NomeCanonico,
    Guid? RegulacaoProcedimentoId,
    bool EhGrupo,
    /// <summary>Nomes que entraram na conta da fila (a família).</summary>
    IReadOnlyList<string> Familia);

/// <summary>O retrato do procedimento hoje — o ponto de partida de toda simulação.</summary>
public sealed record CenarioFilaDto(
    ProcedimentoCenarioDto Procedimento,
    FilaCenarioDto Fila,
    RitmoDto Entrada,
    RitmoDto Vazao,
    /// <summary>De quem saiu da fila desde a carga: fração que saiu sem agendar (cancelou/negado).</summary>
    double? SaidaSemAgendarFracao,
    OfertaCenarioDto Oferta,
    OcupacaoCenarioDto Ocupacao,
    AgendaCoberturaDto Cobertura,
    ParametrosEstrategia ParametrosIniciais,
    DateTime GeradoEm);

// ---------------------------------------------------------------------------------------------
// Projeção
// ---------------------------------------------------------------------------------------------

public sealed record PontoProjecaoDto(int Semana, double Fila, double Capacidade, double Atendidos, double Entrada);

/// <summary>O que a simulação diz. Números nossos — o modelo nunca os produz.</summary>
/// <param name="SemanaZera">Semana em que a fila chega a zero; nulo se não zera no horizonte.</param>
/// <param name="CapacidadeEquilibrio">Capacidade semanal mínima para a fila parar de crescer (= entrada).</param>
/// <param name="CapacidadeParaZerarNoPrazo">Capacidade semanal necessária para zerar em <c>PrazoAlvoSemanas</c>; nulo sem prazo.</param>
public sealed record ProjecaoDto(
    int FilaInicial,
    double CapacidadeSemanal,
    double VagasSemanais,
    double EntradaSemanal,
    bool Zera,
    int? SemanaZera,
    double FilaFinal,
    double CrescimentoSemanal,
    double CapacidadeEquilibrio,
    double? CapacidadeParaZerarNoPrazo,
    double AtendidosAteZerar,
    double PicoFila,
    int HorizonteSemanas,
    IReadOnlyList<PontoProjecaoDto> Serie);

// ---------------------------------------------------------------------------------------------
// Proposta do agente
// ---------------------------------------------------------------------------------------------

public sealed record AcaoPropostaDto(
    /// <summary>escala | profissional | mutirao | dia | horario | unidade | outro</summary>
    string Tipo,
    string Descricao,
    string? Unidade,
    double? ImpactoVagasSemana);

public sealed record PropostaAgenteDto(
    string Resumo,
    IReadOnlyList<AcaoPropostaDto> Acoes,
    IReadOnlyList<string> Riscos,
    /// <summary>0..1, declarada pelo agente.</summary>
    double Confianca,
    /// <summary>Quantas simulações o agente rodou antes de propor.</summary>
    int Simulacoes);

// ---------------------------------------------------------------------------------------------
// Estratégia e rodadas
// ---------------------------------------------------------------------------------------------

public sealed record RodadaResumoDto(
    Guid Id,
    int Numero,
    ModoRodadaEstrategia Modo,
    bool? Zera,
    int? SemanaZera,
    double CapacidadeSemanal,
    string? Falha,
    string? Modelo,
    decimal? CustoUsd,
    int DuracaoMs,
    DateTime CriadoEm,
    Guid? CriadoPor);

public sealed record RodadaDto(
    Guid Id,
    Guid EstrategiaId,
    int Numero,
    ModoRodadaEstrategia Modo,
    ParametrosEstrategia ParametrosEntrada,
    CenarioFilaDto Cenario,
    ParametrosEstrategia ParametrosResultado,
    ProjecaoDto Projecao,
    PropostaAgenteDto? Proposta,
    string? Modelo,
    long TokensEntrada,
    long TokensSaida,
    decimal? CustoUsd,
    int DuracaoMs,
    string? Falha,
    DateTime CriadoEm,
    Guid? CriadoPor);

public sealed record EstrategiaResumoDto(
    Guid Id,
    string Nome,
    string? ProcedimentoCodigo,
    string ProcedimentoNome,
    string? NomeCanonico,
    StatusEstrategiaFila Status,
    int Rodadas,
    RodadaResumoDto? RodadaAtual,
    DateTime? AplicadaEm,
    DateTime CriadoEm,
    DateTime? AtualizadoEm);

public sealed record EstrategiaDto(
    Guid Id,
    string Nome,
    string? ProcedimentoCodigo,
    string ProcedimentoNome,
    string? NomeCanonico,
    StatusEstrategiaFila Status,
    ParametrosEstrategia Parametros,
    RodadaDto? RodadaAtual,
    IReadOnlyList<RodadaResumoDto> Rodadas,
    DateTime? AplicadaEm,
    Guid? AplicadaPor,
    string? AplicacaoNota,
    DateTime CriadoEm,
    Guid? CriadoPor,
    DateTime? AtualizadoEm);

// ---------------------------------------------------------------------------------------------
// Requests
// ---------------------------------------------------------------------------------------------

public sealed record SimularRequest(string? ProcedimentoCodigo, string ProcedimentoNome, ParametrosEstrategia Parametros);

public sealed record SimularRespostaDto(CenarioFilaDto Cenario, ProjecaoDto Projecao);

/// <summary>Projeção pura, sem remontar o cenário — é o que a tela chama a cada clique no quadro.</summary>
public sealed record ProjetarRequest(ParametrosEstrategia Parametros, int FilaInicial);

public sealed record CriarEstrategiaRequest(
    string Nome,
    string? ProcedimentoCodigo,
    string ProcedimentoNome,
    ParametrosEstrategia Parametros);

public sealed record AtualizarEstrategiaRequest(string Nome, ParametrosEstrategia Parametros, StatusEstrategiaFila? Status);

public sealed record NovaRodadaRequest(ModoRodadaEstrategia Modo, ParametrosEstrategia Parametros);

public sealed record MarcarAplicadaRequest(string? Nota);

public sealed record EstrategiaFiltro(StatusEstrategiaFila? Status, string? ProcedimentoCodigo, string? ProcedimentoNome, bool IncluirArquivadas = false);
