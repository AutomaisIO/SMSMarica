using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Conversas.RespostasRapidas.Dtos;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Conversas.RespostasRapidas;

public sealed class RespostaRapidaService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IUsuarioUnidadeService vinculos,
    IPacientesService pacientes) : IRespostaRapidaService
{
    public async Task<IReadOnlyList<RespostaRapidaDto>> ListarAsync(
        bool incluirInativas, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var minhasUnidades = await vinculos.ObterUnidadeIdsAsync(me, ct);

        var query = db.RespostasRapidas
            .AsNoTracking()
            .Include(r => r.Campos)
            .Include(r => r.Unidade)
            .Where(r => r.ExcluidoEm == null)
            // Global (sem unidade) todo mundo vê; a da unidade, só quem é dela.
            .Where(r => r.UnidadeId == null || minhasUnidades.Contains(r.UnidadeId.Value));

        if (!incluirInativas) query = query.Where(r => r.Ativo);

        var achados = await query
            .OrderBy(r => r.Ordem)
            .ThenBy(r => r.Titulo)
            .ToListAsync(ct);

        return [.. achados.Select(ParaDto)];
    }

    public async Task<RespostaRapidaDto> ObterAsync(Guid id, CancellationToken ct = default) =>
        ParaDto(await CarregarAsync(id, ct));

    public async Task<Guid> CriarAsync(SalvarRespostaRapidaRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        Validar(request);

        var agora = DateTime.UtcNow;
        var entidade = new RespostaRapida
        {
            Id = Guid.CreateVersion7(),
            Titulo = request.Titulo.Trim(),
            Corpo = request.Corpo.Trim(),
            Categoria = string.IsNullOrWhiteSpace(request.Categoria) ? null : request.Categoria.Trim(),
            UnidadeId = request.UnidadeId,
            Ativo = request.Ativo,
            Ordem = request.Ordem,
            CriadoEm = agora,
            CriadoPor = me,
            Campos = [.. request.Campos.Select(ParaEntidade)],
        };

        db.RespostasRapidas.Add(entidade);
        await db.SaveChangesAsync(ct);
        return entidade.Id;
    }

    public async Task AtualizarAsync(Guid id, SalvarRespostaRapidaRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        Validar(request);

        var entidade = await CarregarAsync(id, ct, rastrear: true);

        entidade.Titulo = request.Titulo.Trim();
        entidade.Corpo = request.Corpo.Trim();
        entidade.Categoria = string.IsNullOrWhiteSpace(request.Categoria) ? null : request.Categoria.Trim();
        entidade.UnidadeId = request.UnidadeId;
        entidade.Ativo = request.Ativo;
        entidade.Ordem = request.Ordem;
        entidade.AtualizadoEm = DateTime.UtcNow;
        entidade.AtualizadoPor = me;

        // Os campos são a declaração das variáveis, não têm vida própria: troca por inteiro.
        db.RespostaRapidaCampos.RemoveRange(entidade.Campos);
        entidade.Campos = [.. request.Campos.Select(ParaEntidade)];

        await db.SaveChangesAsync(ct);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var entidade = await CarregarAsync(id, ct, rastrear: true);

        entidade.ExcluidoEm = DateTime.UtcNow;
        entidade.ExcluidoPor = me;
        await db.SaveChangesAsync(ct);
    }

    public IReadOnlyList<TagAutomaticaDto> ListarTagsAutomaticas() =>
        [.. TagsRespostaRapida.Automaticas.Select(t => new TagAutomaticaDto(t.Nome, t.Descricao))];

    public async Task<TextoResolvidoDto> ResolverAsync(
        Guid conversaId,
        Guid respostaRapidaId,
        ResolverRespostaRapidaRequest request,
        CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var resposta = await CarregarAsync(respostaRapidaId, ct);

        var conversa = await db.Conversas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversaId && c.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Conversa", conversaId);

        var contexto = await MontarContextoAsync(conversa, me, ct);
        var tipos = resposta.Campos.ToDictionary(c => c.Nome, c => c.Tipo, StringComparer.OrdinalIgnoreCase);
        var valores = new Dictionary<string, string>(request.Valores, StringComparer.OrdinalIgnoreCase);

        var texto = TagsRespostaRapida.Resolver(resposta.Corpo, contexto, valores, tipos);

        // O que sobrou como {{tag}} no texto final: automática sem dado no cadastro (paciente
        // sem CNS) ou campo manual em branco. A UI mostra antes de o operador enviar.
        var pendentes = TagsRespostaRapida.Extrair(texto);

        return new TextoResolvidoDto(texto, pendentes);
    }

    private async Task<ContextoTags> MontarContextoAsync(Conversa conversa, Guid operadorId, CancellationToken ct)
    {
        string? nome = conversa.NomeContato, cpf = null, cns = null;
        DateOnly? nascimento = null;

        if (conversa.PacienteId is { } pacienteId)
        {
            // O cadastro é a fonte: o nome do perfil do WhatsApp pode ser apelido.
            var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
            nome = p.NomeCompleto;
            cpf = p.Cpf;
            cns = p.Cns;
            nascimento = p.DataNascimento;
        }

        var operador = await db.Usuarios
            .Where(u => u.Id == operadorId)
            .Select(u => u.NomeCompleto)
            .FirstOrDefaultAsync(ct);

        var unidade = conversa.UnidadeId is { } unidadeId
            ? await db.Unidades.Where(u => u.Id == unidadeId).Select(u => u.Nome).FirstOrDefaultAsync(ct)
            : null;

        return new ContextoTags(
            nome, cpf, cns, nascimento, conversa.TelefoneCanonical, operador, unidade);
    }

    private async Task<RespostaRapida> CarregarAsync(Guid id, CancellationToken ct, bool rastrear = false)
    {
        var query = db.RespostasRapidas.Include(r => r.Campos).Include(r => r.Unidade).AsQueryable();
        if (!rastrear) query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(r => r.Id == id && r.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Resposta rápida", id);
    }

    private static void Validar(SalvarRespostaRapidaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo))
            throw new ValidacaoException("titulo", "Informe o título do atalho.");
        if (string.IsNullOrWhiteSpace(request.Corpo))
            throw new ValidacaoException("corpo", "A mensagem não pode ser vazia.");

        var duplicado = request.Campos
            .GroupBy(c => c.Nome.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicado is not null)
            throw new ValidacaoException("campos", $"A variável {{{{{duplicado.Key}}}}} está declarada duas vezes.");

        foreach (var campo in request.Campos)
        {
            if (string.IsNullOrWhiteSpace(campo.Nome))
                throw new ValidacaoException("campos", "Toda variável precisa de um nome.");

            if (TagsRespostaRapida.EhAutomatica(campo.Nome))
                throw new ValidacaoException("campos",
                    $"{{{{{campo.Nome}}}}} é uma tag automática — o sistema já preenche, não declare como campo.");

            if (!campo.Nome.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new ValidacaoException("campos",
                    $"A variável \"{campo.Nome}\" só pode ter letras, números e _ (é assim que ela aparece no texto).");
        }

        // Campo declarado que não aparece no corpo seria pedido ao operador e jogado fora.
        var noCorpo = TagsRespostaRapida.Extrair(request.Corpo);
        var orfao = request.Campos.FirstOrDefault(c =>
            !noCorpo.Contains(c.Nome.Trim(), StringComparer.OrdinalIgnoreCase));
        if (orfao is not null)
            throw new ValidacaoException("campos",
                $"A variável {{{{{orfao.Nome}}}}} não aparece na mensagem — use-a no texto ou remova o campo.");

        // Tag no corpo que não é automática nem declarada: o operador nunca conseguiria preencher.
        var declarados = request.Campos.Select(c => c.Nome.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var desconhecida = noCorpo.FirstOrDefault(t =>
            !TagsRespostaRapida.EhAutomatica(t) && !declarados.Contains(t));
        if (desconhecida is not null)
            throw new ValidacaoException("corpo",
                $"A mensagem usa {{{{{desconhecida}}}}}, que não é uma tag automática nem um campo declarado.");
    }

    private static RespostaRapidaCampo ParaEntidade(RespostaRapidaCampoDto c) => new()
    {
        Id = Guid.CreateVersion7(),
        Nome = c.Nome.Trim(),
        Rotulo = string.IsNullOrWhiteSpace(c.Rotulo) ? null : c.Rotulo.Trim(),
        Tipo = c.Tipo,
        Ordem = c.Ordem,
    };

    private static RespostaRapidaDto ParaDto(RespostaRapida r) => new(
        r.Id,
        r.Titulo,
        r.Corpo,
        r.Categoria,
        r.UnidadeId,
        r.Unidade?.Nome,
        r.Ativo,
        r.Ordem,
        [.. r.Campos
            .OrderBy(c => c.Ordem)
            .Select(c => new RespostaRapidaCampoDto(c.Nome, c.Rotulo, c.Tipo, c.Ordem))],
        [.. TagsRespostaRapida.Extrair(r.Corpo).Where(TagsRespostaRapida.EhAutomatica)]);

    private Guid ExigirUsuario() =>
        usuarioAtual.UsuarioId ?? throw new ValidacaoException("operador", "Operador não identificado na requisição.");
}
