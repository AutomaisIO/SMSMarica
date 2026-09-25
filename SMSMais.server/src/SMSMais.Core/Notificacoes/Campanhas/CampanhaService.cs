using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.Notificacoes.Campanhas;

public sealed record CampanhaDto(
    Guid Id,
    string Nome,
    Guid UnidadeId,
    string? UnidadeNome,
    DateTime InicioEm,
    DateTime FimEm,
    string LocalNome,
    string LocalEndereco,
    bool ExigirConferenciaCadastral,
    bool EnvioAutomatico,
    bool Ativa,
    DateTime CriadoEm,
    DateTime? AtualizadoEm);

public sealed record SalvarCampanhaRequest(
    string Nome,
    Guid UnidadeId,
    DateTime InicioEm,
    DateTime FimEm,
    string LocalNome,
    string LocalEndereco,
    bool ExigirConferenciaCadastral,
    bool EnvioAutomatico,
    bool Ativa);

/// <summary>Quem sai no botão "Enviar" da campanha.</summary>
public enum ModoEnvioCampanha
{
    /// <summary>Agendamentos do período que ainda não receberam nada (ou estão empilhados
    /// esperando a janela de horário): entram/saem agora.</summary>
    NaoEnviados = 1,

    /// <summary>Já receberam e não responderam (nem confirmaram, nem avisaram que não vão): a
    /// mensagem é refeita do zero — link novo, o anterior revogado.</summary>
    NaoRespondidos = 2,
}

public sealed record EnviarCampanhaRequest(ModoEnvioCampanha Modo);

public sealed record EnvioCampanhaResultadoDto(int Enfileirados);

public sealed record CampanhaAlcanceItemDto(
    Guid SolicitacaoId,
    string? CodigoSolicitacao,
    string? PacienteNome,
    DateTime? DataAgendada,
    string? Procedimento,
    Guid? ComunicacaoId,
    string? Telefone,
    string? StatusComunicacao,
    DateTime? EnviadoEm,
    DateTime? EntregueEm,
    DateTime? LidoEm,
    DateTime? VisualizadoEm,
    string? MotivoFalha,
    string StatusConfirmacao,
    string? ConfirmadoCanal);

/// <summary>Contagens do período, na ordem do funil.</summary>
public sealed record CampanhaAlcanceTotaisDto(
    int Agendados,
    int SemMensagem,
    int NaFila,
    int Enviados,
    int Entregues,
    int Lidos,
    int Confirmados,
    int NaoVao,
    int SemResposta,
    int Falhas,
    int SemTelefone);

public sealed record CampanhaAlcanceDto(CampanhaAlcanceTotaisDto Totais, IReadOnlyList<CampanhaAlcanceItemDto> Itens);

public interface ICampanhaService
{
    Task<IReadOnlyList<CampanhaDto>> ListarAsync(CancellationToken ct = default);
    Task<CampanhaDto> ObterAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CriarAsync(SalvarCampanhaRequest req, CancellationToken ct = default);
    Task AtualizarAsync(Guid id, SalvarCampanhaRequest req, CancellationToken ct = default);
    Task ExcluirAsync(Guid id, CancellationToken ct = default);

    /// <summary>Agendamentos da campanha com o estado da mensagem e da resposta de cada um.</summary>
    Task<CampanhaAlcanceDto> AlcanceAsync(Guid id, CancellationToken ct = default);

    /// <summary>Botão "Enviar": enfileira (ou refaz) a mensagem de quem se encaixa no modo, fora
    /// da janela de horário — foi alguém que clicou. O worker envia no ritmo configurado.</summary>
    Task<EnvioCampanhaResultadoDto> EnviarAsync(Guid id, EnviarCampanhaRequest req, CancellationToken ct = default);
}

/// <summary>
/// O que a campanha diz sobre um agendamento — resolvido direto no banco (sem DI) porque o envio
/// e a importação precisam dele e o serviço da campanha depende do de comunicação.
/// </summary>
public sealed record CampanhaVigente(
    Guid Id, string LocalNome, string LocalEndereco, bool ExigirConferenciaCadastral, bool EnvioAutomatico);

