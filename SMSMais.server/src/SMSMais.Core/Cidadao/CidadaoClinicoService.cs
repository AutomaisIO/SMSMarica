using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Anexos;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Anexos.Dtos;
using SMSMais.Core.Cidadao.Dtos;
using SMSMais.Core.Exames;
using SMSMais.Core.Integracoes.SisregWeb.Chave;
using SMSMais.Core.Laudos;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Core.Laudos.Assinatura.Dtos;
using SMSMais.Core.Laudos.Dtos;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Agendamentos;
using SMSMais.Core.Pacientes.Agendamentos.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Cidadao;

// Visão clínica do cidadão (PWA). Exames de imagem = satélite ExameImagem (id público preservado);
// a regulação (paciente, datas, confirmação) vem por .Solicitacao. Ver ADR-0021.
public sealed class CidadaoClinicoService(
    SmsMaisDbContext db,
    IAnexosService anexos,
    ILaudosService laudos,
    ILaudoAssinaturaService assinatura,
    IExameImagensPdfService imagensPdf,
    IChaveConfirmacaoSisregService chaves,
    IAgendamentosPacienteService agendamentosPaciente,
    IPacientesService pacientes,
    ILogger<CidadaoClinicoService> logger) : ICidadaoClinicoService
{
    public async Task<IReadOnlyList<ExameResumoDto>> ListarExamesAsync(
        Guid pacienteId, CancellationToken cancellationToken = default)
    {
        var exames = await db.ExamesImagem.AsNoTracking()
            .Where(s => s.Solicitacao!.PacienteId == pacienteId && s.ExcluidoEm == null
                && (s.Status == StatusSolicitacaoExame.Realizada || s.Status == StatusSolicitacaoExame.Laudada))
            .OrderByDescending(s => s.RealizadoEm ?? s.CriadoEm)
            .Select(s => new
            {
                s.Id,
                s.StudyInstanceUID,
                s.Status,
                s.DataEstudo,
                s.RealizadoEm,
                s.CriadoEm,
                Nome = s.TipoExame != null ? s.TipoExame.Nome : "Exame de imagem",
            })
            .ToListAsync(cancellationToken);

        if (exames.Count == 0) return [];

        var ids = exames.Select(e => e.Id).ToList();

        var docs = await db.DocumentosExame.AsNoTracking()
            .Where(d => ids.Contains(d.ExameImagemId) && d.ExcluidoEm == null
                && d.Status == StatusDocumentoExame.Salvo)
            .OrderByDescending(d => d.CriadoEm)
            .Select(d => new { d.Id, d.ExameImagemId, d.Nome, d.TamanhoBytes, d.Paginas })
            .ToListAsync(cancellationToken);
        var docsPorExame = docs
            .GroupBy(d => d.ExameImagemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Study EFETIVO por exame: exames sem worklist (ex.: mamografia no Fuji) têm o study REAL
        // do equipamento na ExameAssociacao — e o LAUDO é criado com esse UID real, não com o
        // pré-gerado. Sem isso o laudo nunca casa com o card do exame.
        var associacoes = await db.ExameAssociacoes.AsNoTracking()
            .Where(a => ids.Contains(a.ExameImagemId) && a.ExcluidoEm == null)
            .Select(a => new { a.ExameImagemId, a.StudyInstanceUID })
            .ToListAsync(cancellationToken);
        var studyRealPorExame = associacoes
            .GroupBy(a => a.ExameImagemId)
            .ToDictionary(g => g.Key, g => g.First().StudyInstanceUID);
        string? StudyEfetivo(Guid exameId, string? preGerado) =>
            studyRealPorExame.TryGetValue(exameId, out var real) && !string.IsNullOrEmpty(real)
                ? real
                : (string.IsNullOrEmpty(preGerado) ? null : preGerado);

        var studyUids = exames
            .Select(e => StudyEfetivo(e.Id, e.StudyInstanceUID))
            .Where(u => u is not null)
            .Select(u => u!)
            .Distinct()
            .ToList();
        var laudosPorStudy = studyUids.Count > 0
            ? (await laudos.ListarPorStudyUidsAsync(studyUids, cancellationToken))
                .GroupBy(l => l.StudyInstanceUID)
                .ToDictionary(g => g.Key, g => g.First())
            : [];
        IReadOnlySet<Guid> assinados = laudosPorStudy.Count > 0
            ? await assinatura.QuaisAssinadosAsync([.. laudosPorStudy.Values.Select(l => l.LaudoId)], cancellationToken)
            : new HashSet<Guid>();

        return exames.Select(e =>
        {
            docsPorExame.TryGetValue(e.Id, out var ds);
            LaudoPorStudyDto? laudo = null;
            var studyEfetivo = StudyEfetivo(e.Id, e.StudyInstanceUID);
            if (studyEfetivo is not null)
                laudosPorStudy.TryGetValue(studyEfetivo, out laudo);
            var laudoAssinado = laudo is not null && assinados.Contains(laudo.LaudoId);

            return new ExameResumoDto(
                e.Id,
                // Data do exame = DICOM (StudyDate/StudyTime) como fonte da verdade; cai para a
                // hora de detecção (RealizadoEm) e, por fim, CriadoEm.
                e.DataEstudo ?? e.RealizadoEm ?? e.CriadoEm,
                e.Nome,
                DescreverStatusExame(e.Status),
                string.IsNullOrEmpty(e.StudyInstanceUID) ? null : e.StudyInstanceUID,
                TemImagens: !string.IsNullOrEmpty(e.StudyInstanceUID),
                Documentos: ds is null
                    ? []
                    : [.. ds.Select(d => new AnexoResumoDto(d.Id, d.Nome, d.TamanhoBytes, d.Paginas))],
                LaudoId: laudoAssinado ? laudo!.LaudoId : null,
                LaudoAssinado: laudoAssinado);
        }).ToList();
    }

    public async Task<AnexoConteudo?> ObterAnexoAsync(
        Guid pacienteId, Guid anexoId, CancellationToken cancellationToken = default)
    {
        var pertence = await (
            from d in db.DocumentosExame.AsNoTracking()
            join e in db.ExamesImagem.AsNoTracking() on d.ExameImagemId equals e.Id
            where d.Id == anexoId && d.ExcluidoEm == null && d.Status == StatusDocumentoExame.Salvo
                  && e.Solicitacao!.PacienteId == pacienteId && e.ExcluidoEm == null
            select d.Id).AnyAsync(cancellationToken);

        return pertence ? await anexos.ObterConteudoAsync(anexoId, cancellationToken) : null;
    }

    public async Task<byte[]?> ObterImagensPdfAsync(
        Guid pacienteId, Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var pertence = await db.ExamesImagem.AsNoTracking()
            .AnyAsync(e => e.Id == solicitacaoExameId && e.Solicitacao!.PacienteId == pacienteId && e.ExcluidoEm == null,
                cancellationToken);
        if (!pertence) return null;

        // Abrir o exame no app = VISUALIZOU o "Exame liberado" (✓✓ azul). Best-effort/idempotente.
        await MarcarVisualizadoAsync(solicitacaoExameId, FinalidadeComunicacao.ExameLiberado, cancellationToken);

        return await imagensPdf.GerarOuObterAsync(solicitacaoExameId, cancellationToken);
    }

    /// <summary>Estampa VisualizadoEm na comunicação (solicitação × finalidade). A comunicação é
    /// ancorada na espinha; traduz o id público (exame) → id da espinha. Nunca lança.</summary>
    private async Task MarcarVisualizadoAsync(
        Guid exameId, FinalidadeComunicacao finalidade, CancellationToken ct)
    {
        try
        {
            var solicitacaoId = await db.ExamesImagem.AsNoTracking()
                .Where(e => e.Id == exameId).Select(e => (Guid?)e.SolicitacaoId).FirstOrDefaultAsync(ct)
                ?? exameId;
            await db.ComunicacoesPaciente
                .Where(c => c.SolicitacaoId == solicitacaoId
                    && c.Finalidade == finalidade && c.VisualizadoEm == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.VisualizadoEm, DateTime.UtcNow), ct);
        }
        catch
        {
            /* marcador é telemetria — nunca falha o request do cidadão */
        }
    }

    public async Task<IReadOnlyList<LaudoResumoDto>> ListarLaudosAsync(
        Guid pacienteId, CancellationToken cancellationToken = default)
    {
        var lista = await laudos.ListarAsync(
            new FiltroLaudosDto(PacienteId: pacienteId, Limite: 200), cancellationToken);

        return [.. lista.Itens
            .Where(l => l.Assinado)
            .Select(l => new LaudoResumoDto(l.Id, l.FinalizadoEm ?? l.CriadoEm, l.Titulo, "Assinado"))];
    }

    public async Task<PdfDownloadDto?> ObterLaudoPdfAsync(
        Guid pacienteId, Guid laudoId, CancellationToken cancellationToken = default)
    {
        var laudo = await db.Laudos.AsNoTracking()
            .Where(l => l.Id == laudoId && !l.Excluido)
            .Select(l => new { l.PacienteId, l.StudyInstanceUID })
            .FirstOrDefaultAsync(cancellationToken);

        if (laudo?.PacienteId != pacienteId) return null;
        if (!await assinatura.EstaAssinadoAsync(laudoId, cancellationToken)) return null;

        // Abrir o laudo no app = VISUALIZOU o "Laudo pronto" (✓✓ azul). Resolve o exame pelo study
        // (direto ou associação); best-effort.
        var exameId = await ResolverExamePorStudyAsync(laudo.StudyInstanceUID, cancellationToken);
        if (exameId is { } eid)
            await MarcarVisualizadoAsync(eid, FinalidadeComunicacao.LaudoPronto, cancellationToken);

        return await assinatura.ObterPdfParaDownloadAsync(laudoId, cancellationToken);
    }

    private async Task<Guid?> ResolverExamePorStudyAsync(string? studyUid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(studyUid)) return null;
        var direto = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.StudyInstanceUID == studyUid && e.ExcluidoEm == null)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(ct);
        if (direto is not null) return direto;
        return await db.ExameAssociacoes.AsNoTracking()
            .Where(a => a.StudyInstanceUID == studyUid && a.ExcluidoEm == null)
            .Select(a => (Guid?)a.ExameImagemId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<AgendamentoResumoDto>> ListarAgendamentosAsync(
        Guid pacienteId, string? tipo, CancellationToken cancellationToken = default)
    {
        // A agenda local do municipio (Especialidade -> Agenda -> Agendamento, ADR-0012/0013) foi
        // REMOVIDA em 05/09/2026: nunca saiu de 3 linhas de teste em producao e o motor dela assumia
        // "1 paciente por slot", incompativel com o bloco de N vagas que o SISREG publica. O que o
        // cidadao ve aqui vem do que foi de fato regulado — SISREG, SER, SERNIT e ESUS de São
        // Gonçalo —, em três partes: próximos, na fila e passados (pedido do Bernardo, 09/10/2026:
        // "tem que mostrar passado, futuro e os que estão em fila aguardando").
        var filtro = tipo?.Trim().ToLowerInvariant();
        var agenda = (await agendamentosPaciente.AgendaDoPacienteAsync(
                pacienteId, await CartoesSusAsync(pacienteId, cancellationToken), cancellationToken))
            .Where(a => filtro switch
            {
                "exame" => a.Item.Tipo != "Consulta",
                "consulta" => a.Item.Tipo == "Consulta",
                _ => true,
            })
            .ToList();

        // Do SISREG, o que só a solicitação sabe: a resposta do paciente (botões do card), a unidade
        // da campanha e o exame de imagem já realizado ou cancelado.
        var idsSisreg = agenda
            .Where(a => a.Item.Origem == OrigemAgendamentoPaciente.Sisreg && a.Item.DetalheId is not null)
            .Select(a => a.Item.Id)
            .ToList();
        var solicitacoes = idsSisreg.Count == 0
            ? []
            : await db.Solicitacoes.AsNoTracking()
                .Where(s => idsSisreg.Contains(s.Id))
                .Select(s => new SolicitacaoDoCard(
                    s.Id,
                    s.StatusConfirmacao,
                    s.UnidadeExecutanteId,
                    s.DataAgendada,
                    s.ExameImagem != null ? s.ExameImagem.Status : null,
                    s.ExameImagem != null && s.ExameImagem.TipoExame != null ? s.ExameImagem.TipoExame.Nome : null))
                .ToDictionaryAsync(s => s.Id, cancellationToken);

        var linhas = new List<(AgendamentoResumoDto Dto, MomentoAgendamentoPaciente Momento, DateOnly? Pedido)>();
        foreach (var (item, momentoDaFonte) in agenda)
        {
            var momento = momentoDaFonte;
            var status = StatusParaPaciente(item.Situacao);
            var unidade = item.Unidade;
            var titulo = item.Descricao;
            string? statusConfirmacao = null;
            var podeResponder = false;

            if (solicitacoes.TryGetValue(item.Id, out var s))
            {
                titulo = s.TipoExameNome ?? titulo;
                // O exame de imagem diz o que a agenda não diz: foi feito (tem estudo/laudo) ou foi
                // cancelado — vale mais que a falta de apontamento da unidade no SISREG.
                if (s.ExameStatus is StatusSolicitacaoExame.Realizada or StatusSolicitacaoExame.Laudada)
                    (momento, status) = (MomentoAgendamentoPaciente.Passado, "Realizado");
                else if (s.ExameStatus == StatusSolicitacaoExame.Cancelada)
                    (momento, status) = (MomentoAgendamentoPaciente.Passado, "Cancelado");

                if (momento == MomentoAgendamentoPaciente.Proximo)
                {
                    statusConfirmacao = s.StatusConfirmacao.ToString();
                    status = DescreverStatusConfirmacao(s.StatusConfirmacao);
                    podeResponder = s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente;
                    // Campanha (ADR-0062): o card mostra o local da campanha no lugar da unidade do SISREG.
                    unidade = (await Notificacoes.Campanhas.CampanhaResolver.VigenteAsync(
                        db, s.UnidadeExecutanteId, s.DataAgendada, cancellationToken))?.LocalNome ?? unidade;
                }
            }

            var naFila = momento == MomentoAgendamentoPaciente.NaFila;
            // DataHora da agenda é hora LOCAL de Brasília (wall-clock): vai ao app em UTC.
            DateTime? inicio = naFila || item.DataHora is null ? null : FusoBrasilia.DeBrasiliaParaUtc(item.DataHora.Value);
            var ehConsulta = item.Tipo == "Consulta";
            linhas.Add((new AgendamentoResumoDto(
                item.Origem == OrigemAgendamentoPaciente.Sisreg && item.DetalheId is { } publico ? publico : item.Id,
                inicio,
                inicio,
                ehConsulta ? "Consulta" : "Exame",
                string.IsNullOrWhiteSpace(titulo) ? (ehConsulta ? "Consulta" : "Exame") : titulo,
                null,
                naFila ? null : unidade,
                status,
                // Id público do SISREG: abre o ticket e responde a confirmação (consulta inclusive).
                // Só no próximo — o ticket fala de chave do dia e de confirmação, que no passado não
                // existem mais, e o app antigo conta como "a confirmar" todo card que o tem.
                SolicitacaoExameId: item.Origem == OrigemAgendamentoPaciente.Sisreg
                    && momento == MomentoAgendamentoPaciente.Proximo ? item.DetalheId : null,
                StatusConfirmacao: statusConfirmacao,
                PodeResponder: podeResponder,
                Origem: AgendamentosPacienteService.DescreverRegulacaoParaPaciente(item.Origem),
                NaFila: naFila,
                Momento: momento.ToString(),
                TemHora: item.TemHora), momento, item.DataSolicitacao));
        }

        // Próximos (o mais perto primeiro) → na fila (quem pediu antes primeiro) → passados (o mais
        // recente primeiro; sem data, no fim).
        return [.. linhas
            .OrderBy(l => l.Momento switch
            {
                MomentoAgendamentoPaciente.Proximo => 0,
                MomentoAgendamentoPaciente.NaFila => 1,
                _ => 2,
            })
            .ThenBy(l => l.Momento == MomentoAgendamentoPaciente.Proximo ? l.Dto.InicioEm?.Ticks ?? long.MaxValue : 0)
            .ThenBy(l => l.Momento == MomentoAgendamentoPaciente.NaFila ? l.Pedido?.DayNumber ?? int.MaxValue : 0)
            .ThenBy(l => l.Momento == MomentoAgendamentoPaciente.Passado ? (l.Dto.InicioEm is null ? 1 : 0) : 0)
            .ThenByDescending(l => l.Momento == MomentoAgendamentoPaciente.Passado ? l.Dto.InicioEm?.Ticks ?? 0 : 0)
            .ThenBy(l => l.Dto.Titulo, StringComparer.Ordinal)
            .Select(l => l.Dto)];
    }

    private sealed record SolicitacaoDoCard(
        Guid Id,
        StatusConfirmacaoAgendamento StatusConfirmacao,
        Guid UnidadeExecutanteId,
        DateTime? DataAgendada,
        StatusSolicitacaoExame? ExameStatus,
        string? TipoExameNome);

    /// <summary>
    /// Os cartões SUS do paciente — a fila do SISREG só se liga a ele pelo CNS. Sem o cadastro (hub
    /// fora do ar), a agenda sai sem a fila do SISREG: o resto não depende dele.
    /// </summary>
    private async Task<IReadOnlyCollection<string>> CartoesSusAsync(Guid pacienteId, CancellationToken ct)
    {
        try
        {
            var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
            return [.. (p.Identificadores ?? [])
                .Where(i => i.Sistema.Contains("cns", StringComparison.OrdinalIgnoreCase))
                .Select(i => i.Valor)
                .Append(p.Cns)
                .OfType<string>()
                .Distinct()];
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Agenda do paciente {PacienteId} sem a fila do SISREG: cadastro não lido.", pacienteId);
            return [];
        }
    }

    /// <summary>Situação em palavras do paciente. O que passou sem chegada nem falta apontada é só
    /// "já passou" — a ficha distingue "em aberto" de "sem registro", o paciente não precisa.</summary>
    private static string StatusParaPaciente(SituacaoAgendamentoPaciente s) => s switch
    {
        SituacaoAgendamentoPaciente.EmFila or SituacaoAgendamentoPaciente.Pendente => "Na fila",
        SituacaoAgendamentoPaciente.Agendado => "Agendado",
        SituacaoAgendamentoPaciente.Confirmado => "Confirmado",
        SituacaoAgendamentoPaciente.Compareceu => "Realizado",
        SituacaoAgendamentoPaciente.Concluido => "Concluído",
        SituacaoAgendamentoPaciente.Faltou => "Falta registrada",
        SituacaoAgendamentoPaciente.Cancelado => "Cancelado",
        _ => "Já passou",
    };

    public async Task<AgendamentoExameDetalheDto?> ObterExameAgendadoAsync(
        Guid pacienteId, Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var s = await db.ExamesImagem.AsNoTracking()
            .Include(x => x.TipoExame)
            .Include(x => x.Solicitacao!).ThenInclude(so => so.UnidadeExecutante!).ThenInclude(u => u.Endereco)
            .Include(x => x.Solicitacao!).ThenInclude(so => so.UnidadeSolicitante)
            .FirstOrDefaultAsync(x => x.Id == solicitacaoExameId && x.ExcluidoEm == null, cancellationToken);
        // Sem exame de imagem (consulta, ECG, endoscopia…), o id público é o da própria solicitação.
        var reg = s?.Solicitacao ?? await db.Solicitacoes.AsNoTracking()
            .Include(so => so.UnidadeExecutante!).ThenInclude(u => u.Endereco)
            .Include(so => so.UnidadeSolicitante)
            .FirstOrDefaultAsync(so => so.Id == solicitacaoExameId && so.ExcluidoEm == null, cancellationToken);
        if (reg is null || reg.PacienteId != pacienteId) return null;
        var ehConsulta = reg.Categoria == CategoriaSolicitacao.Consulta;

        // Campanha (ADR-0062): o atendimento é no local da campanha, não na unidade do SISREG — o
        // telefone da unidade também sai, porque não é o de quem atende.
        var campanha = await Notificacoes.Campanhas.CampanhaResolver.VigenteAsync(
            db, reg.UnidadeExecutanteId, reg.DataAgendada, cancellationToken);

        return new AgendamentoExameDetalheDto(
            s?.Id ?? reg.Id,
            s?.TipoExame?.Nome ?? reg.ProcedimentoTexto ?? reg.EspecialidadeTexto ?? (ehConsulta ? "Consulta" : "Exame"),
            reg.DataAgendada,
            reg.DataSolicitacao,
            reg.DataRegulacao,
            campanha?.LocalNome ?? reg.UnidadeExecutante?.Nome,
            campanha?.LocalEndereco ?? FormatarEndereco(reg.UnidadeExecutante?.Endereco),
            campanha is null ? reg.UnidadeExecutante?.Telefone : null,
            reg.UnidadeSolicitante?.Nome,
            string.IsNullOrWhiteSpace(reg.SolicitanteNome) ? null : reg.SolicitanteNome,
            s?.AccessionNumber,
            reg.CodigoSolicitacao,
            reg.Prioridade.ToString(),
            reg.Observacoes,
            reg.StatusConfirmacao.ToString(),
            reg.ConfirmadoEm,
            reg.ConfirmadoCanal,
            reg.ConfirmacaoCanceladaEm,
            reg.MotivoCancelamentoPaciente,
            ChaveAcessoDisponivelHoje: PodeVerChaveHoje(reg),
            Tipo: ehConsulta ? "Consulta" : "Exame");
    }

    /// <summary>A chave aparece no dia do atendimento, e só se o pedido tem número do SISREG.</summary>
    private static bool PodeVerChaveHoje(SMSMais.Data.Entities.Solicitacao reg) =>
        FusoBrasilia.EhHojeEmBrasilia(reg.DataAgendada)
        && !string.IsNullOrWhiteSpace(reg.CodigoSolicitacao)
        && reg.CodigoSolicitacao.Trim() != "0000";

    public async Task<ChaveAcessoCidadaoDto> ObterChaveAcessoExameAsync(
        Guid pacienteId, Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var solicitacaoId = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == solicitacaoExameId && e.ExcluidoEm == null)
            .Select(e => (Guid?)e.SolicitacaoId).FirstOrDefaultAsync(cancellationToken)
            ?? solicitacaoExameId;
        var reg = await db.Solicitacoes.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == solicitacaoId && x.ExcluidoEm == null, cancellationToken);
        // 404 também quando não é do paciente (não vaza existência).
        if (reg is null || reg.PacienteId != pacienteId)
            throw new Common.Excecoes.NaoEncontradoException("solicitacao.nao_encontrada", "Agendamento não encontrado.");

        // A janela é decidida AQUI, não no app: véspera e dia seguinte não veem a chave.
        if (!PodeVerChaveHoje(reg))
            throw new Common.Excecoes.ConflitoException(
                "chave_acesso.fora_do_dia",
                "A chave de acesso fica disponível somente no dia do atendimento.");

        // O mesmo comando do painel: banco primeiro; se não houver, lê no SISREG e guarda.
        var chave = await chaves.ObterAsync(reg.Id, cancellationToken);
        return new ChaveAcessoCidadaoDto(chave.Chave, chave.CodigoSolicitacao);
    }

    private static string? FormatarEndereco(SMSMais.Data.Entities.Endereco? e)
    {
        if (e is null) return null;
        var partes = new[]
        {
            string.Join(", ", new[] { e.Logradouro, e.Numero }.Where(p => !string.IsNullOrWhiteSpace(p))),
            e.Bairro,
            string.Join("/", new[] { e.Cidade, e.Uf }.Where(p => !string.IsNullOrWhiteSpace(p))),
        }.Where(p => !string.IsNullOrWhiteSpace(p));
        var texto = string.Join(" · ", partes);
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    public async Task ConfirmarExameAsync(
        Guid pacienteId, Guid solicitacaoExameId, string canal, CancellationToken cancellationToken = default)
    {
        var s = await ObterSolicitacaoDoPacienteAsync(pacienteId, solicitacaoExameId, cancellationToken);
        if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
            throw new Common.Excecoes.ConflitoException(
                "confirmacao.ja_respondida", "Este agendamento já foi respondido.");

        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
        s.ConfirmadoEm = DateTime.UtcNow;
        s.ConfirmadoCanal = canal;
        s.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelarExameAsync(
        Guid pacienteId, Guid solicitacaoExameId, string motivo, string canal, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new Common.Excecoes.ValidacaoException(
                "confirmacao.motivo_obrigatorio", "Informe o motivo para avisar que não poderá comparecer.");

        var s = await ObterSolicitacaoDoPacienteAsync(pacienteId, solicitacaoExameId, cancellationToken);
        if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
            throw new Common.Excecoes.ConflitoException(
                "confirmacao.ja_respondida", "Este agendamento já foi respondido.");

        var texto = motivo.Trim();
        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Cancelada;
        s.ConfirmacaoCanceladaEm = DateTime.UtcNow;
        s.ConfirmadoCanal = canal;
        s.MotivoCancelamentoPaciente = texto.Length <= 500 ? texto : texto[..500];
        s.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    // Retorna a ESPINHA (Solicitacao) do exame do paciente. O id recebido é o público (exame);
    // traduz para a espinha (consulta: já é o id da espinha).
    private async Task<SMSMais.Data.Entities.Solicitacao> ObterSolicitacaoDoPacienteAsync(
        Guid pacienteId, Guid solicitacaoExameId, CancellationToken ct)
    {
        var solicitacaoId = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == solicitacaoExameId).Select(e => (Guid?)e.SolicitacaoId).FirstOrDefaultAsync(ct)
            ?? solicitacaoExameId;
        var s = await db.Solicitacoes.FirstOrDefaultAsync(
            x => x.Id == solicitacaoId && x.ExcluidoEm == null, ct);
        // 404 também quando não é do paciente (não vaza existência).
        if (s is null || s.PacienteId != pacienteId)
            throw new Common.Excecoes.NaoEncontradoException("solicitacao.nao_encontrada", "Agendamento não encontrado.");
        // Mesma régua da listagem: vale enquanto o exame for do dia (Brasília). Exigir hora
        // futura recusava quem abria o app no dia, depois do horário marcado — o card
        // aparecia com o botão e o POST devolvia 409.
        if (s.DataAgendada is not { } da || da < FusoBrasilia.InicioDoDiaAtualEmUtc())
            throw new Common.Excecoes.ConflitoException(
                "confirmacao.exame_passado", "Este exame já aconteceu ou não tem data futura.");
        return s;
    }

    private static string DescreverStatusConfirmacao(StatusConfirmacaoAgendamento status) => status switch
    {
        StatusConfirmacaoAgendamento.Confirmada => "Confirmado",
        StatusConfirmacaoAgendamento.Cancelada => "Aguardando remarcação",
        _ => "Agendado",
    };

    private static string DescreverStatusExame(StatusSolicitacaoExame status) => status switch
    {
        StatusSolicitacaoExame.Laudada => "Laudado",
        StatusSolicitacaoExame.Realizada => "Realizado",
        _ => status.ToString(),
    };
}
