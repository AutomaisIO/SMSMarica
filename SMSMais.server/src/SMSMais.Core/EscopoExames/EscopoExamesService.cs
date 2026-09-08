using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.EscopoExames.Dtos;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.EscopoExames;

/// <summary>
/// O que cada unidade executa de imagem — a tela por trás de "Unidades → Exames de imagem".
/// Ver <see cref="TipoExameUnidade"/> para o porquê do modelo.
/// </summary>
public interface IEscopoExamesService
{
    Task<IReadOnlyList<EscopoExameItemDto>> ListarDaUnidadeAsync(
        Guid unidadeId, bool incluirInativos, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EscopoExameItemDto>> ListarDoTipoAsync(
        Guid tipoExameId, CancellationToken cancellationToken = default);

    Task<Guid> AdicionarAsync(AdicionarEscopoExameRequest request, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Guid id, AtualizarEscopoExameRequest request, CancellationToken cancellationToken = default);

    Task RemoverAsync(Guid id, CancellationToken cancellationToken = default);
}

internal sealed class EscopoExamesService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : IEscopoExamesService
{
    public async Task<IReadOnlyList<EscopoExameItemDto>> ListarDaUnidadeAsync(
        Guid unidadeId, bool incluirInativos, CancellationToken cancellationToken = default)
    {
        var query = db.TiposExameUnidade.AsNoTracking()
            .Include(a => a.TipoExame)
            .Include(a => a.Unidade)
            .Include(a => a.Equipamento)
            // TipoExame!.ExcluidoEm: exame excluído do catálogo não volta a aparecer na tela
            // da unidade só porque a associação sobreviveu a ele.
            .Where(a => a.UnidadeId == unidadeId && a.ExcluidoEm == null
                        && a.TipoExame!.ExcluidoEm == null);

        if (!incluirInativos) query = query.Where(a => a.Ativo);

        var linhas = await query.OrderBy(a => a.TipoExame!.Nome).ToListAsync(cancellationToken);

        // Quantos aparelhos da unidade atendem a modalidade de cada linha — a tela usa para dizer
        // se o destino será deduzido (um) ou escolhido pela recepção (mais de um).
        var equipamentos = await db.Equipamentos.AsNoTracking()
            .Where(e => e.UnidadeId == unidadeId && e.Ativo && e.ExcluidoEm == null
                        && e.IdentificadorDicom != null)
            .Select(e => e.ModalidadeDicom)
            .ToListAsync(cancellationToken);

        var porModalidade = equipamentos.GroupBy(m => m).ToDictionary(g => g.Key, g => g.Count());

        return [.. linhas.Select(a => Mapear(a, porModalidade))];
    }

    public async Task<IReadOnlyList<EscopoExameItemDto>> ListarDoTipoAsync(
        Guid tipoExameId, CancellationToken cancellationToken = default)
    {
        var linhas = await db.TiposExameUnidade.AsNoTracking()
            .Include(a => a.TipoExame)
            .Include(a => a.Unidade)
            .Include(a => a.Equipamento)
            .Where(a => a.TipoExameId == tipoExameId && a.ExcluidoEm == null && a.Ativo
                        && a.TipoExame!.ExcluidoEm == null)
            .OrderBy(a => a.Unidade!.Nome)
            .ToListAsync(cancellationToken);

        // Aqui a contagem por unidade importa menos (a tela é de leitura), então resolve-se por
        // unidade em uma consulta só.
        var unidades = linhas.Select(a => a.UnidadeId).Distinct().ToList();
        var equipamentos = await db.Equipamentos.AsNoTracking()
            .Where(e => unidades.Contains(e.UnidadeId) && e.Ativo && e.ExcluidoEm == null
                        && e.IdentificadorDicom != null)
            .Select(e => new { e.UnidadeId, e.ModalidadeDicom })
            .ToListAsync(cancellationToken);

        return
        [
            .. linhas.Select(a => Mapear(
                a,
                equipamentos.Where(e => e.UnidadeId == a.UnidadeId)
                            .GroupBy(e => e.ModalidadeDicom)
                            .ToDictionary(g => g.Key, g => g.Count())))
        ];
    }

    public async Task<Guid> AdicionarAsync(
        AdicionarEscopoExameRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.TiposExame.AnyAsync(t => t.Id == request.TipoExameId && t.ExcluidoEm == null, cancellationToken))
            throw new NaoEncontradoException(nameof(TipoExame), request.TipoExameId);

        if (!await db.Unidades.AnyAsync(u => u.Id == request.UnidadeId, cancellationToken))
            throw new NaoEncontradoException("Unidade", request.UnidadeId);

        await ValidarEquipamentoAsync(request.EquipamentoId, request.UnidadeId, cancellationToken);

        var existente = await db.TiposExameUnidade.FirstOrDefaultAsync(
            a => a.TipoExameId == request.TipoExameId && a.UnidadeId == request.UnidadeId
                 && a.ExcluidoEm == null, cancellationToken);

        if (existente is not null)
        {
            // Readicionar um exame que fora desativado é reativar, não duplicar — o índice único
            // recusaria a segunda linha de qualquer forma.
            if (existente.Ativo)
                throw new ConflitoException("escopoExame.duplicado",
                    "Este exame já está no escopo desta unidade.");

            existente.Ativo = true;
            existente.EnviarParaWorklist = request.EnviarParaWorklist;
            existente.EquipamentoId = request.EquipamentoId;
            existente.AtualizadoEm = DateTime.UtcNow;
            existente.AtualizadoPor = usuarioAtual.UsuarioId;
            await db.SaveChangesAsync(cancellationToken);
            return existente.Id;
        }

