using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.EscopoExames.Dtos;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.EscopoExames;

/// <summary>
/// O que cada unidade executa de imagem: a tela por trás de "Unidades → Exames de imagem" e do
/// painel "Exames a configurar". Ver <see cref="TipoExameUnidade"/> para o porquê do modelo.
/// </summary>
public interface IEscopoExamesService
{
    Task<IReadOnlyList<EscopoExameItemDto>> ListarDaUnidadeAsync(
        Guid unidadeId, bool incluirInativos, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EscopoExameItemDto>> ListarDoTipoAsync(
        Guid tipoExameId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendenciaEscopoExameDto>> ListarPendenciasAsync(
        Guid? unidadeId, CancellationToken cancellationToken = default);

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
            .Where(a => a.UnidadeId == unidadeId && a.ExcluidoEm == null);

        if (!incluirInativos) query = query.Where(a => a.Ativo);

        var linhas = await query.OrderBy(a => a.TipoExame!.Nome).ToListAsync(cancellationToken);

        // Quantos aparelhos da unidade atendem a modalidade de cada linha — é o que separa
        // "configurado" de "a definir" quando o destino não está amarrado.
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
            .Where(a => a.TipoExameId == tipoExameId && a.ExcluidoEm == null && a.Ativo)
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

    public async Task<IReadOnlyList<PendenciaEscopoExameDto>> ListarPendenciasAsync(
        Guid? unidadeId, CancellationToken cancellationToken = default)
    {
        // Só unidades COM equipamento: cobrar destino DICOM de quem não tem máquina é ruído — o
        // Hospital Santo Antônio executa radiografia e não tem aparelho nenhum cadastrado.
        var unidadesComEquipamento = await db.Equipamentos.AsNoTracking()
            .Where(e => e.Ativo && e.ExcluidoEm == null && e.IdentificadorDicom != null)
            .Select(e => e.UnidadeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var query = db.TiposExameUnidade.AsNoTracking()
            .Include(a => a.TipoExame)
            .Include(a => a.Unidade)
            .Include(a => a.Equipamento)
            .Where(a => a.ExcluidoEm == null && a.Ativo
                        && unidadesComEquipamento.Contains(a.UnidadeId));

        if (unidadeId is { } uid) query = query.Where(a => a.UnidadeId == uid);

        var linhas = await query.ToListAsync(cancellationToken);

        var equipamentos = await db.Equipamentos.AsNoTracking()
            .Where(e => unidadesComEquipamento.Contains(e.UnidadeId) && e.Ativo
                        && e.ExcluidoEm == null && e.IdentificadorDicom != null)
            .Select(e => new { e.UnidadeId, e.ModalidadeDicom })
            .ToListAsync(cancellationToken);

        var equipamentosPorUnidade = equipamentos
            .GroupBy(e => e.UnidadeId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<ModalidadeDicom, int>)g
                    .GroupBy(e => e.ModalidadeDicom)
                    .ToDictionary(x => x.Key, x => x.Count()));

        var vazio = new Dictionary<ModalidadeDicom, int>();

        var itens = linhas
            .Select(a => (Linha: a, Item: Mapear(
                a,
                equipamentosPorUnidade.TryGetValue(a.UnidadeId, out var mapa) ? mapa : vazio)))
            .Where(x => x.Item.Situacao != SituacaoEscopoExame.Configurado)
            .ToList();

        // Uma consulta só para todas as linhas: dentro do loop isto era um N+1 que cresceria com
        // cada unidade que ganhasse aparelho.
        var chaves = itens.Select(x => x.Linha.TipoExameId).Distinct().ToList();
        var parados = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.TipoExameId != null
                        && chaves.Contains(e.TipoExameId.Value)
                        && e.ExcluidoEm == null
                        && e.WorklistItemUid == null
                        && (e.Status == StatusSolicitacaoExame.Solicitada
                            || e.Status == StatusSolicitacaoExame.Enviada))
            .Join(db.Solicitacoes.AsNoTracking(),
                  e => e.SolicitacaoId, s => s.Id,
                  (e, s) => new { TipoExameId = e.TipoExameId!.Value, s.UnidadeExecutanteId })
            .GroupBy(x => new { x.TipoExameId, x.UnidadeExecutanteId })
            .Select(g => new { g.Key.TipoExameId, g.Key.UnidadeExecutanteId, Quantidade = g.Count() })
            .ToListAsync(cancellationToken);

        var paradosPorPar = parados.ToDictionary(
            p => (p.TipoExameId, p.UnidadeExecutanteId), p => p.Quantidade);

        var pendencias = itens.Select(x => new PendenciaEscopoExameDto(
            x.Linha.Id,
            x.Linha.UnidadeId,
            x.Linha.Unidade?.Nome ?? "—",
            x.Linha.TipoExameId,
            x.Linha.TipoExame?.Nome ?? "—",
            x.Linha.TipoExame?.ModalidadeDicom ?? ModalidadeDicom.Indefinida,
            x.Item.Situacao,
            OQueFalta(x.Item),
            paradosPorPar.TryGetValue((x.Linha.TipoExameId, x.Linha.UnidadeId), out var q) ? q : 0));

        return [.. pendencias.OrderByDescending(p => p.ExamesParados).ThenBy(p => p.UnidadeNome)];
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

        a.EnviarParaWorklist = request.EnviarParaWorklist;
        a.EquipamentoId = request.EquipamentoId;
        a.Ativo = request.Ativo;
        a.AtualizadoEm = DateTime.UtcNow;
        a.AtualizadoPor = usuarioAtual.UsuarioId;

        await db.SaveChangesAsync(cancellationToken);
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

        var situacao = !a.EnviarParaWorklist
            ? SituacaoEscopoExame.Desligado
            : a.EquipamentoId is not null || compativeis == 1
                ? SituacaoEscopoExame.Configurado
                : SituacaoEscopoExame.ADefinir;

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
            a.Ativo,
            situacao);
    }

    private static string OQueFalta(EscopoExameItemDto item) => item.Situacao switch
    {
        SituacaoEscopoExame.Desligado => "worklist desligada",
        SituacaoEscopoExame.ADefinir when item.EquipamentosCompativeis == 0 =>
            "nenhum aparelho desta modalidade na unidade",
        SituacaoEscopoExame.ADefinir =>
            $"{item.EquipamentosCompativeis} aparelhos possíveis — sem destino definido",
        _ => "—",
    };
}
