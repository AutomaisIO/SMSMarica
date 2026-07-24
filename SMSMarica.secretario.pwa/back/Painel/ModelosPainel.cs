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
    EsperaPorCorSecao? EsperaPorCor);

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
    int InternadosUrgencia,
    int InternadosEletiva,
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

/// <summary>Ponto da série diária de internações — só ela tem o split urgência/eletiva (contrato).</summary>
public sealed record DiaQtdInternacao(string Dia, int Qtd, int? Urgencia, int? Eletiva);

public sealed record InternacoesMes(string Rotulo, int Total, int Urgencia, int Eletiva, double? MediaDiaria);

public sealed record InternacoesHoje(int Total, int Urgencia, int Eletiva);

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