        var novo = new TipoExameUnidade
        {
            Id = Guid.CreateVersion7(),
            TipoExameId = request.TipoExameId,
            UnidadeId = request.UnidadeId,
            EnviarParaWorklist = request.EnviarParaWorklist,
            EquipamentoId = request.EquipamentoId,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };

        db.TiposExameUnidade.Add(novo);
        await db.SaveChangesAsync(cancellationToken);
        return novo.Id;
    }

    public async Task AtualizarAsync(
        Guid id, AtualizarEscopoExameRequest request, CancellationToken cancellationToken = default)
    {
        var a = await db.TiposExameUnidade.FirstOrDefaultAsync(
            x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(TipoExameUnidade), id);

        await ValidarEquipamentoAsync(request.EquipamentoId, a.UnidadeId, cancellationToken);

        var estavaDesligado = !a.EnviarParaWorklist;

        a.EnviarParaWorklist = request.EnviarParaWorklist;
        a.EquipamentoId = request.EquipamentoId;
        a.Ativo = request.Ativo;
        a.AtualizadoEm = DateTime.UtcNow;
        a.AtualizadoPor = usuarioAtual.UsuarioId;

        // LIGAR TEM DE VALER PARA QUEM JÁ ESTÁ ESPERANDO. O worker só seleciona por
        // ProximaTentativaEm, e um exame autorizado enquanto o envio estava desligado saiu da fila
        // com esse campo nulo — ligar depois não o traria de volta. Foi o que aconteceu em
        // 08/09/2026: às 17:52 o 260908005 foi autorizado e carimbado como impedido; às 18:06
        // alguém ligou o exame na tela e nada aconteceu, porque o toggle mexia só na configuração.
        // A tela oferecia um botão que parecia resolver e não resolvia.
        if (estavaDesligado && request.EnviarParaWorklist && request.Ativo)
        {
            await ReenfileirarPendentesAsync(a.TipoExameId, a.UnidadeId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Devolve à fila os exames deste par que estavam esperando: só os ainda vivos
    /// (<c>Solicitada</c>/<c>Enviada</c>), já autorizados pela recepção e sem item na worklist.
    /// Mesma régua do "Reenviar worklist", aplicada em lote — inclusive limpando o motivo antigo,
    /// que senão continuaria na tela contradizendo o estado novo.
    ///
    /// <para>Não toca em exame realizado, laudado ou cancelado, nem em exame não autorizado: o gate
    /// da recepção continua valendo, ligar o envio não autoriza ninguém.</para>
    /// </summary>
    private async Task ReenfileirarPendentesAsync(Guid tipoExameId, Guid unidadeId, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;

        var pendentes = await db.ExamesImagem
            .Where(e => e.TipoExameId == tipoExameId
                        && e.ExcluidoEm == null
                        && e.WorklistItemUid == null
                        && (e.Status == StatusSolicitacaoExame.Solicitada
                            || e.Status == StatusSolicitacaoExame.Enviada)
                        && e.Solicitacao!.UnidadeExecutanteId == unidadeId
                        && e.Solicitacao!.AutorizadoEm != null)
            .ToListAsync(ct);

        foreach (var e in pendentes)
        {
            e.ProximaTentativaEm = agora;
            e.ErroIntegracaoPacs = null;
            e.AtualizadoEm = agora;
            e.AtualizadoPor = usuarioAtual.UsuarioId;
        }
    }

    public async Task RemoverAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var a = await db.TiposExameUnidade.FirstOrDefaultAsync(
            x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(TipoExameUnidade), id);

        var agora = DateTime.UtcNow;
        a.ExcluidoEm = agora;
        a.ExcluidoPor = usuarioAtual.UsuarioId;
        a.AtualizadoEm = agora;
        a.AtualizadoPor = usuarioAtual.UsuarioId;

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// O aparelho tem de ser DA unidade. É a trava que impede mandar exame do CDT para a máquina do
    /// Centro Materno Infantil — o select da tela já só oferece os certos, mas a API não confia nele.
    /// </summary>
    private async Task ValidarEquipamentoAsync(Guid? equipamentoId, Guid unidadeId, CancellationToken ct)
    {
        if (equipamentoId is not { } eq) return;

        var pertence = await db.Equipamentos.AsNoTracking()
            .AnyAsync(e => e.Id == eq && e.UnidadeId == unidadeId && e.ExcluidoEm == null, ct);

        if (!pertence)
            throw new ValidacaoException("escopoExame.equipamento_de_outra_unidade",
                "O equipamento escolhido não é desta unidade.");
    }

    private static EscopoExameItemDto Mapear(
        TipoExameUnidade a, IReadOnlyDictionary<ModalidadeDicom, int> equipamentosPorModalidade)
    {
        var modalidade = a.TipoExame?.ModalidadeDicom ?? ModalidadeDicom.Indefinida;
        equipamentosPorModalidade.TryGetValue(modalidade, out var compativeis);

        return new EscopoExameItemDto(
            a.Id,
            a.TipoExameId,
            a.TipoExame?.Nome ?? "—",
            a.TipoExame?.CodigoSisreg,
            modalidade,
            a.UnidadeId,
            a.Unidade?.Nome ?? "—",
            a.EnviarParaWorklist,
            a.EquipamentoId,
            a.Equipamento?.Nome,
            a.Equipamento?.IdentificadorDicom,
            compativeis,
            a.Ativo);
    }
}
