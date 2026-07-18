using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Auditoria.Dtos;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Auditoria;

public sealed class AuditoriaService(
    SmsMaricaDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IPacienteResolver pacienteResolver) : IAuditoriaService
{
    private const int TamanhoMaximo = 200;

    public async Task RegistrarAsync(
        string entidade,
        string entidadeId,
        string acao,
        string? valorAnterior,
        string? valorNovo,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = usuarioAtual.UsuarioId;
        // Nome do usuário do SISTEMA (se for). Quando o ator é um paciente pelo app, o id não está
        // em Usuarios e fica null aqui — o nome é resolvido na LEITURA (retroativo, sem migration).
        var usuarioNome = usuarioId is null
            ? null
            : await db.Usuarios.AsNoTracking()
                .Where(u => u.Id == usuarioId)
                .Select(u => u.NomeCompleto)
                .FirstOrDefaultAsync(cancellationToken);

        db.RegistrosAuditoria.Add(new RegistroAuditoria
        {
            Id = Guid.CreateVersion7(),
            Entidade = entidade,
            EntidadeId = entidadeId,
            Acao = acao,
            ValorAnterior = valorAnterior,
            ValorNovo = valorNovo,
            UsuarioId = usuarioId,
            UsuarioNome = usuarioNome,
            // X-Forwarded-For já honrado no Program.cs; corta em 64 (limite da coluna).
            Ip = usuarioAtual.Ip is { } ip ? (ip.Length <= 64 ? ip : ip[..64]) : null,
            CriadoEm = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaginaAuditoriaDto> BuscarAsync(AuditoriaFiltroDto filtro, CancellationToken cancellationToken = default)
    {
        var q = db.RegistrosAuditoria.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Entidade))
            q = q.Where(r => r.Entidade == filtro.Entidade);
        if (!string.IsNullOrWhiteSpace(filtro.EntidadeId))
            q = q.Where(r => r.EntidadeId == filtro.EntidadeId);
        if (filtro.UsuarioId is { } uid)
            q = q.Where(r => r.UsuarioId == uid);
        if (filtro.De is { } de)
            q = q.Where(r => r.CriadoEm >= de);
        if (filtro.Ate is { } ate)
            q = q.Where(r => r.CriadoEm <= ate);
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var padrao = $"%{filtro.Texto.Trim()}%";
            q = q.Where(r =>
                EF.Functions.ILike(r.ValorAnterior ?? string.Empty, padrao) ||
                EF.Functions.ILike(r.ValorNovo ?? string.Empty, padrao) ||
                EF.Functions.ILike(r.UsuarioNome ?? string.Empty, padrao) ||
                EF.Functions.ILike(r.Acao, padrao));
        }

        var total = await q.CountAsync(cancellationToken);

        var pagina = filtro.Pagina < 1 ? 1 : filtro.Pagina;
        var tamanho = Math.Clamp(filtro.Tamanho, 1, TamanhoMaximo);

        var registros = await q
            .OrderByDescending(r => r.CriadoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(r => new
            {
                r.Id, r.Entidade, r.EntidadeId, r.Acao, r.ValorAnterior, r.ValorNovo,
                r.UsuarioId, r.UsuarioNome, r.Ip, r.CriadoEm,
            })
            .ToListAsync(cancellationToken);

        // ---- Enriquecimento (retroativo) do ATOR e da ENTIDADE afetada ----
        // Ator: o id pode ser um usuário do sistema OU um paciente (app). Resolve os dois e
        // discrimina o tipo. Entidade afetada "Paciente": resolve o nome do cadastro alterado.
        var atorIds = registros.Where(r => r.UsuarioId is { }).Select(r => r.UsuarioId!.Value).Distinct().ToList();
        var usuariosSistema = await db.Usuarios.AsNoTracking()
            .Where(u => atorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.NomeCompleto })
            .ToDictionaryAsync(u => u.Id, u => u.NomeCompleto, cancellationToken);

        // Ids que não são usuários do sistema → tenta como paciente. Mais os ids das entidades
        // "Paciente" afetadas (para o nome do cadastro alterado). Uma resolução FHIR só.
        var pacienteIds = new HashSet<Guid>(atorIds.Where(id => !usuariosSistema.ContainsKey(id)));
        foreach (var r in registros)
            if (string.Equals(r.Entidade, "Paciente", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(r.EntidadeId, out var eid))
                pacienteIds.Add(eid);

        IReadOnlyDictionary<Guid, PacienteResumo> pacientes = pacienteIds.Count == 0
            ? new Dictionary<Guid, PacienteResumo>()
            : await pacienteResolver.ResolverManyAsync(pacienteIds, cancellationToken);

        var itens = registros.Select(r =>
        {
            string? atorNome = r.UsuarioNome;
            var atorTipo = TipoAtorAuditoria.Desconhecido;
            if (r.UsuarioId is { } aid)
            {
                if (usuariosSistema.TryGetValue(aid, out var nomeSis))
                {
                    atorNome = nomeSis;
                    atorTipo = TipoAtorAuditoria.UsuarioSistema;
                }
                else if (pacientes.TryGetValue(aid, out var pac))
                {
                    atorNome = pac.Nome;
                    atorTipo = TipoAtorAuditoria.Paciente;
                }
            }

            string? entidadeNome = null;
            if (Guid.TryParse(r.EntidadeId, out var entId) && pacientes.TryGetValue(entId, out var pacAfetado))
                entidadeNome = pacAfetado.Nome;

            return new RegistroAuditoriaDto(
                r.Id, r.Entidade, r.EntidadeId, r.Acao, r.ValorAnterior, r.ValorNovo,
                r.UsuarioId, atorNome, atorTipo, entidadeNome, r.Ip, r.CriadoEm);
        }).ToList();

        return new PaginaAuditoriaDto(itens, total, pagina, tamanho);
    }
}
