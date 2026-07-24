namespace SMSMarica.Secretario.Api.Painel;

// DTOs do snapshot do painel — shape EXATO de docs/contrato-painel.json.
// PascalCase aqui; a serialização (JsonSerializerDefaults.Web) emite camelCase.
// Seções são anuláveis: antes da primeira carga de cada tick a seção ainda não existe
// (em regime, o snapshot persistido garante que tudo chega preenchido).

public sealed record PainelSnapshot(
    DateTimeOffset GeradoEm,
    string Fonte,
    OracleStatus Oracle,
    AgoraSecao? Agora,
    AtendimentosSecao? Atendimentos,
    InternacoesSecao? Internacoes,
    EsperaPorCorSecao? EsperaPorCor,
    MaternidadeSecao? Maternidade);

public sealed record OracleStatus(
    bool Ok,
    string? UltimoErro,
    DateTimeOffset? UltimaAtualizacaoOk);

// ── Agora (tick rápido) ─────────────────────────────────────────────────────

public sealed record AgoraSecao(
    DateTimeOffset AtualizadoEm,
    int AguardandoMedico,
    IReadOnlyList<CorAguardando> AguardandoPorCor,
    int EmAtendimento,
    int InternadosAgora,
    int InternadosMaternidade,
    int InternadosAte17,
    int InternadosAdultos,
    double? MediaDiasInternacao,
    int AtendimentosHoje,
    int InternacoesHoje);

public sealed record CorAguardando(string Cor, int Qtd, int? MinMedioEspera);

// ── Atendimentos (tick lento) ───────────────────────────────────────────────

public sealed record AtendimentosSecao(
    DateTimeOffset AtualizadoEm,
    AtendimentosMesAnterior MesAnterior,
    AtendimentosMesAtual MesAtual,
    TotalSimples Hoje,
    IReadOnlyList<DiaQtd> SerieDiaria,
    IReadOnlyList<HoraQtd> PorHoraHoje);

public sealed record AtendimentosMesAnterior(string Rotulo, int Total, int Dias, double? MediaDiaria);

public sealed record AtendimentosMesAtual(
    string Rotulo, int Total, int DiasCompletos, int TotalDiasCompletos, double? MediaDiaria);

public sealed record TotalSimples(int Total);

public sealed record DiaQtd(string Dia, int Qtd);

public sealed record HoraQtd(int Hora, int Qtd);

// ── Internações (tick lento) ────────────────────────────────────────────────

public sealed record InternacoesSecao(
    DateTimeOffset AtualizadoEm,
    InternacoesMes MesAnterior,
    InternacoesMes MesAtual,
    InternacoesHoje Hoje,
    IReadOnlyList<DiaQtdInternacao> SerieDiaria);

/// <summary>
/// Ponto da série diária de internações — só ela tem o split em faixas (contrato).
/// As três faixas são exclusivas: maternidade → até 17 anos → adultos.
/// </summary>
public sealed record DiaQtdInternacao(string Dia, int Qtd, int? Maternidade, int? Ate17, int? Adultos);

public sealed record InternacoesMes(
    string Rotulo, int Total, int Maternidade, int Ate17, int Adultos, double? MediaDiaria);

public sealed record InternacoesHoje(int Total, int Maternidade, int Ate17, int Adultos);

// ── Maternidade (tick lento — Q7) ───────────────────────────────────────────

public sealed record MaternidadeSecao(
    DateTimeOffset AtualizadoEm,
    MaternidadePeriodo MesAnterior,
    MaternidadePeriodo MesAtual,
    MaternidadePeriodo Hoje,
    IReadOnlyList<DiaPartos> SerieDiaria);

public sealed record MaternidadePeriodo(
    string Rotulo,
    int Partos,
    int Cesareas,
    int Vaginais,
    int Prematuros,
    int BaixoPeso,
    double? PesoMedioKg,
    int Apgar5Abaixo7,
    int Meninas,
    int Meninos,
    double? MediaDiaria,
    /// <summary>Fração de cesáreas (0–100) — null quando não houve parto no período.</summary>
    double? PctCesarea);

public sealed record DiaPartos(string Dia, int Qtd, int? Cesareas);

// ── Espera por cor (tick lento — Q6) ────────────────────────────────────────

public sealed record EsperaPorCorSecao(
    DateTimeOffset AtualizadoEm,
    EsperaPeriodos Periodos);

public sealed record EsperaPeriodos(
    IReadOnlyList<EsperaCor> Hoje,
    IReadOnlyList<EsperaCor> MesAtual,
    IReadOnlyList<EsperaCor> MesAnterior);

public sealed record EsperaCor(
    string Cor,
    int Pacientes,
    int ComAtendimento,
    double? MediaAteTriagem,
    double? MediaEspera,
    double? MedianaEspera,
    double? P90Espera,
    int? MetaMin,
    double? PctNaMeta);
