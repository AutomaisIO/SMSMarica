using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Especialidades.Dtos;
using SMSMarica.Core.Identidade;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Especialidades;

public sealed class EspecialidadesService(SmsMaricaDbContext db, IUsuarioAtualAccessor usuarioAtual) : IEspecialidadesService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    public async Task<IReadOnlyList<EspecialidadeListItemDto>> ListarAsync(
        bool incluirInativas, CancellationToken cancellationToken = default)
    {
        IQueryable<Especialidade> query = _db.Especialidades.AsNoTracking()
            .Where(e => e.ExcluidoEm == null);

        if (!incluirInativas)
        {
            query = query.Where(e => e.Ativo);
        }

        var lista = await query.OrderBy(e => e.Nome).ToListAsync(cancellationToken);
        return [.. lista.Select(EspecialidadesMapper.ParaListItem)];
    }

    public async Task<EspecialidadeDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var e = await _db.Especialidades.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Especialidade), id);

        return EspecialidadesMapper.ParaDto(e);
    }

    public async Task<Guid> CadastrarAsync(CadastrarEspecialidadeRequest request, CancellationToken cancellationToken = default)
    {
        var nome = request.Nome.Trim();
        await GarantirNomeUnicoAsync(nome, null, cancellationToken);

        var especialidade = new Especialidade
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            CodigoCbo = Normalizar(request.CodigoCbo),
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.Especialidades.Add(especialidade);
        await _db.SaveChangesAsync(cancellationToken);
        return especialidade.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarEspecialidadeRequest request, CancellationToken cancellationToken = default)
    {
        var e = await _db.Especialidades.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Especialidade), id);

        var nome = request.Nome.Trim();
        await GarantirNomeUnicoAsync(nome, id, cancellationToken);

        e.Nome = nome;
        e.CodigoCbo = Normalizar(request.CodigoCbo);
        e.Ativo = request.Ativo;
        e.AtualizadoEm = DateTime.UtcNow;
        e.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var e = await _db.Especialidades.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Especialidade), id);

        var emUso = await _db.Agendas.AsNoTracking()
            .AnyAsync(a => a.EspecialidadeId == id && a.ExcluidoEm == null, cancellationToken);
        if (emUso)
        {
            throw new ConflitoException("especialidade.em_uso",
                "Não é possível excluir: há agendas vinculadas a esta especialidade. Desative-a em vez disso.");
        }

        var agora = DateTime.UtcNow;
        e.ExcluidoEm = agora;
        e.ExcluidoPor = _usuarioAtual.UsuarioId;
        e.AtualizadoEm = agora;
        e.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task GarantirNomeUnicoAsync(string nome, Guid? ignorarId, CancellationToken ct)
    {
        var duplicado = await _db.Especialidades.AsNoTracking().AnyAsync(
            e => e.Nome == nome && e.ExcluidoEm == null && (ignorarId == null || e.Id != ignorarId),
            ct);
        if (duplicado)
        {
            throw new ConflitoException("especialidade.nome_duplicado", $"Já existe especialidade com o nome '{nome}'.");
        }
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
