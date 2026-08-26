using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.RoboAtendimento.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.RoboAtendimento;

public sealed class RoboAssuntoService(SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual) : IRoboAssuntoService
{
    public async Task<IReadOnlyList<RoboAssuntoListItemDto>> ListarAsync(
        bool incluirInativos, CancellationToken ct = default)
    {
        var query = db.RoboAssuntos.AsNoTracking()
            .Include(a => a.Comandos)
            .Where(a => a.ExcluidoEm == null);

        if (!incluirInativos) query = query.Where(a => a.Ativo);

        var achados = await query.OrderBy(a => a.Ordem).ThenBy(a => a.Nome).ToListAsync(ct);
        return [.. achados.Select(a => new RoboAssuntoListItemDto(
            a.Id, a.Nome, a.Descricao, a.Ativo, a.Modelo, a.Ordem,
            a.Comandos.Count(c => c.Habilitado)))];
    }

    public async Task<RoboAssuntoDto> ObterAsync(Guid id, CancellationToken ct = default) =>
        ParaDto(await CarregarAsync(id, ct));

    public async Task<Guid> CriarAsync(SalvarRoboAssuntoRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        Validar(request);
        var nome = request.Nome.Trim();
        await GarantirNomeUnicoAsync(nome, null, ct);

        var assunto = new RoboAssunto
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            Descricao = Normalizar(request.Descricao),
            InstrucoesPersona = request.InstrucoesPersona.Trim(),
            Modelo = Normalizar(request.Modelo),
            Ativo = request.Ativo,
            HorarioInicio = request.HorarioInicio,
            HorarioFim = request.HorarioFim,
            DiasSemana = request.DiasSemana,
            MaxInteracoesSemResolver = request.MaxInteracoesSemResolver,
            LimiarConfianca = request.LimiarConfianca,
            EscalonamentoUnidadeId = request.EscalonamentoUnidadeId,
            Ordem = request.Ordem,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = me,
            Condicoes = [.. request.Condicoes.Select(CondicaoParaEntidade)],
            Treinos = [.. request.Treinos.Select(TreinoParaEntidade)],
            Comandos = [.. request.Comandos.Distinct().Select(ComandoParaEntidade)],
        };

