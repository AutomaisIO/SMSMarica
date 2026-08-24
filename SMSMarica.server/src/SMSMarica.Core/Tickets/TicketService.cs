using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Tickets.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Tickets;

public sealed class TicketService(SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual) : ITicketService
{
    private readonly SmsMaisDbContext _db = db;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    // ================= Self-service =================

    public async Task<IReadOnlyList<TicketListItemDto>> ListarVisiveisAsync(bool incluirArquivados, CancellationToken ct = default)
    {
        var visibilidade = await ObterVisibilidadeAsync(ct);
        var meuId = _usuarioAtual.UsuarioId;
        var minhaUnidade = _usuarioAtual.UnidadeAtivaId;

        var query = _db.Tickets.AsNoTracking().Where(t => t.ExcluidoEm == null);

        query = visibilidade switch
        {
            TicketVisibilidade.Publico => query,
            TicketVisibilidade.PorUnidade => query.Where(t => t.CriadoPor == meuId
                || (minhaUnidade != null && t.UnidadeId == minhaUnidade)),
            _ => query.Where(t => t.CriadoPor == meuId),
        };

        if (!incluirArquivados)
        {
            query = query.Where(t => t.ArquivadoPeloAutorEm == null);
        }

        return await ProjetarListaAsync(query, ct);
    }

    public Task<TicketDto> ObterAsync(Guid id, CancellationToken ct = default) => ObterInternoAsync(id, gestao: false, ct);

    public async Task<Guid> AbrirAsync(AbrirTicketRequest request, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var ticket = new Ticket
        {
            Id = Guid.CreateVersion7(),
            Titulo = request.Titulo.Trim(),
            Descricao = request.Descricao.Trim(),
            Tipo = request.Tipo,
            Status = TicketStatus.Aberto,
            Prioridade = TicketPrioridade.Normal,
            UnidadeId = _usuarioAtual.UnidadeAtivaId,
            CriadoEm = agora,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        foreach (var anexo in DistinctAnexos(request.Anexos))
        {
            ticket.Anexos.Add(new TicketAnexo
            {
                Id = Guid.CreateVersion7(),
                TicketId = ticket.Id,
                MidiaId = anexo.MidiaId,
                NomeArquivo = anexo.NomeArquivo,
                CriadoEm = agora,
            });
        }

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct);
        return ticket.Id;
    }

    public async Task ComentarAsync(Guid id, ComentarTicketRequest request, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id && t.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(Ticket), id);

        // Usuário comum só comenta no próprio ticket.
        if (ticket.CriadoPor != _usuarioAtual.UsuarioId)
        {
            throw new NaoEncontradoException(nameof(Ticket), id);
        }

