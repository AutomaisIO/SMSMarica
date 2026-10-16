using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Equipamentos.Dtos;
using SMSMais.Core.Worklist;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Equipamentos;

public sealed class EquipamentosService(SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual) : IEquipamentosService
{
    private readonly SmsMaisDbContext _db = db;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    public async Task<IReadOnlyList<EquipamentoListItemDto>> ListarAsync(
        Guid? unidadeId, bool incluirInativos, CancellationToken cancellationToken = default)
    {
        IQueryable<Equipamento> query = _db.Equipamentos.AsNoTracking()
            .Include(e => e.Unidade)
            .Where(e => e.ExcluidoEm == null);

        if (!incluirInativos)
        {
            query = query.Where(e => e.Ativo);
        }

        if (unidadeId.HasValue)
        {
            query = query.Where(e => e.UnidadeId == unidadeId);
        }

        var lista = await query.OrderBy(e => e.Nome).ToListAsync(cancellationToken);
        return [.. lista.Select(EquipamentosMapper.ParaListItem)];
    }

    public async Task<EquipamentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var e = await _db.Equipamentos.AsNoTracking()
            .Include(x => x.Unidade)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Equipamento), id);

        return EquipamentosMapper.ParaDto(e);
    }

    public async Task<Guid> CadastrarAsync(CadastrarEquipamentoRequest request, CancellationToken cancellationToken = default)
    {
        var nome = request.Nome.Trim();
        await ValidarUnidadeAsync(request.UnidadeId, cancellationToken);
        await GarantirNomeUnicoAsync(request.UnidadeId, nome, null, cancellationToken);

        // Código numa tag de texto (fora de sequência) dispensa as sequências: uma coisa ou outra.
        var codigoTag = EquipamentosMapper.TagCodigo(request.CodigoProcedimentoTag);
        var equipamento = new Equipamento
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            UnidadeId = request.UnidadeId,
            ModalidadeDicom = request.ModalidadeDicom,
            IdentificadorDicom = Normalizar(request.IdentificadorDicom),
            DescricaoMaxCaracteres = request.DescricaoMaxCaracteres,
            CodigoProcedimentoDestinos = codigoTag is null
                ? EquipamentosMapper.Destinos(request.CodigoNoProcedimentoSolicitado, request.CodigoNoProtocoloAgendado)
                : DestinoCodigoProcedimento.Nenhum,
            CodigoProcedimentoEsquema = Normalizar(request.CodigoProcedimentoEsquema),
            CodigoProcedimentoTag = codigoTag,
            MedicoSolicitanteTag = EquipamentosMapper.TagLigada(
                request.EnviarMedicoSolicitante, request.MedicoSolicitanteTag, TagDicomWorklist.MedicoSolicitantePadrao),
            UnidadeOrigem = request.UnidadeOrigem,
            UnidadeTag = EquipamentosMapper.TagLigada(
                request.EnviarUnidade, request.UnidadeTag, TagDicomWorklist.UnidadePadrao(request.UnidadeOrigem)),
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.Equipamentos.Add(equipamento);
        await _db.SaveChangesAsync(cancellationToken);
        return equipamento.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarEquipamentoRequest request, CancellationToken cancellationToken = default)
    {
        var e = await _db.Equipamentos.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Equipamento), id);

        var nome = request.Nome.Trim();
        await ValidarUnidadeAsync(request.UnidadeId, cancellationToken);
        await GarantirNomeUnicoAsync(request.UnidadeId, nome, id, cancellationToken);

        e.Nome = nome;
        e.UnidadeId = request.UnidadeId;
        e.ModalidadeDicom = request.ModalidadeDicom;
        e.IdentificadorDicom = Normalizar(request.IdentificadorDicom);
        // Omitido = mantém (ver AtualizarEquipamentoRequest): edição de nome não desfaz o aparelho.
        if (request.DescricaoMaxCaracteres is { } descricaoMax) e.DescricaoMaxCaracteres = descricaoMax;
        if (request.CodigoNoProcedimentoSolicitado is not null || request.CodigoNoProtocoloAgendado is not null)
        {
            var atual = e.CodigoProcedimentoDestinos;
            e.CodigoProcedimentoDestinos = EquipamentosMapper.Destinos(
                request.CodigoNoProcedimentoSolicitado ?? atual.HasFlag(DestinoCodigoProcedimento.ProcedimentoSolicitado),
                request.CodigoNoProtocoloAgendado ?? atual.HasFlag(DestinoCodigoProcedimento.ProtocoloAgendado));
            e.CodigoProcedimentoEsquema = Normalizar(request.CodigoProcedimentoEsquema);
        }
        // Campos opcionais da worklist (adendo do ADR-0074) — mesma régua: omitido = mantém.
        if (request.CodigoProcedimentoTag is not null)
            e.CodigoProcedimentoTag = EquipamentosMapper.TagCodigo(request.CodigoProcedimentoTag);
        if (e.CodigoProcedimentoTag is not null) e.CodigoProcedimentoDestinos = DestinoCodigoProcedimento.Nenhum;
        if (request.EnviarMedicoSolicitante is { } enviarMedico)
            e.MedicoSolicitanteTag = EquipamentosMapper.TagLigada(
                enviarMedico, request.MedicoSolicitanteTag, TagDicomWorklist.MedicoSolicitantePadrao);
        if (request.UnidadeOrigem is { } origem) e.UnidadeOrigem = origem;
        if (request.EnviarUnidade is { } enviarUnidade)
            e.UnidadeTag = EquipamentosMapper.TagLigada(
                enviarUnidade, request.UnidadeTag, TagDicomWorklist.UnidadePadrao(e.UnidadeOrigem));
        e.Ativo = request.Ativo;
        e.AtualizadoEm = DateTime.UtcNow;
        e.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var e = await _db.Equipamentos.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Equipamento), id);

        // A trava "ha agendas vinculadas" saiu junto com a agenda local (05/09/2026). Hoje o
        // equipamento so e referenciado por PACS/worklist, que nao tem FK para ele — o vinculo e
        // pelo AE Title. Se algum dia voltar a existir agenda propria, a trava volta com ela.

        var agora = DateTime.UtcNow;
        e.ExcluidoEm = agora;
        e.ExcluidoPor = _usuarioAtual.UsuarioId;
        e.AtualizadoEm = agora;
        e.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidarUnidadeAsync(Guid unidadeId, CancellationToken ct)
    {
        var existe = await _db.Unidades.AsNoTracking().AnyAsync(u => u.Id == unidadeId, ct);
        if (!existe)
        {
            throw new NaoEncontradoException("Unidade", unidadeId);
        }
    }

    private async Task GarantirNomeUnicoAsync(Guid unidadeId, string nome, Guid? ignorarId, CancellationToken ct)
    {
        var duplicado = await _db.Equipamentos.AsNoTracking().AnyAsync(
            e => e.UnidadeId == unidadeId && e.Nome == nome && e.ExcluidoEm == null
                && (ignorarId == null || e.Id != ignorarId),
            ct);
        if (duplicado)
        {
            throw new ConflitoException("equipamento.nome_duplicado",
                $"Já existe equipamento '{nome}' nesta unidade.");
        }
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