        db.RoboAssuntos.Add(assunto);
        await db.SaveChangesAsync(ct);
        return assunto.Id;
    }

    public async Task AtualizarAsync(Guid id, SalvarRoboAssuntoRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        Validar(request);

        var assunto = await CarregarAsync(id, ct, rastrear: true);
        var nome = request.Nome.Trim();
        await GarantirNomeUnicoAsync(nome, id, ct);

        assunto.Nome = nome;
        assunto.Descricao = Normalizar(request.Descricao);
        assunto.InstrucoesPersona = request.InstrucoesPersona.Trim();
        assunto.Modelo = Normalizar(request.Modelo);
        assunto.Ativo = request.Ativo;
        assunto.HorarioInicio = request.HorarioInicio;
        assunto.HorarioFim = request.HorarioFim;
        assunto.DiasSemana = request.DiasSemana;
        assunto.MaxInteracoesSemResolver = request.MaxInteracoesSemResolver;
        assunto.LimiarConfianca = request.LimiarConfianca;
        assunto.EscalonamentoUnidadeId = request.EscalonamentoUnidadeId;
        assunto.Ordem = request.Ordem;
        assunto.AtualizadoEm = DateTime.UtcNow;
        assunto.AtualizadoPor = me;

        // Filhos são declaração, não têm vida própria: troca por inteiro (molde RespostaRapida).
        db.RoboAssuntoCondicoes.RemoveRange(assunto.Condicoes);
        db.RoboAssuntoTreinos.RemoveRange(assunto.Treinos);
        db.RoboAssuntoComandos.RemoveRange(assunto.Comandos);
        assunto.Condicoes = [.. request.Condicoes.Select(CondicaoParaEntidade)];
        assunto.Treinos = [.. request.Treinos.Select(TreinoParaEntidade)];
        assunto.Comandos = [.. request.Comandos.Distinct().Select(ComandoParaEntidade)];

        await db.SaveChangesAsync(ct);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var assunto = await db.RoboAssuntos
            .FirstOrDefaultAsync(a => a.Id == id && a.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Assunto do robô", id);

        var agora = DateTime.UtcNow;
        assunto.ExcluidoEm = agora;
        assunto.ExcluidoPor = me;
        assunto.AtualizadoEm = agora;
        assunto.AtualizadoPor = me;
        await db.SaveChangesAsync(ct);
    }

    public IReadOnlyList<ComandoRoboCatalogoDto> ListarCatalogoComandos() => ComandoRoboCatalogo.Itens;

    private async Task<RoboAssunto> CarregarAsync(Guid id, CancellationToken ct, bool rastrear = false)
    {
        var query = db.RoboAssuntos
            .Include(a => a.Condicoes)
            .Include(a => a.Treinos)
            .Include(a => a.Comandos)
            .Include(a => a.EscalonamentoUnidade)
            .AsQueryable();
        if (!rastrear) query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(a => a.Id == id && a.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Assunto do robô", id);
    }

    private async Task GarantirNomeUnicoAsync(string nome, Guid? ignorarId, CancellationToken ct)
    {
        var duplicado = await db.RoboAssuntos.AsNoTracking().AnyAsync(
            a => a.Nome == nome && a.ExcluidoEm == null && (ignorarId == null || a.Id != ignorarId), ct);
        if (duplicado)
            throw new ConflitoException("robo_assunto.nome_duplicado", $"Já existe um assunto com o nome '{nome}'.");
    }

    private static void Validar(SalvarRoboAssuntoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            throw new ValidacaoException("nome", "Informe o nome do assunto.");
        if (string.IsNullOrWhiteSpace(request.InstrucoesPersona))
            throw new ValidacaoException("instrucoesPersona", "Descreva como o robô deve agir neste assunto.");
        if (request.MaxInteracoesSemResolver < 1)
            throw new ValidacaoException("maxInteracoesSemResolver", "O limite de interações deve ser pelo menos 1.");
        if (request.LimiarConfianca is < 0 or > 1)
            throw new ValidacaoException("limiarConfianca", "A confiança mínima deve estar entre 0 e 1.");
        if (request.DiasSemana is < 0 or > 127)
            throw new ValidacaoException("diasSemana", "Dias da semana inválidos.");

        var invalidos = request.Comandos.Where(c => !ComandoRoboCatalogo.Habilitaveis.Contains(c)).Distinct().ToList();
        if (invalidos.Count > 0)
            throw new ValidacaoException("comandos",
                $"Comando(s) que não podem ser habilitados por assunto: {string.Join(", ", invalidos)}.");

        foreach (var c in request.Condicoes)
            if (string.IsNullOrWhiteSpace(c.Valor))
                throw new ValidacaoException("condicoes", "Toda condição precisa de um valor.");
        foreach (var t in request.Treinos)
            if (string.IsNullOrWhiteSpace(t.Conteudo))
                throw new ValidacaoException("treinos", "Todo treino precisa de conteúdo.");
    }

    private static RoboAssuntoCondicao CondicaoParaEntidade(RoboAssuntoCondicaoDto c) => new()
    {
        Id = Guid.CreateVersion7(),
        Tipo = c.Tipo,
        Valor = c.Valor.Trim(),
        Ativo = c.Ativo,
        Ordem = c.Ordem,
    };

    private static RoboAssuntoTreino TreinoParaEntidade(RoboAssuntoTreinoDto t) => new()
    {
        Id = Guid.CreateVersion7(),
        Tipo = t.Tipo,
        Titulo = string.IsNullOrWhiteSpace(t.Titulo) ? null : t.Titulo.Trim(),
        Conteudo = t.Conteudo.Trim(),
        Ordem = t.Ordem,
        Ativo = t.Ativo,
    };

    private static RoboAssuntoComando ComandoParaEntidade(Data.Entities.Enums.ComandoRobo c) => new()
    {
        Id = Guid.CreateVersion7(),
        Comando = c,
        Habilitado = true,
    };

    private static RoboAssuntoDto ParaDto(RoboAssunto a) => new(
        a.Id,
        a.Nome,
        a.Descricao,
        a.InstrucoesPersona,
        a.Modelo,
        a.Ativo,
        a.HorarioInicio,
        a.HorarioFim,
        a.DiasSemana,
        a.MaxInteracoesSemResolver,
        a.LimiarConfianca,
        a.EscalonamentoUnidadeId,
        a.EscalonamentoUnidade?.Nome,
        a.Ordem,
        [.. a.Condicoes.OrderBy(c => c.Ordem)
            .Select(c => new RoboAssuntoCondicaoDto(c.Tipo, c.Valor, c.Ativo, c.Ordem))],
        [.. a.Treinos.OrderBy(t => t.Ordem)
            .Select(t => new RoboAssuntoTreinoDto(t.Tipo, t.Titulo, t.Conteudo, t.Ordem, t.Ativo))],
        [.. a.Comandos.Where(c => c.Habilitado).Select(c => c.Comando).Distinct()],
        a.CriadoEm);

    private Guid ExigirUsuario() =>
        usuarioAtual.UsuarioId ?? throw new ValidacaoException("operador", "Operador não identificado na requisição.");

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