        await AdicionarComentarioAsync(ticket, request, interno: false, ct);
    }

    public async Task ArquivarComoAutorAsync(Guid id, bool arquivar, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id && t.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(Ticket), id);

        if (ticket.CriadoPor != _usuarioAtual.UsuarioId)
        {
            throw new NaoEncontradoException(nameof(Ticket), id);
        }

        ticket.ArquivadoPeloAutorEm = arquivar ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TicketConfiguracaoDto> ObterConfiguracaoAsync(CancellationToken ct = default)
        => new(await ObterVisibilidadeAsync(ct));

    public async Task ReconhecerAsync(Guid id, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id && t.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(Ticket), id);

        if (ticket.CriadoPor != _usuarioAtual.UsuarioId)
        {
            throw new NaoEncontradoException(nameof(Ticket), id);
        }

        ticket.RespostaReconhecidaEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TicketResumoAutorDto> ObterResumoAutorAsync(CancellationToken ct = default)
    {
        var meuId = _usuarioAtual.UsuarioId;
        var pendentes = await _db.Tickets.AsNoTracking()
            .Where(t => t.ExcluidoEm == null && t.CriadoPor == meuId && t.ArquivadoPeloAutorEm == null
                && t.RespondidoEm != null
                && (t.RespostaReconhecidaEm == null || t.RespostaReconhecidaEm < t.RespondidoEm))
            .OrderByDescending(t => t.RespondidoEm)
            .Select(t => new TicketPendenteDto(t.Id, t.Numero, t.Titulo, t.Status, t.RespostaFinal))
            .ToListAsync(ct);
        return new TicketResumoAutorDto(pendentes.Count, pendentes);
    }

    // ================= Gestão =================

    public async Task<IReadOnlyList<TicketListItemDto>> ListarTodosAsync(bool incluirArquivados, CancellationToken ct = default)
    {
        var query = _db.Tickets.AsNoTracking().Where(t => t.ExcluidoEm == null);
        if (!incluirArquivados)
        {
            query = query.Where(t => t.ArquivadoPeloAdminEm == null);
        }
        return await ProjetarListaAsync(query, ct);
    }

    public Task<TicketDto> ObterGestaoAsync(Guid id, CancellationToken ct = default) => ObterInternoAsync(id, gestao: true, ct);

    public async Task<TicketContextoDto> ObterContextoPorNumeroAsync(int numero, CancellationToken ct = default)
    {
        var contexto = await _db.Tickets.AsNoTracking()
            .Where(t => t.ExcluidoEm == null && t.Numero == numero)
            .Select(t => new TicketContextoDto(t.Numero, t.Titulo, t.Tipo, t.Status))
            .FirstOrDefaultAsync(ct);
        return contexto ?? throw new NaoEncontradoException(nameof(Ticket), numero);
    }

    public async Task<TicketResumoGestaoDto> ObterResumoGestaoAsync(CancellationToken ct = default)
    {
        var baseQuery = _db.Tickets.AsNoTracking()
            .Where(t => t.ExcluidoEm == null && t.ArquivadoPeloAdminEm == null);

        var novos = await baseQuery.CountAsync(t => t.VistoPelaGestaoEm == null
            || (t.AtualizadoEm ?? t.CriadoEm) > t.VistoPelaGestaoEm, ct);
        var abertos = await baseQuery.CountAsync(t => t.Status == TicketStatus.Aberto, ct);
        var emAnalise = await baseQuery.CountAsync(t => t.Status == TicketStatus.EmAnalise, ct);

        return new TicketResumoGestaoDto(novos, abertos, emAnalise);
    }

    public async Task ComentarGestaoAsync(Guid id, ComentarTicketRequest request, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id && t.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(Ticket), id);

        // Comentário público da gestão = resposta ao autor (levanta a bandeira).
        // Nota interna não vira resposta ao autor. Em ambos os casos a gestão "viu" o ticket.
        ticket.VistoPelaGestaoEm = DateTime.UtcNow;
        if (!request.Interno)
        {
            ticket.RespondidoEm = DateTime.UtcNow;
            ticket.RespostaReconhecidaEm = null;
        }

        await AdicionarComentarioAsync(ticket, request, interno: request.Interno, ct);
    }

    public async Task AtualizarGestaoAsync(Guid id, AtualizarTicketGestaoRequest request, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id && t.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(Ticket), id);

        if (request.Prioridade.HasValue)
        {
            ticket.Prioridade = request.Prioridade.Value;
        }

        if (request.RespostaFinal is not null)
        {
            ticket.RespostaFinal = string.IsNullOrWhiteSpace(request.RespostaFinal) ? null : request.RespostaFinal.Trim();
        }

        var agora = DateTime.UtcNow;

        if (request.Status.HasValue && request.Status.Value != ticket.Status)
        {
            var novo = request.Status.Value;
            // Estados finais exigem um retorno (justificativa do Negado / feedback do Concluído).
            if (novo is TicketStatus.Negado or TicketStatus.Concluido && string.IsNullOrWhiteSpace(ticket.RespostaFinal))
            {
                var campo = novo == TicketStatus.Negado ? "justificativa" : "feedback";
                throw new ValidacaoException("respostaFinal", $"Informe {(novo == TicketStatus.Negado ? "a" : "o")} {campo} ao {(novo == TicketStatus.Negado ? "negar" : "concluir")} o ticket.");
            }
            ticket.Status = novo;

            // Conclusão/negação = resposta ao autor → levanta a bandeira dele.
            if (novo is TicketStatus.Negado or TicketStatus.Concluido)
            {
                ticket.RespondidoEm = agora;
                ticket.RespostaReconhecidaEm = null;
            }
        }

        // A gestão atuou no ticket → some do "novo" no inbox compartilhado.
        ticket.VistoPelaGestaoEm = agora;
        ticket.AtualizadoEm = agora;
        ticket.AtualizadoPor = _usuarioAtual.UsuarioId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarcarEnviadoIaAsync(Guid id, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id && t.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(Ticket), id);

        // Registra o encaminhamento ao Agente IA. Idempotente: reenviar apenas atualiza o instante.
        // Não conta como "resposta ao autor" nem mexe no visto da gestão — é só a marca de envio.
        ticket.EnviadoIaEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ArquivarComoAdminAsync(Guid id, bool arquivar, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id && t.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(Ticket), id);

        ticket.ArquivadoPeloAdminEm = arquivar ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id && t.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(Ticket), id);

        ticket.ExcluidoEm = DateTime.UtcNow;
        ticket.ExcluidoPor = _usuarioAtual.UsuarioId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task AtualizarVisibilidadeAsync(AtualizarVisibilidadeRequest request, CancellationToken ct = default)
    {
        var config = await _db.TicketConfiguracoes.FirstOrDefaultAsync(c => c.Id == TicketConfiguracao.IdSingleton, ct);
        if (config is null)
        {
            config = new TicketConfiguracao { Id = TicketConfiguracao.IdSingleton };
            _db.TicketConfiguracoes.Add(config);
        }

        config.Visibilidade = request.Visibilidade;
        config.AtualizadoEm = DateTime.UtcNow;
        config.AtualizadoPor = _usuarioAtual.UsuarioId;
        await _db.SaveChangesAsync(ct);
    }

    // ================= Internos =================

    private async Task AdicionarComentarioAsync(Ticket ticket, ComentarTicketRequest request, bool interno, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var comentario = new TicketComentario
        {
            Id = Guid.CreateVersion7(),
            TicketId = ticket.Id,
            AutorId = _usuarioAtual.UsuarioId,
            Texto = request.Texto.Trim(),
            Interno = interno,
            CriadoEm = agora,
        };

        foreach (var anexo in DistinctAnexos(request.Anexos))
        {
            comentario.Anexos.Add(new TicketAnexo
            {
                Id = Guid.CreateVersion7(),
                TicketId = ticket.Id,
                ComentarioId = comentario.Id,
                MidiaId = anexo.MidiaId,
                NomeArquivo = anexo.NomeArquivo,
                CriadoEm = agora,
            });
        }

        _db.TicketComentarios.Add(comentario);
        ticket.AtualizadoEm = agora;
        ticket.AtualizadoPor = _usuarioAtual.UsuarioId;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<TicketDto> ObterInternoAsync(Guid id, bool gestao, CancellationToken ct)
    {
        var ticket = await _db.Tickets.AsNoTracking()
            .Include(t => t.Anexos)
            .Include(t => t.Comentarios.OrderBy(c => c.CriadoEm))
                .ThenInclude(c => c.Anexos)
            .FirstOrDefaultAsync(t => t.Id == id && t.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(Ticket), id);

        if (!gestao)
        {
            await ObterVisibilidadeAsync(ct);
            if (!PodeVer(ticket))
            {
                throw new NaoEncontradoException(nameof(Ticket), id);
            }

            // Autor abriu o próprio ticket = reconheceu a resposta (baixa a bandeira).
            if (ticket.CriadoPor == _usuarioAtual.UsuarioId && ticket.RespondidoEm != null
                && (ticket.RespostaReconhecidaEm == null || ticket.RespostaReconhecidaEm < ticket.RespondidoEm))
            {
                await _db.Tickets.Where(t => t.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RespostaReconhecidaEm, DateTime.UtcNow), ct);
            }
        }
        else if (ticket.VistoPelaGestaoEm == null || (ticket.AtualizadoEm ?? ticket.CriadoEm) > ticket.VistoPelaGestaoEm)
        {
            // Gestão abriu o detalhe = visualizou (baixa o "novo" no inbox compartilhado).
            await _db.Tickets.Where(t => t.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.VistoPelaGestaoEm, DateTime.UtcNow), ct);
        }

        // Coleta ids de usuários para resolver nomes.
        var ids = new HashSet<Guid>();
        if (ticket.CriadoPor is { } cp) ids.Add(cp);
        foreach (var c in ticket.Comentarios)
        {
            if (c.AutorId is { } ca) ids.Add(ca);
        }
        var nomes = await ResolverNomesAsync(ids, ct);

        var comentarios = ticket.Comentarios
            .Where(c => gestao || !c.Interno)
            .Select(c => new TicketComentarioDto(
                c.Id, c.AutorId, NomeDe(nomes, c.AutorId), c.Texto, c.Interno, c.CriadoEm,
                [.. c.Anexos.Select(MapAnexo)]))
            .ToList();

        return new TicketDto(
            ticket.Id, ticket.Numero, ticket.Titulo, ticket.Descricao, ticket.Tipo, ticket.Status, ticket.Prioridade,
            ticket.RespostaFinal, NomeDe(nomes, ticket.CriadoPor), ticket.CriadoPor, ticket.UnidadeId,
            ticket.ArquivadoPeloAutorEm != null, ticket.ArquivadoPeloAdminEm != null,
            ticket.CriadoEm, ticket.AtualizadoEm,
            [.. ticket.Anexos.Where(a => a.ComentarioId == null).Select(MapAnexo)],
            comentarios,
            ticket.RespondidoEm, ticket.RespostaReconhecidaEm,
            ticket.EnviadoIaEm != null);
    }

    private async Task<IReadOnlyList<TicketListItemDto>> ProjetarListaAsync(IQueryable<Ticket> query, CancellationToken ct)
    {
        var linhas = await query
            .OrderByDescending(t => t.AtualizadoEm ?? t.CriadoEm)
            .Select(t => new
            {
                t.Id, t.Numero, t.Titulo, t.Tipo, t.Status, t.Prioridade, t.CriadoPor, t.UnidadeId,
                Arquivado = t.ArquivadoPeloAutorEm != null || t.ArquivadoPeloAdminEm != null,
                QtdComentarios = t.Comentarios.Count(c => !c.Interno),
                t.CriadoEm, t.AtualizadoEm,
                RespostaNaoReconhecida = t.RespondidoEm != null
                    && (t.RespostaReconhecidaEm == null || t.RespostaReconhecidaEm < t.RespondidoEm),
                NovoParaGestao = t.VistoPelaGestaoEm == null
                    || (t.AtualizadoEm ?? t.CriadoEm) > t.VistoPelaGestaoEm,
                Respondido = t.RespondidoEm != null
                    || (t.RespostaFinal != null && t.RespostaFinal != ""),
                EnviadoIa = t.EnviadoIaEm != null,
            })
            .ToListAsync(ct);

        var ids = linhas.Where(l => l.CriadoPor.HasValue).Select(l => l.CriadoPor!.Value).ToHashSet();
        var nomes = await ResolverNomesAsync(ids, ct);

        return [.. linhas.Select(l => new TicketListItemDto(
            l.Id, l.Numero, l.Titulo, l.Tipo, l.Status, l.Prioridade, NomeDe(nomes, l.CriadoPor), l.CriadoPor,
            l.UnidadeId, l.Arquivado, l.QtdComentarios, l.CriadoEm, l.AtualizadoEm,
            l.RespostaNaoReconhecida, l.NovoParaGestao, l.Respondido, l.EnviadoIa))];
    }

    private bool PodeVer(Ticket t)
    {
        var meuId = _usuarioAtual.UsuarioId;
        if (t.CriadoPor == meuId) return true;

        var visibilidade = _cacheVisibilidade ?? TicketVisibilidade.Privado;
        return visibilidade switch
        {
            TicketVisibilidade.Publico => true,
            TicketVisibilidade.PorUnidade => _usuarioAtual.UnidadeAtivaId != null && t.UnidadeId == _usuarioAtual.UnidadeAtivaId,
            _ => false,
        };
    }

    private TicketVisibilidade? _cacheVisibilidade;

    private async Task<TicketVisibilidade> ObterVisibilidadeAsync(CancellationToken ct)
    {
        var config = await _db.TicketConfiguracoes.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == TicketConfiguracao.IdSingleton, ct);
        _cacheVisibilidade = config?.Visibilidade ?? TicketVisibilidade.Privado;
        return _cacheVisibilidade.Value;
    }

    private async Task<Dictionary<Guid, string>> ResolverNomesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        return await _db.Usuarios.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NomeCompleto, ct);
    }

    private static string? NomeDe(Dictionary<Guid, string> nomes, Guid? id)
        => id.HasValue && nomes.TryGetValue(id.Value, out var n) ? n : null;

    private static TicketAnexoDto MapAnexo(TicketAnexo a)
        => new(a.Id, a.MidiaId, a.NomeArquivo, $"/midias/{a.MidiaId}");

    private static IEnumerable<TicketAnexoRef> DistinctAnexos(IReadOnlyList<TicketAnexoRef>? anexos)
        => anexos is null ? [] : anexos.GroupBy(a => a.MidiaId).Select(g => g.First());
}
