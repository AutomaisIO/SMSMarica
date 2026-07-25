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
    MaternidadeSecao? Maternidade,
    LeitosSecao? Leitos,
    DiagnosticosSecao? Diagnosticos);

// ── Leitos e internação (tick lento) ────────────────────────────────────────

/// <summary>
/// A segunda visão do painel. O Conde tem internação de verdade — 37 setores, perfil
/// dos internados e permanência das altas. As UPAs têm leitos de OBSERVAÇÃO, que é
/// outra coisa e aparece como tal.
/// </summary>
public sealed record LeitosSecao(
    DateTimeOffset AtualizadoEm,
    /// <summary>Nulo quando a unidade não tem cadastro de leito confiável — ver <see cref="Indisponivel"/>.</summary>
    Ocupacao? Ocupacao,
    IReadOnlyList<SetorOcupacao> Setores,
    /// <summary>Quem está internado agora (só onde há internação).</summary>
    PerfilInternados? Perfil,
    /// <summary>Permanência das altas do período (só onde há internação).</summary>
    Permanencia? Permanencia,
    /// <summary>Fluxo de encaminhamento à observação (só nas UPAs).</summary>
    ObservacaoFluxo? Observacao,
    string? Escopo,
    /// <summary>
    /// Motivo de não haver ocupação, para a tela dizer o que falta em vez de mostrar
    /// zero. Preenchido em Santa Rita, cujo cadastro de leitos está vazio e parado.
    /// </summary>
    string? Indisponivel);

/// <summary>
/// Ocupação do momento. O numerador são PACIENTES reais (internação sem alta), não o
/// flag <c>ID_SIT_LEITO</c> do leito — assim o número casa com "internados agora" do
/// resto do painel, em vez de divergir dele por alguns leitos (162 × 155 em 25/07).
/// O denominador exclui leitos bloqueados, que não são capacidade operacional, mas
/// eles vêm reportados à parte porque leito interditado é informação de gestão.
/// </summary>
public sealed record Ocupacao(
    int Leitos, int Ocupados, int Livres, int Bloqueados, double? Taxa);

public sealed record SetorOcupacao(string Setor, int Leitos, int Ocupados, int Bloqueados, double? Taxa);

public sealed record PerfilInternados(
    int Total, int Homens, int Mulheres, int SemSexo,
    int Ate17, int Adultos, int Idosos,
    double? IdadeMedia, double? DiasMedios);

public sealed record Permanencia(
    string Rotulo, int Altas,
    double? MediaDias, double? MedianaDias, double? P90Dias,
    IReadOnlyList<PermanenciaSegmento> Segmentos);

public sealed record PermanenciaSegmento(string Segmento, int Altas, double? MediaDias);

public sealed record ObservacaoFluxo(string Rotulo, int Encaminhados, int Classificados);

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
    double? PctCesarea,
    /// <summary>Nascidos mortos (<c>ID_CONDICAO_NASCIMENTO = 'M'</c>) — 100% preenchido.</summary>
    int Natimortos,
    int ComMalformacao,
    int MalformacaoSemInfo,
    /// <summary>
    /// Idade gestacional pela codificação do SINASC em <c>ID_TMP_GESTACAO</c>: 5 = 37–41
    /// semanas (a termo), 4 = 32–36 (prematuro tardio), 6 = 42+ (pós-termo). Decodificação
    /// conferida contra o peso médio (2,5 kg no 4 × 3,37 kg no 5) e contra <c>IN_PREMATURO</c>.
    /// </summary>
    int ATermo,
    int PrematuroTardio,
    int PosTermo,
    int GestacaoSemInfo,
    int GravidezUnica,
    int GravidezMultipla,
    int Apgar1Abaixo7,
    double? EstaturaMedia,
    double? PerimetroCefalicoMedio,
    /// <summary>Idade da mãe, pela FIA do parto — ligação de 100% dos nascimentos.</summary>
    double? IdadeMediaMae,
    int MaeAte17,
    int MaeMenor20,
    int Mae35Mais);

public sealed record DiaPartos(string Dia, int Qtd, int? Cesareas);

// ── Diagnósticos por cor (tick lento — Q8) ──────────────────────────────────

/// <summary>
/// Os CIDs mais registrados em cada cor da triagem. Só o Conde tem — nas UPAs o CID da
/// classificação não é preenchido (ver ConsultasUpa).
/// </summary>
public sealed record DiagnosticosSecao(
    DateTimeOffset AtualizadoEm,
    string Rotulo,
    IReadOnlyList<DiagnosticosDaCor> PorCor,
    string? Escopo);

public sealed record DiagnosticosDaCor(
    string Cor,
    int Boletins,
    IReadOnlyList<CidRanking> Cids);

public sealed record CidRanking(string Codigo, string Descricao, int Qtd, double? Pct);

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