public static class CampanhaResolver
{
    /// <summary>Campanha ativa da unidade cujo período contém a data do agendamento, ou null.</summary>
    public static async Task<CampanhaVigente?> VigenteAsync(
        SmsMaisDbContext db, Guid? unidadeExecutanteId, DateTime? dataAgendadaUtc, CancellationToken ct = default)
    {
        if (unidadeExecutanteId is not { } unidadeId || dataAgendadaUtc is not { } quando) return null;
        return await db.Campanhas.AsNoTracking()
            .Where(c => c.Ativa && c.ExcluidoEm == null && c.UnidadeId == unidadeId
                && c.InicioEm <= quando && c.FimEm >= quando)
            .OrderByDescending(c => c.InicioEm)
            .Select(c => new CampanhaVigente(
                c.Id, c.LocalNome, c.LocalEndereco, c.ExigirConferenciaCadastral, c.EnvioAutomatico))
            .FirstOrDefaultAsync(ct);
    }
}

public sealed class CampanhaService(
    SmsMaisDbContext db,
    IComunicacaoPacienteService comunicacoes,
    IPacienteResolver pacienteResolver,
    IUsuarioAtualAccessor usuarioAtual) : ICampanhaService
{
    public async Task<IReadOnlyList<CampanhaDto>> ListarAsync(CancellationToken ct = default)
    {
        var lista = await db.Campanhas.AsNoTracking()
            .Include(c => c.Unidade)
            .Where(c => c.ExcluidoEm == null)
            .OrderByDescending(c => c.InicioEm)
            .ToListAsync(ct);
        return lista.Select(Mapear).ToList();
    }

    public async Task<CampanhaDto> ObterAsync(Guid id, CancellationToken ct = default)
        => Mapear(await CarregarAsync(id, ct, rastrear: false));

    public async Task<Guid> CriarAsync(SalvarCampanhaRequest req, CancellationToken ct = default)
    {
        await ValidarAsync(null, req, ct);
        var c = new Campanha
        {
            Id = Guid.CreateVersion7(),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        Aplicar(c, req);
        db.Campanhas.Add(c);
        await db.SaveChangesAsync(ct);
        return c.Id;
    }

    public async Task AtualizarAsync(Guid id, SalvarCampanhaRequest req, CancellationToken ct = default)
    {
        var c = await CarregarAsync(id, ct, rastrear: true);
        await ValidarAsync(id, req, ct);
        Aplicar(c, req);
        c.AtualizadoEm = DateTime.UtcNow;
        c.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var c = await CarregarAsync(id, ct, rastrear: true);
        c.ExcluidoEm = DateTime.UtcNow;
        c.ExcluidoPor = usuarioAtual.UsuarioId;
        c.Ativa = false;
        await db.SaveChangesAsync(ct);
    }

    public async Task<CampanhaAlcanceDto> AlcanceAsync(Guid id, CancellationToken ct = default)
    {
        var c = await CarregarAsync(id, ct, rastrear: false);

        var solicitacoes = await QuerySolicitacoes(c)
            .AsNoTracking()
            .Include(s => s.ExameImagem!).ThenInclude(e => e.TipoExame)
            .OrderBy(s => s.DataAgendada)
            .ToListAsync(ct);
        var ids = solicitacoes.Select(s => s.Id).ToList();

        var comunicacoesPorSolicitacao = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(n => n.SolicitacaoId != null && ids.Contains(n.SolicitacaoId.Value)
                && n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento)
            .ToDictionaryAsync(n => n.SolicitacaoId!.Value, ct);

        var nomes = await pacienteResolver.ResolverManyAsync(solicitacoes.Select(s => s.PacienteId).Distinct(), ct);

        var itens = solicitacoes.Select(s =>
        {
            comunicacoesPorSolicitacao.TryGetValue(s.Id, out var n);
            return new CampanhaAlcanceItemDto(
                s.Id,
                s.CodigoSolicitacao,
                nomes.TryGetValue(s.PacienteId, out var p) ? p.Nome : null,
                s.DataAgendada,
                s.ExameImagem?.TipoExame?.Nome ?? s.EspecialidadeTexto ?? s.ProcedimentoTexto,
                n?.Id,
                n?.Telefone,
                n?.Status.ToString(),
                n?.EnviadoEm,
                n?.EntregueEm,
                n?.LidoEm,
                n?.VisualizadoEm,
                n?.MotivoFalha,
                s.StatusConfirmacao.ToString(),
                s.ConfirmadoCanal);
        }).ToList();

        static bool Saiu(CampanhaAlcanceItemDto i) => i.EnviadoEm is not null;
        var totais = new CampanhaAlcanceTotaisDto(
            Agendados: itens.Count,
            SemMensagem: itens.Count(i => i.ComunicacaoId is null),
            NaFila: itens.Count(i => i.StatusComunicacao == nameof(StatusComunicacao.Pendente)),
            Enviados: itens.Count(Saiu),
            Entregues: itens.Count(i => i.EntregueEm is not null || i.LidoEm is not null),
            Lidos: itens.Count(i => i.LidoEm is not null || i.VisualizadoEm is not null),
            Confirmados: itens.Count(i => i.StatusConfirmacao == nameof(StatusConfirmacaoAgendamento.Confirmada)),
            NaoVao: itens.Count(i => i.StatusConfirmacao == nameof(StatusConfirmacaoAgendamento.Cancelada)),
            SemResposta: itens.Count(i => Saiu(i) && i.StatusConfirmacao == nameof(StatusConfirmacaoAgendamento.Pendente)),
            Falhas: itens.Count(i => i.StatusComunicacao is nameof(StatusComunicacao.Falha)
                or nameof(StatusComunicacao.AguardandoCorrecaoContato)),
            SemTelefone: itens.Count(i => i.StatusComunicacao == nameof(StatusComunicacao.SemTelefoneValido)));

        return new CampanhaAlcanceDto(totais, itens);
    }

    public async Task<EnvioCampanhaResultadoDto> EnviarAsync(
        Guid id, EnviarCampanhaRequest req, CancellationToken ct = default)
    {
        var c = await CarregarAsync(id, ct, rastrear: false);
        if (!c.Ativa)
            throw new ConflitoException("campanha.inativa", "A campanha está desligada — ligue-a antes de enviar.");

        var agora = DateTime.UtcNow;
        // Só o que ainda vai acontecer e segue de pé: mensagem sobre agendamento passado ou
        // cancelado é a única que não tem conserto.
        var solicitacoes = await QuerySolicitacoes(c)
            .Where(s => s.DataAgendada > agora && s.Status != StatusSolicitacao.Cancelada)
            .ToListAsync(ct);
        var ids = solicitacoes.Select(s => s.Id).ToList();

        var existentes = await db.ComunicacoesPaciente
            .Where(n => n.SolicitacaoId != null && ids.Contains(n.SolicitacaoId.Value)
                && n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento)
            .ToDictionaryAsync(n => n.SolicitacaoId!.Value, ct);

        var enfileirados = 0;
        foreach (var s in solicitacoes)
        {
            existentes.TryGetValue(s.Id, out var n);

            if (req.Modo == ModoEnvioCampanha.NaoEnviados)
            {
                if (n is null)
                {
                    if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente) continue;
                    await comunicacoes.EnfileirarAsync(s, FinalidadeComunicacao.ConfirmacaoAgendamento, ct);
                    var nova = db.ChangeTracker.Entries<ComunicacaoPaciente>()
                        .Select(e => e.Entity)
                        .FirstOrDefault(x => x.SolicitacaoId == s.Id
                            && x.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento);
                    if (nova is null) continue; // a fila recusou (regra de origem, data)
                    nova.IgnorarJanelaHorario = true;
                    nova.Origem = OrigemComunicacao.Manual;
                    nova.EnviadoPor = usuarioAtual.UsuarioId;
                    enfileirados++;
                }
                else if (n.Status == StatusComunicacao.Pendente && n.EnviadoEm is null)
                {
                    // Empilhada esperando a janela: sai agora.
                    n.IgnorarJanelaHorario = true;
                    n.ProximaTentativaEm = agora;
                    n.AtualizadoEm = agora;
                    enfileirados++;
                }
                continue;
            }

            // NaoRespondidos: já saiu e o paciente não disse nada.
            if (n is null || n.EnviadoEm is null) continue;
            if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente) continue;
            // Número negado, sem celular e atendimento humano em curso não se reenviam por aqui:
            // cada um tem o seu caminho (cadastro, pendência, a própria atendente).
            if (n.Status is StatusComunicacao.SemTelefoneValido
                or StatusComunicacao.AguardandoCorrecaoContato
                or StatusComunicacao.SubstituidaPorAtendente
                or StatusComunicacao.Pendente)
                continue;

            await comunicacoes.RevogarAcessosAsync(s.Id, agora, ct);
            ComunicacaoPacienteService.RearmarParaNovoEnvio(n);
            n.IgnorarJanelaHorario = true;
            n.Origem = OrigemComunicacao.Manual;
            n.EnviadoPor = usuarioAtual.UsuarioId;
            db.ContatosRegistro.Add(new ContatoRegistro
            {
                Id = Guid.CreateVersion7(),
                SolicitacaoId = s.Id,
                PacienteId = s.PacienteId,
                Meio = MeioContato.WhatsApp,
                Resultado = ResultadoContato.Outro,
                Observacao = $"Reenvio pela campanha \"{c.Nome}\" (sem resposta à mensagem anterior; link anterior revogado).",
                CriadoEm = agora,
                CriadoPor = usuarioAtual.UsuarioId,
            });
            enfileirados++;
        }

        await db.SaveChangesAsync(ct);
        return new EnvioCampanhaResultadoDto(enfileirados);
    }

    /// <summary>Agendamentos que a campanha cobre: unidade executante + período, não excluídos.</summary>
    private IQueryable<Solicitacao> QuerySolicitacoes(Campanha c) =>
        db.Solicitacoes.Where(s => s.ExcluidoEm == null
            && s.UnidadeExecutanteId == c.UnidadeId
            && s.DataAgendada >= c.InicioEm && s.DataAgendada <= c.FimEm);

    private async Task<Campanha> CarregarAsync(Guid id, CancellationToken ct, bool rastrear)
    {
        var q = db.Campanhas.Include(c => c.Unidade).Where(c => c.Id == id && c.ExcluidoEm == null);
        if (!rastrear) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(ct) ?? throw new NaoEncontradoException(nameof(Campanha), id);
    }

    private async Task ValidarAsync(Guid? id, SalvarCampanhaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nome))
            throw new ValidacaoException("nome", "Dê um nome à campanha.");
        if (string.IsNullOrWhiteSpace(req.LocalNome))
            throw new ValidacaoException("localNome", "Informe o nome do local que o paciente vai ver.");
        if (string.IsNullOrWhiteSpace(req.LocalEndereco))
            throw new ValidacaoException("localEndereco", "Informe o endereço do local.");
        if (req.FimEm <= req.InicioEm)
            throw new ValidacaoException("fimEm", "O fim da campanha precisa ser depois do início.");
        if (!await db.Unidades.AnyAsync(u => u.Id == req.UnidadeId, ct))
            throw new ValidacaoException("unidadeId", "Unidade não encontrada.");

        if (req.Ativa)
        {
            var sobreposta = await db.Campanhas.AsNoTracking()
                .Where(c => c.Id != id && c.Ativa && c.ExcluidoEm == null && c.UnidadeId == req.UnidadeId
                    && c.InicioEm <= req.FimEm && c.FimEm >= req.InicioEm)
                .Select(c => c.Nome)
                .FirstOrDefaultAsync(ct);
            if (sobreposta is not null)
                throw new ConflitoException("campanha.sobreposta",
                    $"Já existe a campanha ativa \"{sobreposta}\" para esta unidade no mesmo período.");
        }
    }

    private static void Aplicar(Campanha c, SalvarCampanhaRequest req)
    {
        c.Nome = req.Nome.Trim();
        c.UnidadeId = req.UnidadeId;
        c.InicioEm = DateTime.SpecifyKind(req.InicioEm.ToUniversalTime(), DateTimeKind.Utc);
        c.FimEm = DateTime.SpecifyKind(req.FimEm.ToUniversalTime(), DateTimeKind.Utc);
        c.LocalNome = req.LocalNome.Trim();
        c.LocalEndereco = req.LocalEndereco.Trim();
        c.ExigirConferenciaCadastral = req.ExigirConferenciaCadastral;
        c.EnvioAutomatico = req.EnvioAutomatico;
        c.Ativa = req.Ativa;
    }

    private static CampanhaDto Mapear(Campanha c) => new(
        c.Id, c.Nome, c.UnidadeId, c.Unidade?.Nome, c.InicioEm, c.FimEm, c.LocalNome, c.LocalEndereco,
        c.ExigirConferenciaCadastral, c.EnvioAutomatico, c.Ativa, c.CriadoEm, c.AtualizadoEm);
}
