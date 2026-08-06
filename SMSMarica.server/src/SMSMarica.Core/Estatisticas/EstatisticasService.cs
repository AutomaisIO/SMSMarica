using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Common.Unidades;
using SMSMarica.Core.Estatisticas.Dtos;
using SMSMarica.Core.Identidade;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Estatisticas;

/// <summary>
/// Agrega o histórico de mensagens WhatsApp (<c>whatsapp_mensagem</c>) e conversas em números
/// gerenciais. Usa SQL agregado direto na conexão do contexto (nunca materializa linhas) porque o
/// banco é compartilhado com outros produtos e as contagens varreriam a tabela à toa via EF.
///
/// Os agregados de EXAMES DE IMAGEM seguem outra estratégia: materializam uma projeção enxuta do
/// conjunto (recorte por categoria Imagem + período + escopo de unidade) e calculam em memória —
/// o volume por período é pequeno (centenas/poucos milhares), e assim se evita a mistura de fuso
/// entre <c>DataEstudo</c> (wall-clock local) e os timestamps UTC no SQL.
/// </summary>
public sealed class EstatisticasService(
    SmsMaricaDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    Pacientes.Fhir.IPacienteResolver pacienteResolver,
    ILogger<EstatisticasService> logger) : IEstatisticasService
{
    private const int MaxDiasPeriodo = 400;

    // Discriminadores (ver ADR/entidades): direcao 1=Saida 2=Entrada; template preenchido = HSM;
    // autor_usuario_id nulo = envio automático do sistema, preenchido = operador na Central.
    // status: 1 Enviada, 2 Entregue, 3 Lida, 4 Falha, 5 Recebida.
    private const string FiltroPeriodoSimulado = """
        m.ocorrido_em::date BETWEEN @de AND @ate
        AND (m.conteudo IS NULL OR m.conteudo NOT LIKE '[SIMULADO]%')
        AND (m.wa_message_id IS NULL OR m.wa_message_id NOT LIKE 'simulado-%')
        """;

    public async Task<EstatisticasWhatsAppDto> ObterWhatsAppAsync(
        DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        if (ate < de) (de, ate) = (ate, de);
        if (ate.DayNumber - de.DayNumber + 1 > MaxDiasPeriodo)
            throw new ValidacaoException("periodo", $"O período não pode exceder {MaxDiasPeriodo} dias.");

        var conn = db.Database.GetDbConnection();
        var abriuAqui = conn.State != ConnectionState.Open;
        if (abriuAqui) await conn.OpenAsync(ct);
        try
        {
            var resumoBruto = await LerResumoAsync(conn, de, ate, ct);
            var porDia = await LerPorDiaAsync(conn, de, ate, ct);
            var porTemplate = await LerRotuloAsync(conn,
                $"SELECT COALESCE(NULLIF(m.template,''),'(sem template)') AS r, count(*) AS c " +
                $"FROM smsmarica.whatsapp_mensagem m WHERE {FiltroPeriodoSimulado} " +
                "AND m.template IS NOT NULL AND m.template <> '' GROUP BY 1 ORDER BY 2 DESC LIMIT 12",
                de, ate, ct);
            var porStatus = await LerStatusAsync(conn, de, ate, ct);
            var porAtendente = await LerRotuloAsync(conn,
                "SELECT COALESCE(NULLIF(m.autor_nome_exibicao,''),'(sem nome)') AS r, count(*) AS c " +
                $"FROM smsmarica.whatsapp_mensagem m WHERE {FiltroPeriodoSimulado} " +
                "AND m.autor_usuario_id IS NOT NULL GROUP BY 1 ORDER BY 2 DESC LIMIT 12",
                de, ate, ct);
            var conversasNovas = await LerEscalarAsync(conn,
                "SELECT count(*) FROM smsmarica.conversa c " +
                "WHERE c.excluido_em IS NULL AND c.criado_em::date BETWEEN @de AND @ate",
                de, ate, ct);

            var dias = ate.DayNumber - de.DayNumber + 1;
            var total = resumoBruto.Enviadas + resumoBruto.Recebidas;
            var mensagensSessao = total - resumoBruto.TemplatesSistema - resumoBruto.TemplatesAtendente;

            var resumo = new EstatisticasResumoDto(
                TotalMensagens: total,
                Enviadas: resumoBruto.Enviadas,
                Recebidas: resumoBruto.Recebidas,
                TemplatesSistema: resumoBruto.TemplatesSistema,
                TemplatesAtendente: resumoBruto.TemplatesAtendente,
                MensagensSessao: mensagensSessao,
                ConversasNovas: conversasNovas,
                DiasNoPeriodo: dias,
                MediaDiaria: dias > 0 ? Math.Round((double)total / dias, 1) : 0,
                TaxaEntrega: resumoBruto.Enviadas > 0
                    ? Math.Round(100.0 * resumoBruto.Entregues / resumoBruto.Enviadas, 1) : 0,
                TaxaLeitura: resumoBruto.Enviadas > 0
                    ? Math.Round(100.0 * resumoBruto.Lidas / resumoBruto.Enviadas, 1) : 0,
                Atendentes: resumoBruto.Atendentes);

            var porCategoria = new List<RotuloContagemDto>
            {
                new("Template (sistema)", resumoBruto.TemplatesSistema),
                new("Template (atendente)", resumoBruto.TemplatesAtendente),
                new("Atendente (sessão)", resumoBruto.TextoAtendente),
                new("Automática (sessão)", resumoBruto.TextoSistema),
                new("Recebidas", resumoBruto.Recebidas),
            };

            return new EstatisticasWhatsAppDto(de, ate, resumo, porDia, porCategoria,
                porTemplate, porStatus, porAtendente);
        }
        finally
        {
            if (abriuAqui) await conn.CloseAsync();
        }
    }

    // ===================== EXAMES DE IMAGEM =====================

    public async Task<EstatisticasExamesImagemDto> ObterExamesImagemAsync(
        DateOnly de, DateOnly ate, Guid? unidadeId, ModalidadeDicom? modalidade, Guid? tipoExameId,
        CancellationToken ct = default)
    {
        if (ate < de) (de, ate) = (ate, de);
        if (ate.DayNumber - de.DayNumber + 1 > MaxDiasPeriodo)
            throw new ValidacaoException("periodo", $"O período não pode exceder {MaxDiasPeriodo} dias.");

        var recorte = await CarregarExamesAsync(de, ate, unidadeId, modalidade, tipoExameId, ct);
        var exames = recorte.Exames;
        var laudoPorExame = recorte.LaudoPorExame;
        long laudosFinalizados = recorte.Laudos.Count;

        var dias = ate.DayNumber - de.DayNumber + 1;
        long total = exames.Count;
        long realizados = exames.Count(e => e.Realizado);
        long laudados = exames.Count(e => laudoPorExame.ContainsKey(e.Id));
        long cancelados = exames.Count(e => e.Status == StatusSolicitacaoExame.Cancelada);
        long aguardandoLaudo = Math.Max(0, realizados - laudados);
        int medicosLaudando = laudoPorExame.Values
            .Select(l => l.MedicoId)
            .Distinct().Count();

        // Assinatura sobre o laudo VIGENTE de cada exame — nunca sobre todas as versões. Uma
        // retificação deixa a versão anterior para trás por design: cobrá-la de assinatura contaria
        // como pendente um trabalho que já foi substituído, e a fila viraria ficção.
        long assinados = laudoPorExame.Values.Count(l => l.Assinado);
        long aguardandoAssinatura = Math.Max(0, laudados - assinados);
        long laudosAssinados = recorte.Laudos.Count(l => l.Assinado);

        // Tempos médios (horas) por trecho — só timestamps UTC, pares válidos e monotônicos.
        var chegExec = new List<double>();
        var execLaudo = new List<double>();
        var laudoAssin = new List<double>();
        var totalCiclo = new List<double>();
        foreach (var e in exames)
        {
            var l = laudoPorExame.GetValueOrDefault(e.Id);
            DateTime? fin = l?.FinalizadoEm;
            if (e.AutorizadoEm is { } a1 && e.RealizadoEm is { } r1 && r1 >= a1)
                chegExec.Add((r1 - a1).TotalHours);
            if (e.RealizadoEm is { } r2 && fin is { } f2 && f2 >= r2)
                execLaudo.Add((f2 - r2).TotalHours);
            if (fin is { } f4 && l?.AssinadoEm is { } s4 && s4 >= f4)
                laudoAssin.Add((s4 - f4).TotalHours);
            // Ciclo total continua sendo chegada → LAUDO (não até a assinatura): é o número que o
            // painel já publica sob esse nome, e reancorá-lo em silêncio quebraria a comparação com
            // os períodos anteriores. A etapa da assinatura tem métrica própria, acima.
            if (e.AutorizadoEm is { } a3 && fin is { } f3 && f3 >= a3)
                totalCiclo.Add((f3 - a3).TotalHours);
        }

        var resumo = new ExamesImagemResumoDto(
            TotalExames: total,
            Realizados: realizados,
            Laudados: laudados,
            AguardandoLaudo: aguardandoLaudo,
            LaudosEmitidos: laudosFinalizados,
            LaudosAssinados: laudosAssinados,
            Assinados: assinados,
            AguardandoAssinatura: aguardandoAssinatura,
            Cancelados: cancelados,
            MedicosLaudando: medicosLaudando,
            DiasNoPeriodo: dias,
            MediaExamesDia: dias > 0 ? Math.Round((double)total / dias, 1) : 0,
            PercentualLaudados: realizados > 0 ? Math.Round(100.0 * laudados / realizados, 1) : 0,
            PercentualAssinados: laudados > 0 ? Math.Round(100.0 * assinados / laudados, 1) : 0,
            TempoMedioChegadaExecucaoHoras: Media(chegExec),
            TempoMedioExecucaoLaudoHoras: Media(execLaudo),
            TempoMedioLaudoAssinaturaHoras: Media(laudoAssin),
            TempoMedioTotalHoras: Media(totalCiclo),
            AmostraChegadaExecucao: chegExec.Count,
            AmostraExecucaoLaudo: execLaudo.Count,
            AmostraLaudoAssinatura: laudoAssin.Count,
            AmostraTotal: totalCiclo.Count);

        var porDia = exames
            .GroupBy(e => DateOnly.FromDateTime(e.DataRef))
            .Select(g => new SerieExamesDiaDto(
                g.Key, g.LongCount(), g.LongCount(e => e.Realizado),
                g.LongCount(e => laudoPorExame.ContainsKey(e.Id))))
            .OrderBy(s => s.Dia)
            .ToList();

        var porModalidade = exames
            .GroupBy(e => e.Modalidade)
            .Select(g => new RotuloContagemDto(RotuloModalidade(g.Key), g.LongCount()))
            .OrderByDescending(r => r.Total).ToList();

        var porUnidade = exames
            .GroupBy(e => e.UnidadeExecutante ?? "(sem unidade)")
            .Select(g => new RotuloContagemDto(g.Key, g.LongCount()))
            .OrderByDescending(r => r.Total).Take(15).ToList();

        var porStatus = exames
            .GroupBy(e => e.Status)
            .Select(g => new RotuloContagemDto(RotuloStatusExame(g.Key), g.LongCount()))
            .OrderByDescending(r => r.Total).ToList();

        var porTipo = exames
            .GroupBy(e => e.TipoExameNome ?? "(sem tipo)")
            .Select(g => new RotuloContagemDto(g.Key, g.LongCount()))
            .OrderByDescending(r => r.Total).Take(15).ToList();

        // Produção por médico. As três contagens medem coisas diferentes de propósito:
        // ExamesLaudados é cobertura (exame que saiu da fila), LaudosEmitidos é o trabalho de fato
        // (retificação é laudo escrito de novo) e Assinados é o que virou documento válido. Um
        // médico com muitos emitidos e poucos exames laudados está retrabalhando.
        var porMedico = recorte.Laudos
            .GroupBy(l => l.MedicoId)
            .Select(g =>
            {
                var refer = g.OrderByDescending(l => l.FinalizadoEm).First();
                var exames = laudoPorExame.Values.Count(l => l.MedicoId == g.Key);
                return new ProducaoMedicoDto(
                    refer.MedicoNome ?? "(sem nome)", refer.MedicoCrm,
                    ExamesLaudados: exames,
                    LaudosEmitidos: g.LongCount(),
                    LaudosAssinados: g.LongCount(l => l.Assinado));
            })
            .OrderByDescending(m => m.LaudosEmitidos).Take(15).ToList();

        return new EstatisticasExamesImagemDto(
            de, ate, unidadeId, resumo, porDia, porModalidade, porUnidade, porStatus, porTipo, porMedico);
    }

    public async Task<ExportacaoImagemDto> ObterExportacaoImagemAsync(
        DateOnly de, DateOnly ate, Guid? unidadeId, ModalidadeDicom? modalidade, Guid? tipoExameId,
        ConteudoExportacaoImagem conteudo, CancellationToken ct = default)
    {
        if (ate < de) (de, ate) = (ate, de);
        if (ate.DayNumber - de.DayNumber + 1 > MaxDiasPeriodo)
            throw new ValidacaoException("periodo", $"O período não pode exceder {MaxDiasPeriodo} dias.");

        var recorte = await CarregarExamesAsync(de, ate, unidadeId, modalidade, tipoExameId, ct);
        var exames = recorte.Exames;
        var laudoPorExame = recorte.LaudoPorExame;

        var linhasExame = new List<ExameImagemAnaliticoDto>();
        var linhasLaudo = new List<LaudoAnaliticoDto>();

        // Visão EXAME (uma por exame) — para "Exames" e "Exames e Laudos".
        if (conteudo is ConteudoExportacaoImagem.Exames or ConteudoExportacaoImagem.ExamesLaudos)
        {
            foreach (var e in exames.OrderBy(x => x.DataRef))
            {
                DateTime? fin = laudoPorExame.TryGetValue(e.Id, out var laudo) ? laudo.FinalizadoEm : null;
                linhasExame.Add(new ExameImagemAnaliticoDto(
                    e.CodigoSolicitacao, e.AccessionNumber, e.StudyInstanceUID,
                    RotuloModalidade(e.Modalidade), e.TipoExameNome,
                    e.UnidadeExecutante, e.UnidadeSolicitante, RotuloStatusExame(e.Status),
                    e.DataSolicitacao, e.AutorizadoEm, e.DataEstudo, e.RealizadoEm,
                    fin, laudo?.MedicoNome, laudo?.MedicoCrm,
                    Horas(e.AutorizadoEm, e.RealizadoEm), Horas(e.RealizadoEm, fin), Horas(e.AutorizadoEm, fin)));
            }
        }

        // Visão LAUDO (uma por laudo finalizado, todas as versões) — para "Laudos".
        if (conteudo is ConteudoExportacaoImagem.Laudos)
        {
            foreach (var l in recorte.Laudos.OrderBy(x => x.FinalizadoEm))
            {
                recorte.ExamePorUid.TryGetValue(l.StudyInstanceUID, out var e);
                linhasLaudo.Add(new LaudoAnaliticoDto(
                    e?.CodigoSolicitacao, e?.AccessionNumber ?? "", l.StudyInstanceUID, l.Versao,
                    RotuloModalidade(e?.Modalidade), e?.TipoExameNome, e?.UnidadeExecutante,
                    e?.DataEstudo, e?.RealizadoEm, l.FinalizadoEm, l.MedicoNome, l.MedicoCrm,
                    Horas(e?.RealizadoEm, l.FinalizadoEm),
                    l.AssinadoEm, Horas(l.FinalizadoEm, l.AssinadoEm)));
            }
        }

        // Trilha de auditoria: quem exportou, o quê e quantas linhas.
        logger.LogInformation(
            "Auditoria: exportação analítica de imagem ({Conteudo}) por usuário {UsuarioId} — período {De}..{Ate}, unidade {Unidade}, {Exames} exame(s)/{Laudos} laudo(s).",
            conteudo, usuarioAtual.UsuarioId, de, ate, unidadeId, linhasExame.Count, linhasLaudo.Count);

        return new ExportacaoImagemDto(linhasExame, linhasLaudo);
    }

    public async Task<IReadOnlyList<ExameFaturamentoDto>> ObterFaturamentoImagemAsync(
        DateOnly de, DateOnly ate, Guid? unidadeId, ModalidadeDicom? modalidade, Guid? tipoExameId,
        CancellationToken ct = default)
    {
        if (ate < de) (de, ate) = (ate, de);
        if (ate.DayNumber - de.DayNumber + 1 > MaxDiasPeriodo)
            throw new ValidacaoException("periodo", $"O período não pode exceder {MaxDiasPeriodo} dias.");

        var recorte = await CarregarExamesAsync(de, ate, unidadeId, modalidade, tipoExameId, ct);

        // Faturamento é sobre o que foi REALIZADO: descarta pendentes/agendados (sem data de
        // realização), que só virariam ruído/linha inválida na planilha de faturamento.
        var exames = recorte.Exames.Where(e => e.Realizado).ToList();

        // Resolve a PII do paciente no hub FHIR em lote (nome/CPF/CNS/nascimento/CEP/celular). O hub
        // indisponível degrada por paciente (linha sai só com o que houver), nunca derruba a exportação.
        var pacientes = await pacienteResolver.ResolverManyAsync(exames.Select(e => e.PacienteId), ct);

        // Da menor para a maior data de realização.
        var linhas = exames
            .OrderBy(e => e.RealizadoEm ?? e.DataRef)
            .Select(e =>
            {
                pacientes.TryGetValue(e.PacienteId, out var p);
                return new ExameFaturamentoDto(
                    p?.Nome ?? "(sem nome)", p?.Cpf, p?.Cns, p?.DataNascimento, p?.Cep, p?.Logradouro,
                    p?.Celular, e.TipoExameNome, e.RealizadoEm);
            })
            .ToList();

        logger.LogInformation(
            "Auditoria: exportação de FATURAMENTO (com PII) de imagem por usuário {UsuarioId} — período {De}..{Ate}, unidade {Unidade}, {Linhas} linha(s).",
            usuarioAtual.UsuarioId, de, ate, unidadeId, linhas.Count);

        return linhas;
    }

    /// <summary>Duração em horas entre dois instantes UTC (null se algum falta ou for regressivo).</summary>
    private static double? Horas(DateTime? inicio, DateTime? fim) =>
        inicio is { } i && fim is { } f && f >= i ? Math.Round((f - i).TotalHours, 1) : null;

    /// <summary>
    /// Recorte materializado do período: os exames, os laudos que incidem sobre eles e os dois
    /// índices que ligam um ao outro.
    /// </summary>
    private sealed record RecorteImagem(
        List<ExameLinha> Exames,
        /// <summary>Todas as versões finalizadas que incidem sobre os exames do recorte.</summary>
        List<LaudoRaw> Laudos,
        /// <summary>Exame → laudo VIGENTE (última versão). Ausente = exame sem laudo.</summary>
        Dictionary<Guid, LaudoRaw> LaudoPorExame,
        /// <summary>Qualquer UID do exame (próprio ou conciliado) → o exame.</summary>
        Dictionary<string, ExameLinha> ExamePorUid);

    /// <summary>Projeção enxuta de um exame de imagem para os agregados/exportação.</summary>
    private sealed record ExameLinha(
        Guid Id, string? CodigoSolicitacao, string AccessionNumber, string StudyInstanceUID,
        Guid PacienteId, StatusSolicitacaoExame Status, ModalidadeDicom? Modalidade, string? TipoExameNome,
        string? UnidadeExecutante, string? UnidadeSolicitante, DateTime? AutorizadoEm, DateTime? RealizadoEm,
        DateTime? DataEstudo, DateTime? DataAgendada, DateOnly? DataSolicitacao, DateTime CriadoEm)
    {
        /// <summary>Executado: detectado no PACS ou já em status Realizada/Laudada.</summary>
        public bool Realizado => RealizadoEm != null
            || Status == StatusSolicitacaoExame.Realizada || Status == StatusSolicitacaoExame.Laudada;

        /// <summary>Âncora do período/série (UTC): quando aconteceu, senão o previsto, senão o registro.</summary>
        public DateTime DataRef => RealizadoEm ?? DataAgendada ?? CriadoEm;
    }

    /// <param name="AssinadoEm">
    /// Instante da assinatura ICP-Brasil CONCLUÍDA (PAdES). Null = laudo emitido mas ainda não
    /// assinado — juridicamente o laudo só vale assinado, então este campo é o que separa
    /// "escreveu" de "entregou".
    /// </param>
    private sealed record LaudoRaw(
        Guid Id, string StudyInstanceUID, int Versao, DateTime? FinalizadoEm,
        Guid MedicoId, string? MedicoNome, string? MedicoCrm, DateTime? AssinadoEm)
    {
        public bool Assinado => AssinadoEm != null;
    }

    private async Task<RecorteImagem>
        CarregarExamesAsync(DateOnly de, DateOnly ate, Guid? unidadeId,
            ModalidadeDicom? modalidade, Guid? tipoExameId, CancellationToken ct)
    {
        var deUtc = new DateTime(de.Year, de.Month, de.Day, 0, 0, 0, DateTimeKind.Utc);
        var ateUtc = new DateTime(ate.Year, ate.Month, ate.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);

        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
        if (escopo.SemAcesso)
            return new RecorteImagem([], [], [], new(StringComparer.Ordinal));

        var q = db.ExamesImagem.AsNoTracking()
            .Where(e => e.ExcluidoEm == null
                && e.Solicitacao != null
                && e.Solicitacao.ExcluidoEm == null
                && e.Solicitacao.Categoria == CategoriaSolicitacao.Imagem
                && (e.RealizadoEm ?? e.Solicitacao.DataAgendada ?? e.CriadoEm) >= deUtc
                && (e.RealizadoEm ?? e.Solicitacao.DataAgendada ?? e.CriadoEm) < ateUtc);

        if (!escopo.VeTudo)
        {
            var unidades = escopo.Unidades;
            q = q.Where(e => unidades.Contains(e.Solicitacao!.UnidadeExecutanteId)
                || (e.Solicitacao!.UnidadeSolicitanteId != null
                    && unidades.Contains(e.Solicitacao!.UnidadeSolicitanteId.Value)));
        }

        if (unidadeId is { } uid)
            q = q.Where(e => e.Solicitacao!.UnidadeExecutanteId == uid);

        if (modalidade is { } mod)
            q = q.Where(e => e.TipoExame != null && e.TipoExame.ModalidadeDicom == mod);

        if (tipoExameId is { } tipo)
            q = q.Where(e => e.TipoExameId == tipo);

        var exames = await q.Select(e => new ExameLinha(
                e.Id, e.Solicitacao!.CodigoSolicitacao, e.AccessionNumber, e.StudyInstanceUID,
                e.Solicitacao!.PacienteId, e.Status,
                e.TipoExame != null ? e.TipoExame.ModalidadeDicom : (ModalidadeDicom?)null,
                e.TipoExame != null ? e.TipoExame.Nome : null,
                e.Solicitacao!.UnidadeExecutante != null ? e.Solicitacao!.UnidadeExecutante.Nome : null,
                e.Solicitacao!.UnidadeSolicitante != null ? e.Solicitacao!.UnidadeSolicitante.Nome : null,
                e.Solicitacao!.AutorizadoEm, e.RealizadoEm, e.DataEstudo,
                e.Solicitacao!.DataAgendada, e.Solicitacao!.DataSolicitacao, e.CriadoEm))
            .ToListAsync(ct);

        // Um exame pode ser conhecido por MAIS DE UM StudyInstanceUID: o que geramos ao publicar o
        // item na worklist (gravado em ExameImagem) e o REAL do equipamento, quando ele não honra o
        // da worklist e o estudo precisa ser conciliado depois (ExameAssociacao — ver a entidade).
        // O laudo é gravado sobre o UID que o médico abriu, então casar só pelo primeiro deixaria o
        // exame eternamente "aguardando laudo" mesmo já laudado. Aqui o exame vale por TODOS os
        // seus UIDs — é a mesma régua que LaudosService usa para decidir se um estudo tem vínculo.
        var idsExame = exames.Select(e => e.Id).ToArray();
        var conciliados = idsExame.Length == 0
            ? []
            : await db.ExameAssociacoes.AsNoTracking()
                .Where(a => a.ExcluidoEm == null && idsExame.Contains(a.ExameImagemId))
                .Select(a => new { a.ExameImagemId, a.StudyInstanceUID })
                .ToListAsync(ct);

        var examePorUid = new Dictionary<string, ExameLinha>(StringComparer.Ordinal);
        foreach (var e in exames)
            if (!string.IsNullOrEmpty(e.StudyInstanceUID))
                examePorUid.TryAdd(e.StudyInstanceUID, e);

        var porId = exames.ToDictionary(e => e.Id);
        foreach (var a in conciliados)
            if (!string.IsNullOrEmpty(a.StudyInstanceUID) && porId.TryGetValue(a.ExameImagemId, out var e))
                examePorUid.TryAdd(a.StudyInstanceUID, e);

        var uids = examePorUid.Keys.ToArray();

        var laudosBrutos = uids.Length == 0
            ? []
            : await db.Laudos.AsNoTracking()
                .Where(l => !l.Excluido && l.Status == StatusLaudo.Finalizado && uids.Contains(l.StudyInstanceUID))
                .Select(l => new
                {
                    l.Id, l.StudyInstanceUID, l.Versao, l.FinalizadoEm,
                    l.MedicoId, l.MedicoNomeSnapshot, l.MedicoCrmSnapshot,
                })
                .ToListAsync(ct);

        // Assinatura CONCLUÍDA por laudo. Só o status Concluida vale: os intermediários (preparada,
        // aguardando aprovação do médico) são tentativa em curso, e Cancelada/Falhou não assinam
        // nada — contá-los inflaria a produção com trabalho que ainda não saiu.
        var idsLaudo = laudosBrutos.Select(l => l.Id).ToArray();
        var assinadoEmPorLaudo = idsLaudo.Length == 0
            ? []
            : await db.LaudoAssinaturas.AsNoTracking()
                .Where(a => idsLaudo.Contains(a.LaudoId) && a.Status == StatusAssinatura.Concluida)
                .GroupBy(a => a.LaudoId)
                .Select(g => new { LaudoId = g.Key, AssinadoEm = g.Max(a => a.AssinadoEm) })
                .ToDictionaryAsync(x => x.LaudoId, x => x.AssinadoEm, ct);

        var laudos = laudosBrutos
            .Select(l => new LaudoRaw(
                l.Id, l.StudyInstanceUID, l.Versao, l.FinalizadoEm,
                l.MedicoId, l.MedicoNomeSnapshot, l.MedicoCrmSnapshot,
                assinadoEmPorLaudo.GetValueOrDefault(l.Id)))
            .ToList();

        // Laudo VIGENTE do exame: a versão mais recente entre todos os UIDs que o representam
        // (retificação sobe a versão dentro do mesmo estudo; entre estudos, vale o mais novo).
        var laudoPorExame = new Dictionary<Guid, LaudoRaw>();
        foreach (var l in laudos)
        {
            if (!examePorUid.TryGetValue(l.StudyInstanceUID, out var e)) continue;
            if (!laudoPorExame.TryGetValue(e.Id, out var atual) || MaisRecente(l, atual))
                laudoPorExame[e.Id] = l;
        }

        return new RecorteImagem(exames, laudos, laudoPorExame, examePorUid);

        static bool MaisRecente(LaudoRaw candidato, LaudoRaw atual)
        {
            var fc = candidato.FinalizadoEm ?? DateTime.MinValue;
            var fa = atual.FinalizadoEm ?? DateTime.MinValue;
            return fc != fa ? fc > fa : candidato.Versao > atual.Versao;
        }
    }

    private static double? Media(List<double> valores) =>
        valores.Count > 0 ? Math.Round(valores.Average(), 1) : null;

    private static string RotuloModalidade(ModalidadeDicom? m) => m switch
    {
        ModalidadeDicom.CR => "CR — RX (placa)",
        ModalidadeDicom.DX => "DX — RX (direto)",
        ModalidadeDicom.MG => "MG — Mamografia",
        ModalidadeDicom.US => "US — Ultrassom",
        ModalidadeDicom.CT => "CT — Tomografia",
        ModalidadeDicom.MR => "MR — Ressonância",
        ModalidadeDicom.NM => "NM — Med. Nuclear",
        ModalidadeDicom.PT => "PT — PET",
        ModalidadeDicom.OT => "OT — Outra",
        _ => "(sem tipo)",
    };

    private static string RotuloStatusExame(StatusSolicitacaoExame s) => s switch
    {
        StatusSolicitacaoExame.Solicitada => "Solicitada",
        StatusSolicitacaoExame.Agendada => "Agendada",
        StatusSolicitacaoExame.EmExecucao => "Em execução",
        StatusSolicitacaoExame.Realizada => "Realizada",
        StatusSolicitacaoExame.Laudada => "Laudada",
        StatusSolicitacaoExame.Cancelada => "Cancelada",
        StatusSolicitacaoExame.Enviada => "Enviada",
        StatusSolicitacaoExame.Recebida => "Recebida",
        _ => s.ToString(),
    };

    private sealed record ResumoBruto(
        long Enviadas, long Recebidas, long TemplatesSistema, long TemplatesAtendente,
        long TextoAtendente, long TextoSistema, long Entregues, long Lidas, int Atendentes);

    private async Task<ResumoBruto> LerResumoAsync(DbConnection conn, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        await using var cmd = CriarComando(conn,
            $"""
            SELECT
              count(*) FILTER (WHERE m.direcao=1) AS enviadas,
              count(*) FILTER (WHERE m.direcao=2) AS recebidas,
              count(*) FILTER (WHERE m.direcao=1 AND m.template IS NOT NULL AND m.template<>'' AND m.autor_usuario_id IS NULL) AS tpl_sistema,
              count(*) FILTER (WHERE m.direcao=1 AND m.template IS NOT NULL AND m.template<>'' AND m.autor_usuario_id IS NOT NULL) AS tpl_atendente,
              count(*) FILTER (WHERE m.direcao=1 AND (m.template IS NULL OR m.template='') AND m.autor_usuario_id IS NOT NULL) AS texto_atendente,
              count(*) FILTER (WHERE m.direcao=1 AND (m.template IS NULL OR m.template='') AND m.autor_usuario_id IS NULL) AS texto_sistema,
              count(*) FILTER (WHERE m.direcao=1 AND m.status IN (2,3)) AS entregues,
              count(*) FILTER (WHERE m.direcao=1 AND m.status=3) AS lidas,
              count(DISTINCT m.autor_usuario_id) FILTER (WHERE m.autor_usuario_id IS NOT NULL) AS atendentes
            FROM smsmarica.whatsapp_mensagem m
            WHERE {FiltroPeriodoSimulado}
            """, de, ate);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return new ResumoBruto(
            r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3),
            r.GetInt64(4), r.GetInt64(5), r.GetInt64(6), r.GetInt64(7), (int)r.GetInt64(8));
    }

    private async Task<IReadOnlyList<SerieDiaDto>> LerPorDiaAsync(
        DbConnection conn, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        await using var cmd = CriarComando(conn,
            "SELECT m.ocorrido_em::date AS dia, " +
            "count(*) FILTER (WHERE m.direcao=1) AS env, count(*) FILTER (WHERE m.direcao=2) AS rec " +
            $"FROM smsmarica.whatsapp_mensagem m WHERE {FiltroPeriodoSimulado} GROUP BY 1 ORDER BY 1",
            de, ate);
        var lista = new List<SerieDiaDto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new SerieDiaDto(DateOnly.FromDateTime(r.GetDateTime(0)), r.GetInt64(1), r.GetInt64(2)));
        return lista;
    }

    private async Task<IReadOnlyList<RotuloContagemDto>> LerRotuloAsync(
        DbConnection conn, string sql, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        await using var cmd = CriarComando(conn, sql, de, ate);
        var lista = new List<RotuloContagemDto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new RotuloContagemDto(r.GetString(0), r.GetInt64(1)));
        return lista;
    }

    private async Task<IReadOnlyList<RotuloContagemDto>> LerStatusAsync(
        DbConnection conn, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        await using var cmd = CriarComando(conn,
            "SELECT m.status, count(*) FROM smsmarica.whatsapp_mensagem m " +
            $"WHERE {FiltroPeriodoSimulado} GROUP BY 1 ORDER BY 1", de, ate);
        var lista = new List<RotuloContagemDto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new RotuloContagemDto(RotuloStatus(r.IsDBNull(0) ? (int?)null : r.GetInt32(0)), r.GetInt64(1)));
        return lista;
    }

    private async Task<long> LerEscalarAsync(DbConnection conn, string sql, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        await using var cmd = CriarComando(conn, sql, de, ate);
        var v = await cmd.ExecuteScalarAsync(ct);
        return v is long l ? l : Convert.ToInt64(v);
    }

    private static DbCommand CriarComando(DbConnection conn, string sql, DateOnly de, DateOnly ate)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParam(cmd, "de", de);
        AddParam(cmd, "ate", ate);
        return cmd;
    }

    private static void AddParam(DbCommand cmd, string nome, DateOnly valor)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = nome;
        p.Value = valor;
        cmd.Parameters.Add(p);
    }

    private static string RotuloStatus(int? status) => status switch
    {
        1 => "Enviada",
        2 => "Entregue",
        3 => "Lida",
        4 => "Falha",
        5 => "Recebida",
        _ => "Outro",
    };
}
