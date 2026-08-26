using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes;
using SMSMais.Core.PendenciasCadastro.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.PendenciasCadastro;

public sealed class PendenciaCadastroService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IPacientesService pacientes) : IPendenciaCadastroService
{
    public async Task<IReadOnlyList<PendenciaCadastroListItemDto>> ListarAsync(
        StatusPendenciaCadastro? status, CancellationToken ct = default)
    {
        var query = db.PendenciasCadastro.AsNoTracking().AsQueryable();
        if (status is { } s) query = query.Where(p => p.Status == s);

        var linhas = await query
            .OrderByDescending(p => p.CriadoEm)
            .Take(500)
            .ToListAsync(ct);

        // Resolve nome/CPF uma vez por paciente distinto (melhor esforço; hub FHIR).
        var mapa = new Dictionary<Guid, (string? Nome, string? Cpf)>();
        foreach (var pid in linhas.Where(l => l.PacienteId is not null).Select(l => l.PacienteId!.Value).Distinct())
        {
            try
            {
                var p = await pacientes.ObterPorIdAsync(pid, ct);
                mapa[pid] = (p.NomeCompleto, p.Cpf);
            }
            catch (NaoEncontradoException)
            {
                // paciente pode ter sido removido; segue sem nome.
            }
        }

        return [.. linhas.Select(p =>
        {
            var info = p.PacienteId is { } pid && mapa.TryGetValue(pid, out var v) ? v : (Nome: (string?)null, Cpf: (string?)null);
            return new PendenciaCadastroListItemDto(
                p.Id, p.TelefoneCanonical, p.PacienteId, info.Nome, info.Cpf,
                p.Tipo, p.Vinculo, p.Observacao, p.Status,
                p.CriadoPor is null, p.CriadoEm, p.ResolvidoEm, p.ResolucaoNota);
        })];
    }

    public Task ResolverAsync(Guid id, string? nota, CancellationToken ct = default) =>
        FinalizarAsync(id, StatusPendenciaCadastro.Resolvida, nota, ct);

    public Task IgnorarAsync(Guid id, string? nota, CancellationToken ct = default) =>
        FinalizarAsync(id, StatusPendenciaCadastro.Ignorada, nota, ct);

    private async Task FinalizarAsync(Guid id, StatusPendenciaCadastro status, string? nota, CancellationToken ct)
    {
        var p = await db.PendenciasCadastro.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NaoEncontradoException("Pendência de cadastro", id);

        p.Status = status;
        p.ResolvidoEm = DateTime.UtcNow;
        p.ResolvidoPor = usuarioAtual.UsuarioId;
        p.ResolucaoNota = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> RegistrarNumeroErradoAsync(
        Guid? conversaId,
        string telefoneCanonical,
        Guid? pacienteId,
        VinculoContato vinculo,
        string? observacao,
        Guid? criadoPor,
        CancellationToken ct = default)
    {
        // Idempotência: já há uma Aberta para o mesmo telefone + paciente? Atualiza em vez de duplicar.
        var existente = await db.PendenciasCadastro.FirstOrDefaultAsync(
            p => p.Status == StatusPendenciaCadastro.Aberta
                && p.TelefoneCanonical == telefoneCanonical
                && p.PacienteId == pacienteId,
            ct);

        if (existente is not null)
        {
            existente.Vinculo = vinculo;
            if (!string.IsNullOrWhiteSpace(observacao)) existente.Observacao = observacao.Trim();
            if (conversaId is not null) existente.ConversaId = conversaId;
            await db.SaveChangesAsync(ct);
            return existente.Id;
        }

        var pendencia = new PendenciaCadastro
        {
            Id = Guid.CreateVersion7(),
            ConversaId = conversaId,
            TelefoneCanonical = telefoneCanonical,
            PacienteId = pacienteId,
            Tipo = TipoPendenciaCadastro.NumeroErrado,
            Vinculo = vinculo,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
            Status = StatusPendenciaCadastro.Aberta,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = criadoPor,
        };
        db.PendenciasCadastro.Add(pendencia);
        await db.SaveChangesAsync(ct);
        return pendencia.Id;
    }
}
