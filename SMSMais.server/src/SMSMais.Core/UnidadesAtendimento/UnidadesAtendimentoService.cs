using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Geo;
using SMSMais.Core.Identidade;
using SMSMais.Core.UnidadesAtendimento.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.UnidadesAtendimento;

public sealed class UnidadesAtendimentoService(
    SmsMaisDbContext db,
    IGeocodificadorService geo,
    IUsuarioAtualAccessor usuarioAtual) : IUnidadesAtendimentoService
{
    public async Task<IReadOnlyList<UnidadeAtendimentoListItemDto>> ListarAsync(
        bool incluirInativas, CancellationToken cancellationToken = default)
    {
        var query = db.UnidadesAtendimento.AsNoTracking();
        if (!incluirInativas) query = query.Where(u => u.Ativo);

        var linhas = await query
            .OrderBy(u => u.Nome)
            .Select(u => new
            {
                Unidade = u,
                Ativos = db.Tratamentos.Count(t => t.UnidadeAtendimentoId == u.Id && t.Ativo),
            })
            .ToListAsync(cancellationToken);

        return [.. linhas.Select(l => new UnidadeAtendimentoListItemDto(
            l.Unidade.Id,
            l.Unidade.Nome,
            l.Unidade.Endereco?.Logradouro,
            l.Unidade.Endereco?.Numero,
            l.Unidade.Endereco?.Bairro,
            l.Unidade.Endereco?.Cidade,
            l.Unidade.Endereco?.Uf,
            l.Unidade.Gps is not null,
            l.Unidade.Externa,
            l.Unidade.Ativo,
            l.Ativos))];
    }

    public async Task<IReadOnlyList<UnidadeAtendimentoOpcaoDto>> ListarOpcoesAsync(CancellationToken cancellationToken = default)
    {
        var unidades = await db.UnidadesAtendimento.AsNoTracking()
            .Where(u => u.Ativo)
            .OrderBy(u => u.Nome)
            .ToListAsync(cancellationToken);

        return [.. unidades.Select(u => new UnidadeAtendimentoOpcaoDto(
            u.Id, u.Nome, u.Endereco?.Bairro, u.Endereco?.Cidade, u.Endereco?.Uf, u.Gps is not null))];
    }

    public async Task<UnidadeAtendimentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = await db.UnidadesAtendimento.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(UnidadeAtendimento), id);

        var ativos = await ContarTratamentosAtivosAsync(id, cancellationToken);

        return new UnidadeAtendimentoDto(
            u.Id,
            u.Nome,
            u.Endereco is null ? null : EnderecoDto.ParaDto(u.Endereco),
            u.Telefone,
            u.Observacoes,
            u.Gps?.Latitude,
            u.Gps?.Longitude,
            u.Externa,
            u.Ativo,
            ativos,
            u.CriadoEm,
            u.AtualizadoEm);
    }

    public async Task<Guid> CadastrarAsync(SalvarUnidadeAtendimentoRequest request, CancellationToken cancellationToken = default)
    {
        var nome = NormalizarNome(request.Nome);
        await GarantirNomeLivreAsync(nome, ignorarId: null, cancellationToken);

        var endereco = ParaEndereco(request.Endereco);
        var u = new UnidadeAtendimento
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            Endereco = endereco,
            Telefone = Limpar(request.Telefone),
            Observacoes = Limpar(request.Observacoes),
            Gps = await ResolverGpsAsync(request.Latitude, request.Longitude, endereco, cancellationToken),
            Externa = request.Externa,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };

        db.UnidadesAtendimento.Add(u);
        await db.SaveChangesAsync(cancellationToken);
        return u.Id;
    }

    public async Task AtualizarAsync(Guid id, SalvarUnidadeAtendimentoRequest request, CancellationToken cancellationToken = default)
    {
        var u = await db.UnidadesAtendimento.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(UnidadeAtendimento), id);

        var nome = NormalizarNome(request.Nome);
        await GarantirNomeLivreAsync(nome, ignorarId: id, cancellationToken);

        var endereco = ParaEndereco(request.Endereco);
        u.Nome = nome;
        u.Endereco = endereco;
        u.Telefone = Limpar(request.Telefone);
        u.Observacoes = Limpar(request.Observacoes);
        u.Gps = await ResolverGpsAsync(request.Latitude, request.Longitude, endereco, cancellationToken);
        u.Externa = request.Externa;
        u.AtualizadoEm = DateTime.UtcNow;
        u.AtualizadoPor = usuarioAtual.UsuarioId;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = await db.UnidadesAtendimento.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(UnidadeAtendimento), id);

        if (!u.Ativo)
        {
            throw new ConflitoException("unidade_atendimento.ja_inativa", "Unidade de atendimento já está inativa.");
        }

        var ativos = await ContarTratamentosAtivosAsync(id, cancellationToken);
        if (ativos > 0)
        {
            throw new ConflitoException("unidade_atendimento.tratamentos_ativos",
                $"Há {ativos} tratamento(s) ativo(s) com destino nesta unidade. Encerre-os ou troque o destino antes de desativar.");
        }

        u.Ativo = false;
        u.AtualizadoEm = DateTime.UtcNow;
        u.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = await db.UnidadesAtendimento.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(UnidadeAtendimento), id);

        if (u.Ativo)
        {
            throw new ConflitoException("unidade_atendimento.ja_ativa", "Unidade de atendimento já está ativa.");
        }

        await GarantirNomeLivreAsync(u.Nome, ignorarId: id, cancellationToken);

        u.Ativo = true;
        u.AtualizadoEm = DateTime.UtcNow;
        u.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(cancellationToken);
    }

    private Task<int> ContarTratamentosAtivosAsync(Guid unidadeAtendimentoId, CancellationToken ct) =>
        db.Tratamentos.CountAsync(t => t.UnidadeAtendimentoId == unidadeAtendimentoId && t.Ativo, ct);

    /// <summary>Duas unidades ativas com o mesmo nome deixam o seletor do tratamento ambíguo.</summary>
    private async Task GarantirNomeLivreAsync(string nome, Guid? ignorarId, CancellationToken ct)
    {
        var nomeMinusculo = nome.ToLower();
        var existe = await db.UnidadesAtendimento.AsNoTracking()
            .AnyAsync(u => u.Ativo && u.Id != ignorarId && u.Nome.ToLower() == nomeMinusculo, ct);
        if (existe)
        {
            throw new ConflitoException("unidade_atendimento.nome_duplicado",
                $"Já existe uma unidade de atendimento ativa chamada \"{nome}\".");
        }
    }

    /// <summary>Nome sempre em MAIÚSCULAS e com espaço simples — padrão da equipe do transporte, e
    /// o que deixa "Clínica X" e "CLÍNICA  X" caírem como a mesma unidade na checagem de duplicata.</summary>
    private static string NormalizarNome(string nome) =>
        Regex.Replace(nome.Trim(), @"\s+", " ").ToUpperInvariant();

    private static Endereco ParaEndereco(EnderecoDto? dto)
    {
        var endereco = (dto ?? throw new ValidacaoException("endereco", "Informe o endereço da unidade de atendimento."))
            .ParaEntidade();
        // A coluna guarda só os 8 dígitos; o front pode mandar com máscara.
        endereco.Cep = new string([.. endereco.Cep.Where(char.IsDigit)]);
        return endereco;
    }

    /// <summary>
    /// Pin do mapa tem prioridade; sem ele, geocodifica o endereço. Diferente da unidade de saúde,
    /// aqui a coordenada é obrigatória: destino sem ponto no mapa não entra no cálculo da rota.
    /// </summary>
    private async Task<Gps> ResolverGpsAsync(double? latitude, double? longitude, Endereco endereco, CancellationToken ct)
    {
        if (latitude is not null && longitude is not null)
        {
            return new Gps(latitude.Value, longitude.Value);
        }

        var coord = await geo.GeocodificarAsync(endereco, ct);
        return coord is null
            ? throw new ValidacaoException("localizacao",
                "Não foi possível localizar o endereço automaticamente. Marque o ponto da unidade no mapa.")
            : new Gps(coord.Latitude, coord.Longitude);
    }

    private static string? Limpar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
