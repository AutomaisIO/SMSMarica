using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Mapeamento.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Mapeamento;

/// <summary>
/// Workspace de mapeamento SIGTAP→TipoExame (ADR-0021). Exames de IMAGEM importados sem
/// <c>TipoExame</c> (SIGTAP não mapeado) entram como pendentes; aqui o operador vincula um
/// TipoExame existente ao código SIGTAP e faz o BACKFILL de todos os exames que casam. Criar um
/// TipoExame novo é feito na tela de Tipos de Exame (depois volta e vincula).
/// </summary>
public interface IMapeamentoSigtapService
{
    Task<IReadOnlyList<PendenteMapeamentoDto>> ListarPendentesAsync(CancellationToken ct = default);
    Task<VincularMapeamentoResultado> VincularAsync(VincularMapeamentoRequest request, CancellationToken ct = default);
}

public sealed class MapeamentoSigtapService(SmsMaisDbContext db) : IMapeamentoSigtapService
{
    public async Task<IReadOnlyList<PendenteMapeamentoDto>> ListarPendentesAsync(CancellationToken ct = default)
    {
        // Exames de imagem (satélite) sem tipo. O GroupBy+Count sobre a navegação não traduz pro
        // SQL no EF Core — então projetamos os pares (SIGTAP, texto) e agrupamos em memória (o
        // conjunto de pendentes é pequeno).
        var pares = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.ExcluidoEm == null && e.TipoExameId == null && e.Solicitacao!.ExcluidoEm == null)
            .Select(e => new { e.Solicitacao!.ProcedimentoSigtapCodigo, e.Solicitacao!.ProcedimentoTexto })
            .ToListAsync(ct);

        return [.. pares
            .GroupBy(p => new { p.ProcedimentoSigtapCodigo, p.ProcedimentoTexto })
            .Select(g => new PendenteMapeamentoDto(
                g.Key.ProcedimentoSigtapCodigo ?? string.Empty, g.Key.ProcedimentoTexto, g.Count()))
            .OrderByDescending(p => p.Quantidade)];
    }

    public async Task<VincularMapeamentoResultado> VincularAsync(
        VincularMapeamentoRequest request, CancellationToken ct = default)
    {
        var sig = (request.SigtapCodigo ?? string.Empty).Trim();
        if (sig.Length == 0)
            throw new ValidacaoException("mapeamento.sigtap_obrigatorio", "Informe o código SIGTAP.");

        var tipoExiste = await db.TiposExame.AsNoTracking()
            .AnyAsync(t => t.Id == request.TipoExameId && t.ExcluidoEm == null && t.Ativo, ct);
        if (!tipoExiste) throw new NaoEncontradoException(nameof(TipoExame), request.TipoExameId);

        var agora = DateTime.UtcNow;
        // Backfill: todos os exames de imagem pendentes cujo SIGTAP da espinha == o informado.
        var atualizados = await db.ExamesImagem
            .Where(e => e.ExcluidoEm == null && e.TipoExameId == null
                        && e.Solicitacao!.ProcedimentoSigtapCodigo == sig)
            .ExecuteUpdateAsync(u => u
                .SetProperty(e => e.TipoExameId, request.TipoExameId)
                .SetProperty(e => e.AtualizadoEm, agora), ct);

        return new VincularMapeamentoResultado(atualizados);
    }
}
