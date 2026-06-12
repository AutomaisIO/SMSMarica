using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Laudos.Dtos;
using SMSMarica.Core.Medicos;
using SMSMarica.Core.Medicos.Dtos;
using SMSMarica.Core.Medicos.Fhir;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Laudos;

public sealed class LaudosService(
    SmsMaricaDbContext db,
    IHtmlSanitizer sanitizer,
    ISolicitacoesExameService solicitacoes,
    IPacienteFhirClient pacienteFhir,
    IPractitionerFhirClient practitionerFhir,
    IPacienteResolver pacienteResolver,
    ILogger<LaudosService> logger) : ILaudosService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IHtmlSanitizer _sanitizer = sanitizer;
    private readonly ISolicitacoesExameService _solicitacoes = solicitacoes;
    private readonly IPacienteFhirClient _pacienteFhir = pacienteFhir;
    private readonly IPractitionerFhirClient _practitionerFhir = practitionerFhir;
    private readonly IPacienteResolver _pacienteResolver = pacienteResolver;
    private readonly ILogger<LaudosService> _logger = logger;

    private async Task<IReadOnlyList<LaudoListItemDto>> EnriquecerAsync(
        List<LaudoListItemDto> dtos, CancellationToken ct)
    {
        var ids = dtos.Where(d => d.PacienteId.HasValue).Select(d => d.PacienteId!.Value);
        var nomes = await _pacienteResolver.ResolverManyAsync(ids, ct);
        return [.. dtos.Select(d => d.PacienteId.HasValue && nomes.TryGetValue(d.PacienteId.Value, out var r)
            ? d with { PacienteNome = r.Nome } : d)];
    }

    private async Task<LaudoDto> EnriquecerAsync(LaudoDto dto, CancellationToken ct)
    {
        if (!dto.PacienteId.HasValue) return dto;
        var r = await _pacienteResolver.ResolverAsync(dto.PacienteId.Value, ct);
        return r is null ? dto : dto with { PacienteNome = r.Nome, PacienteCpf = r.Cpf };
    }

    public async Task<IReadOnlyList<LaudoListItemDto>> ListarAsync(
        FiltroLaudosDto filtro,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Laudo> query = _db.Laudos.AsNoTracking().Where(l => !l.Excluido);

        if (!string.IsNullOrWhiteSpace(filtro.StudyInstanceUID))
        {
            var uid = filtro.StudyInstanceUID.Trim();
            query = query.Where(l => l.StudyInstanceUID == uid);
        }
        if (filtro.PacienteId.HasValue)
            query = query.Where(l => l.PacienteId == filtro.PacienteId);
        if (filtro.MedicoId.HasValue)
            query = query.Where(l => l.MedicoId == filtro.MedicoId);
        if (filtro.Status.HasValue)
            query = query.Where(l => l.Status == filtro.Status);
        if (filtro.DataInicial.HasValue)
        {
            var ini = filtro.DataInicial.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(l => l.CriadoEm >= ini);
        }
        if (filtro.DataFinal.HasValue)
        {
            var fim = filtro.DataFinal.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(l => l.CriadoEm <= fim);
        }

        var limite = filtro.Limite is <= 0 or > 500 ? 50 : filtro.Limite;
        var lista = await query
            .OrderByDescending(l => l.CriadoEm)
            .Take(limite)
            .ToListAsync(cancellationToken);

        var dtos = await EnriquecerAsync([.. lista.Select(LaudosMapper.ParaListItem)], cancellationToken);
        var assinados = await ResolverAssinadosAsync([.. dtos.Select(d => d.Id)], cancellationToken);
        return [.. dtos.Select(d => assinados.Contains(d.Id) ? d with { Assinado = true } : d)];
    }

    private async Task<HashSet<Guid>> ResolverAssinadosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var assinados = await _db.LaudoAssinaturas.AsNoTracking()
            .Where(a => ids.Contains(a.LaudoId) && a.Status == StatusAssinatura.Concluida)
            .Select(a => a.LaudoId)
            .ToListAsync(ct);
        return [.. assinados];
    }

    public async Task<LaudoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var l = await CarregarCompletoAsync(id, asNoTracking: true, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), id);
        var dto = await EnriquecerAsync(LaudosMapper.ParaDto(l), cancellationToken);
        var assinado = await _db.LaudoAssinaturas.AsNoTracking()
            .AnyAsync(a => a.LaudoId == id && a.Status == StatusAssinatura.Concluida, cancellationToken);
        return dto with { Assinado = assinado };
    }

    public async Task<LaudoDto?> ObterPorStudyAsync(
        string studyInstanceUID,
        CancellationToken cancellationToken = default)
    {
        var uid = NormalizarUid(studyInstanceUID);
        var l = await _db.Laudos.AsNoTracking()
            .Include(x => x.LaudoTemplate)
            .Where(x => x.StudyInstanceUID == uid && !x.Excluido)
            .OrderByDescending(x => x.Versao)
            .FirstOrDefaultAsync(cancellationToken);
        return l is null ? null : await EnriquecerAsync(LaudosMapper.ParaDto(l), cancellationToken);
    }

    public async Task<IReadOnlyList<LaudoHistoricoItemDto>> ListarHistoricoAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var alvo = await _db.Laudos.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.StudyInstanceUID)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), id);

        var historico = await _db.Laudos.AsNoTracking()
            .Where(x => x.StudyInstanceUID == alvo && !x.Excluido)
            .OrderByDescending(x => x.Versao)
            .ToListAsync(cancellationToken);

        return [.. historico.Select(LaudosMapper.ParaHistoricoItem)];
    }

    public async Task<IReadOnlyList<LaudoPorStudyDto>> ListarPorStudyUidsAsync(
        IReadOnlyList<string> studyInstanceUIDs,
        CancellationToken cancellationToken = default)
    {
        if (studyInstanceUIDs is null || studyInstanceUIDs.Count == 0)
            return [];

        var uids = studyInstanceUIDs
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct()
            .ToArray();

        if (uids.Length == 0) return [];

        var bruto = await _db.Laudos.AsNoTracking()
            .Where(l => uids.Contains(l.StudyInstanceUID) && !l.Excluido)
            .Select(l => new { l.StudyInstanceUID, l.Versao, l.Id, l.Status })
            .ToListAsync(cancellationToken);

        return [.. bruto
            .GroupBy(x => x.StudyInstanceUID)
            .Select(g => g.OrderByDescending(x => x.Versao).First())
            .Select(x => new LaudoPorStudyDto(x.StudyInstanceUID, x.Id, x.Versao, x.Status))];
    }

    public async Task<Guid> CadastrarAsync(
        Guid usuarioId,
        CadastrarLaudoRequest request,
        CancellationToken cancellationToken = default)
    {
        var uid = NormalizarUid(request.StudyInstanceUID);
        var medico = await ResolverMedicoAsync(usuarioId, cancellationToken);

        if (request.PacienteId.HasValue)
            await GarantirPacienteExisteAsync(request.PacienteId.Value, cancellationToken);

        if (request.LaudoTemplateId.HasValue)
        {
            var existe = await _db.LaudoTemplates.AsNoTracking().AnyAsync(t => t.Id == request.LaudoTemplateId, cancellationToken);
            if (!existe)
                throw new NaoEncontradoException(nameof(LaudoTemplate), request.LaudoTemplateId);
        }

        var proximaVersao = (await _db.Laudos
            .Where(x => x.StudyInstanceUID == uid && !x.Excluido)
            .Select(x => (int?)x.Versao)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        var agora = DateTime.UtcNow;
        var laudo = new Laudo
        {
            Id = Guid.CreateVersion7(),
            StudyInstanceUID = uid,
            Versao = proximaVersao,
            PacienteId = request.PacienteId,
            MedicoId = medico.Id,
            LaudoTemplateId = request.LaudoTemplateId,
            Titulo = NormalizarTitulo(request.Titulo),
            ConteudoJson = string.IsNullOrWhiteSpace(request.ConteudoJson) ? "{}" : request.ConteudoJson,
            ConteudoHtml = _sanitizer.Sanitize(request.ConteudoHtml ?? string.Empty),
            Status = StatusLaudo.Rascunho,
            CriadoEm = agora,
        };

        _db.Laudos.Add(laudo);
        await _db.SaveChangesAsync(cancellationToken);
        return laudo.Id;
    }

    public async Task AtualizarAsync(
        Guid id,
        Guid usuarioId,
        AtualizarLaudoRequest request,
        CancellationToken cancellationToken = default)
    {
        var laudo = await _db.Laudos.FirstOrDefaultAsync(x => x.Id == id && !x.Excluido, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), id);

        if (laudo.Status == StatusLaudo.Finalizado)
            throw new ConflitoException(
                "laudo.finalizado_imutavel",
                "Laudo finalizado não pode ser editado. Use 'Nova versão' para gerar um rascunho derivado.");

        var medico = await ResolverMedicoAsync(usuarioId, cancellationToken);
        if (laudo.MedicoId != medico.Id)
            throw new ConflitoException("laudo.nao_e_autor", "Apenas o médico autor do rascunho pode editá-lo.");

        if (request.PacienteId.HasValue && request.PacienteId != laudo.PacienteId)
            await GarantirPacienteExisteAsync(request.PacienteId.Value, cancellationToken);

        laudo.PacienteId = request.PacienteId;
        laudo.Titulo = NormalizarTitulo(request.Titulo);
        laudo.ConteudoJson = string.IsNullOrWhiteSpace(request.ConteudoJson) ? "{}" : request.ConteudoJson;
        laudo.ConteudoHtml = _sanitizer.Sanitize(request.ConteudoHtml ?? string.Empty);
        laudo.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task FinalizarAsync(
        Guid id,
        Guid usuarioId,
        FinalizarLaudoRequest request,
        CancellationToken cancellationToken = default)
    {
        var laudo = await _db.Laudos
            .FirstOrDefaultAsync(x => x.Id == id && !x.Excluido, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), id);

        if (laudo.Status == StatusLaudo.Finalizado)
            throw new ConflitoException("laudo.ja_finalizado", "Laudo já está finalizado.");

        var medico = await ResolverMedicoAsync(usuarioId, cancellationToken);
        if (laudo.MedicoId != medico.Id)
            throw new ConflitoException("laudo.nao_e_autor", "Apenas o médico autor pode finalizar o laudo.");

        var html = _sanitizer.Sanitize(request.ConteudoHtml ?? string.Empty);
        if (string.IsNullOrWhiteSpace(html))
            throw new ValidacaoException("laudo.conteudo_vazio", "Conteúdo do laudo não pode estar vazio.");

        laudo.Titulo = NormalizarTitulo(request.Titulo);
        laudo.ConteudoJson = string.IsNullOrWhiteSpace(request.ConteudoJson) ? "{}" : request.ConteudoJson;
        laudo.ConteudoHtml = html;

        // Snapshot dos dados do médico (Practitioner do hub) no momento da finalização.
        laudo.MedicoNomeSnapshot = medico.NomeCompleto;
        laudo.MedicoCrmSnapshot = medico.Registro;
        laudo.MedicoUfCrmSnapshot = medico.UfConselho;
        laudo.MedicoRqeSnapshot = medico.Rqe;

        laudo.Status = StatusLaudo.Finalizado;
        laudo.FinalizadoEm = DateTime.UtcNow;
        laudo.AtualizadoEm = laudo.FinalizadoEm;

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _solicitacoes.MarcarComoLaudadaAsync(laudo.StudyInstanceUID, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Falha ao marcar solicitação como Laudada para StudyInstanceUID {Uid}.", laudo.StudyInstanceUID);
        }
    }

    public async Task<Guid> CriarNovaVersaoAsync(
        Guid id,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var anterior = await _db.Laudos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.Excluido, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), id);

        if (anterior.Status != StatusLaudo.Finalizado)
            throw new ConflitoException(
                "laudo.nova_versao_so_finalizado",
                "Só é possível criar nova versão a partir de um laudo finalizado.");

        var medico = await ResolverMedicoAsync(usuarioId, cancellationToken);

        var proximaVersao = (await _db.Laudos
            .Where(x => x.StudyInstanceUID == anterior.StudyInstanceUID && !x.Excluido)
            .Select(x => (int?)x.Versao)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        var agora = DateTime.UtcNow;
        var novo = new Laudo
        {
            Id = Guid.CreateVersion7(),
            StudyInstanceUID = anterior.StudyInstanceUID,
            Versao = proximaVersao,
            LaudoAnteriorId = anterior.Id,
            PacienteId = anterior.PacienteId,
            MedicoId = medico.Id,
            LaudoTemplateId = anterior.LaudoTemplateId,
            Titulo = anterior.Titulo,
            ConteudoJson = anterior.ConteudoJson,
            ConteudoHtml = anterior.ConteudoHtml,
            Status = StatusLaudo.Rascunho,
            CriadoEm = agora,
        };

        _db.Laudos.Add(novo);
        await _db.SaveChangesAsync(cancellationToken);
        return novo.Id;
    }

    public async Task ExcluirAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var laudo = await _db.Laudos.FirstOrDefaultAsync(x => x.Id == id && !x.Excluido, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), id);

        if (laudo.Status != StatusLaudo.Rascunho)
            throw new ConflitoException(
                "laudo.so_rascunho_apaga",
                "Apenas rascunhos podem ser excluídos. Laudos finalizados são imutáveis (CFM).");

        laudo.Excluido = true;
        laudo.ExcluidoEm = DateTime.UtcNow;
        laudo.ExcluidoPorUsuarioId = usuarioId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Laudo?> CarregarParaPdfAsync(Guid id, CancellationToken cancellationToken = default) =>
        await CarregarCompletoAsync(id, asNoTracking: true, cancellationToken);

    // ---------------- helpers ----------------

    private Task<Laudo?> CarregarCompletoAsync(Guid id, bool asNoTracking, CancellationToken ct)
    {
        IQueryable<Laudo> q = _db.Laudos.Include(x => x.LaudoTemplate);
        if (asNoTracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(x => x.Id == id && !x.Excluido, ct);
    }

    /// <summary>
    /// Resolve o médico-laudador no hub FHIR. MVP: o id do usuário logado é
    /// tratado como id do Practitioner (link Usuário↔Practitioner é fatia futura).
    /// </summary>
    private async Task<MedicoDto> ResolverMedicoAsync(Guid usuarioId, CancellationToken ct)
    {
        // 1) Compat: médicos cujo login tem o MESMO id do Practitioner (Usuario.Id == Practitioner.Id).
        var p = await _practitionerFhir.ObterAsync(usuarioId, ct);

        // 2) Caso geral: o login é um Usuario à parte (id próprio) — casa pelo CPF com o
        //    Practitioner do hub (ex.: médico importado que ganhou login por CPF).
        if (p is null)
        {
            var cpf = await _db.Usuarios.AsNoTracking()
                .Where(u => u.Id == usuarioId)
                .Select(u => u.Cpf)
                .FirstOrDefaultAsync(ct);
            var cpfDigits = SoDigitos(cpf);
            if (cpfDigits.Length == 11)
            {
                var bundle = await _practitionerFhir.BuscarAsync(identifier: cpfDigits, ct: ct);
                p = bundle.Entry.Select(e => e.Resource).OfType<Hl7.Fhir.Model.Practitioner>().FirstOrDefault();
            }
        }

        if (p is null)
            throw new ConflitoException(
                "laudo.usuario_sem_papel_medico",
                "Apenas médicos podem criar/editar Laudos.");

        return MedicoFhirMapper.ParaDto(p);
    }

    private static string SoDigitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string(v.Where(char.IsDigit).ToArray());

    private async Task GarantirPacienteExisteAsync(Guid pacienteId, CancellationToken ct)
    {
        var p = await _pacienteFhir.ObterAsync(pacienteId, ct);
        if (p is null)
            throw new NaoEncontradoException("Paciente", pacienteId);
    }

    private static string NormalizarUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            throw new ValidacaoException("laudo.studyUid_obrigatorio", "StudyInstanceUID é obrigatório.");
        return uid.Trim();
    }

    private static string NormalizarTitulo(string? titulo)
    {
        var t = (titulo ?? string.Empty).Trim();
        return string.IsNullOrEmpty(t) ? "Laudo" : t;
    }
}
