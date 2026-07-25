namespace SMSMarica.Secretario.Api.Painel;

// DTOs do snapshot do painel — shape EXATO de docs/contrato-painel.json.
// PascalCase aqui; a serialização (JsonSerializerDefaults.Web) emite camelCase.
//
// O painel é MULTI-UNIDADE desde 25/07/2026: o snapshot carrega uma lista de
// UnidadePainel (geral / conde / upa) e o front escolhe qual mostrar. Cada unidade
// traz as MESMAS seções, e as que não existem naquela unidade vêm nulas — a UPA não
// tem internação nem maternidade (ver ConsultasUpa) e simplesmente não as envia.

public sealed record PainelSnapshot(
    DateTimeOffset GeradoEm,
    /// <summary>Situação consolidada: só está OK quando TODAS as fontes estão OK.</summary>
    StatusFonte Status,
    /// <summary>Uma entrada por base consultada (Salux/Oracle e UPA/SQL Server).</summary>
    IReadOnlyList<FonteInfo> Fontes,
    IReadOnlyList<UnidadePainel> Unidades);

public sealed record StatusFonte(
    bool Ok,
    string? UltimoErro,
    DateTimeOffset? UltimaAtualizacaoOk);

/// <summary>Procedência dos números de uma base, com o estado da última leitura.</summary>
public sealed record FonteInfo(
    /// <summary>"conde" | "upa" — casa com <see cref="UnidadePainel.Id"/>.</summary>
    string Id,
    string Nome,
    StatusFonte Status);

/// <summary>
/// Uma aba do painel. <c>geral</c> é a rede somada; <c>conde</c> e <c>upa</c> são as
/// unidades reais. As seções ausentes na unidade vêm nulas.
/// </summary>
public sealed record UnidadePainel(
    /// <summary>"geral" | "conde" | "upa".</summary>
    string Id,
    /// <summary>Rótulo curto do seletor ("Geral", "Conde", "UPA").</summary>
    string Rotulo,
    /// <summary>Nome por extenso, exibido no cabeçalho da aba.</summary>
    string Nome,
    /// <summary>Linha de procedência ("Salux HIS — Hospital…", "Rede municipal…").</summary>
    string Fonte,
    /// <summary>
    /// Cores de triagem que ESTA unidade usa. O Conde não usa laranja; a UPA usa.
    /// As demais chegam zeradas e o front as apaga em vez de fingir que valem 0.
    /// </summary>
    IReadOnlyList<string> CoresUsadas,
    AgoraSecao? Agora,
    AtendimentosSecao? Atendimentos,
    InternacoesSecao? Internacoes,
    EsperaPorCorSecao? EsperaPorCor,
    MaternidadeSecao? Maternidade);

// ── Agora (tick rápido) ─────────────────────────────────────────────────────

public sealed record AgoraSecao(
    DateTimeOffset AtualizadoEm,
    int AguardandoMedico,
    IReadOnlyList<CorAguardando> AguardandoPorCor,
    int EmAtendimento,
    int AtendimentosHoje,
    /// <summary>Nulo na UPA — a unidade não interna (ver ConsultasUpa §Internação).</summary>
    InternadosAgora? Internados);

public sealed record CorAguardando(string Cor, int Qtd, int? MinMedioEspera);

/// <summary>
/// Internados neste momento, nas três faixas exclusivas. Na aba "geral" o bloco vem
/// só do Conde e <see cref="Escopo"/> diz isso na tela — número de rede que na verdade
/// é de uma unidade só é como se mente sem mentir.
/// </summary>
public sealed record InternadosAgora(
    int Total,
    int Maternidade,
    int Ate17,
    int Adultos,
    double? MediaDiasInternacao,
    int InternacoesHoje,
    string? Escopo);

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
    IReadOnlyList<DiaQtdInternacao> SerieDiaria,
    /// <summary>Preenchido na aba "geral": diz que o número é só do Conde.</summary>
    string? Escopo);

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
    IReadOnlyList<DiaPartos> SerieDiaria,
    /// <summary>Preenchido na aba "geral": a única maternidade da rede é a do Conde.</summary>
    string? Escopo);

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

// ── Espera por cor (tick lento — Q6/U6) ─────────────────────────────────────

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
