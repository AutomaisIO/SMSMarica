using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Conversas;
using SMSMais.Core.Identidade;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.PendenciasCadastro;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.Notificacoes.Confirmacoes;

/// <summary>
/// Atendimento HUMANO das confirmações de agendamento — o que substitui a planilha das atendentes.
///
/// <para>As quatro abas são DERIVADAS do estado que já existe (resposta do paciente na
/// solicitação, comunicação automática, pendência de cadastro) mais a posse humana
/// (<see cref="AtendimentoConfirmacao"/>). Nada aqui escreve no SISREG: o cancelamento é local
/// (fase 1) e a atendente é orientada a cancelar lá pelo navegador — a extensão observa e concilia.</para>
///
/// <para>Regra de ouro pedida pelo produto: depois que uma pessoa entra no circuito, o sistema
/// não tenta mais enviar a confirmação automática daquela solicitação
/// (<see cref="StatusComunicacao.SubstituidaPorAtendente"/>).</para>
/// </summary>
public interface IAtendimentoConfirmacaoService
{
    Task<PaginaAtendimentoDto> ListarAsync(
        AbaAtendimentoConfirmacao aba, string? texto, Guid? unidadeId, string? envio,
        int pagina, int tamanho, CancellationToken ct = default);

    Task<ResumoAbasAtendimentoDto> ResumoAsync(CancellationToken ct = default);

    /// <summary>Os porquês da aba Telefone comprometido, para o painel no topo da lista.</summary>
    Task<MotivosTelefoneComprometidoDto> MotivosTelefoneComprometidoAsync(CancellationToken ct = default);

    /// <summary>As últimas mensagens trocadas com o paciente — o contexto do pedido de cancelamento.</summary>
    Task<IReadOnlyList<MensagemContextoDto>> ConversaAsync(
        Guid solicitacaoId, int quantas = 30, CancellationToken ct = default);

    Task<IReadOnlyList<EventoAtendimentoDto>> HistoricoAsync(Guid solicitacaoId, CancellationToken ct = default);

    Task<IReadOnlyList<AtendenteConfirmacaoDto>> ListarAtendentesAsync(CancellationToken ct = default);

    Task<AcaoAtendimentoResultadoDto> AtenderAsync(Guid solicitacaoId, CancellationToken ct = default);
    Task<AcaoAtendimentoResultadoDto> AssumirAsync(Guid solicitacaoId, CancellationToken ct = default);
    Task<AcaoAtendimentoResultadoDto> TransferirAsync(Guid solicitacaoId, TransferirAtendimentoRequest request, CancellationToken ct = default);
    Task<AcaoAtendimentoResultadoDto> LiberarAsync(Guid solicitacaoId, CancellationToken ct = default);
    Task<AcaoAtendimentoResultadoDto> ConfirmarAsync(Guid solicitacaoId, ConfirmarAtendimentoRequest request, CancellationToken ct = default);
    Task<AcaoAtendimentoResultadoDto> CancelarAsync(Guid solicitacaoId, CancelarAtendimentoRequest request, CancellationToken ct = default);
    Task<AcaoAtendimentoResultadoDto> EnviarParaPendenteAsync(Guid solicitacaoId, PendenteAtendimentoRequest request, CancellationToken ct = default);
    Task<AcaoAtendimentoResultadoDto> ContatoErradoAsync(Guid solicitacaoId, ContatoErradoAtendimentoRequest request, CancellationToken ct = default);
    Task<AcaoAtendimentoResultadoDto> ContatoCorrigidoAsync(Guid solicitacaoId, ContatoCorrigidoAtendimentoRequest request, CancellationToken ct = default);
}

public sealed class AtendimentoConfirmacaoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IPacienteResolver pacienteResolver,
    IConfirmacaoConfiguracaoService configuracao,
    IComunicacaoPacienteService comunicacoes,
    IPendenciaCadastroService pendencias,
    ILogger<AtendimentoConfirmacaoService> logger) : IAtendimentoConfirmacaoService
{
    /// <summary>Canal gravado na solicitação quando a resposta vem pela mão do atendente.</summary>
    public const string CanalAtendente = "atendente";

    // ===================== LEITURA =====================

    public async Task<PaginaAtendimentoDto> ListarAsync(
        AbaAtendimentoConfirmacao aba, string? texto, Guid? unidadeId, string? envio,
        int pagina, int tamanho, CancellationToken ct = default)
    {
        var me = usuarioAtual.UsuarioId;
        var agora = DateTime.UtcNow;
        var query = await QueryDaAbaAsync(aba, ct);

        if (unidadeId is { } u) query = query.Where(s => s.UnidadeExecutanteId == u);

        if (!string.IsNullOrWhiteSpace(envio))
        {
            if (string.Equals(envio, "NaoEnviada", StringComparison.OrdinalIgnoreCase))
                query = query.Where(s => !db.ComunicacoesPaciente.Any(c =>
                    c.SolicitacaoId == s.Id && c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento));
            else if (Enum.TryParse<StatusComunicacao>(envio, ignoreCase: true, out var st))
                query = query.Where(s => db.ComunicacoesPaciente.Any(c =>
                    c.SolicitacaoId == s.Id && c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento && c.Status == st));
        }

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            var ids = await pacienteResolver.BuscarIdsPorTermoAsync(t, 200, ct);
            query = query.Where(s => ids.Contains(s.PacienteId)
                || (s.CodigoSolicitacao != null && s.CodigoSolicitacao.Contains(t)));
        }

        var total = await query.CountAsync(ct);
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 200);

        var linhas = await query
            .OrderBy(s => s.DataAgendada)
            .ThenBy(s => s.CodigoSolicitacao)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(s => new
            {
                s.Id,
                ExameId = s.ExameImagem != null ? (Guid?)s.ExameImagem.Id : null,
                s.CodigoSolicitacao,
                s.PacienteId,
                s.Categoria,
                Procedimento = s.ExameImagem != null && s.ExameImagem.TipoExame != null
                    ? s.ExameImagem.TipoExame.Nome
                    : s.EspecialidadeTexto ?? s.ProcedimentoTexto,
                s.UnidadeExecutanteId,
                Unidade = s.UnidadeExecutante != null ? s.UnidadeExecutante.Nome : null,
                s.DataAgendada,
                s.StatusConfirmacao,
                s.ConfirmadoCanal,
                s.ConfirmadoEm,
                s.ConfirmacaoCanceladaEm,
                s.MotivoCancelamentoPaciente,
                Envio = db.ComunicacoesPaciente
                    .Where(c => c.SolicitacaoId == s.Id && c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento)
                    .Select(c => new EnvioConfirmacaoDto(
                        c.Id, c.Status.ToString(), c.MotivoFalha,
                        c.MensagemWhatsApp != null ? c.MensagemWhatsApp.ErroMeta : null,
                        c.Tentativas, c.ProximaTentativaEm, c.EnviadoEm, c.EntregueEm, c.LidoEm, c.VisualizadoEm,
                        c.Telefone))
                    .FirstOrDefault(),
                Atendimento = db.AtendimentosConfirmacao
                    .Where(a => a.SolicitacaoId == s.Id && a.EncerradoEm == null)
                    .Select(a => new
                    {
                        a.Id, a.AtendenteUsuarioId,
                        AtendenteNome = a.Atendente != null ? a.Atendente.NomeCompleto : "Atendente",
                        a.Situacao, a.Motivo, a.IniciadoEm, a.AtualizadoEm,
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var pacienteIds = linhas.Select(l => l.PacienteId).Distinct().ToList();
        var resumos = await pacienteResolver.ResolverManyAsync(pacienteIds, ct);

        var negados = await db.PendenciasCadastro.AsNoTracking()
            .Where(p => p.Status == StatusPendenciaCadastro.Aberta && p.PacienteId != null && pacienteIds.Contains(p.PacienteId.Value))
            .Select(p => p.PacienteId!.Value)
            .ToHashSetAsync(ct);

        // O porquê de o canal não alcançar, por paciente. Quando há mais de uma marca aberta vale a
        // mais recente — é a que descreve o número que o cadastro usa hoje.
        var comprometidos = (await db.ContatosComprometidos.AsNoTracking()
                .Where(c => c.ResolvidoEm == null && pacienteIds.Contains(c.PacienteId))
                .Select(c => new { c.PacienteId, c.Motivo, c.Ocorrencias, c.UltimaOcorrenciaEm })
                .ToListAsync(ct))
            .GroupBy(c => c.PacienteId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UltimaOcorrenciaEm).First());

        // Janela de 24h: conversa viva do paciente (ou do telefone para onde a confirmação foi).
        var telefones = linhas.Select(l => l.Envio?.Telefone).Where(t => !string.IsNullOrEmpty(t)).Cast<string>()
            .Concat(resumos.Values.Select(r => r.Celular ?? r.TelefoneVerificado).Where(t => !string.IsNullOrEmpty(t))
                .Select(t => TelefoneWhatsApp.Canonizar(t!)))
            .Distinct()
            .ToList();
        var conversasVivas = await db.Conversas.AsNoTracking()
            .Where(c => c.ExcluidoEm == null
                && (c.Status == StatusConversa.Aberta || c.Status == StatusConversa.Pendente)
                && c.JanelaExpiraEm != null && c.JanelaExpiraEm > agora
                && ((c.PacienteId != null && pacienteIds.Contains(c.PacienteId.Value))
                    || telefones.Contains(c.TelefoneCanonical)))
            .Select(c => new { c.Id, c.PacienteId, c.TelefoneCanonical })
            .ToListAsync(ct);

        var itens = linhas.Select(l =>
        {
            resumos.TryGetValue(l.PacienteId, out var r);
            var fone = l.Envio?.Telefone ?? r?.Celular ?? r?.TelefoneVerificado;
            var foneCanon = fone is null ? null : TelefoneWhatsApp.Canonizar(fone);
            var conversa = conversasVivas.FirstOrDefault(c => c.PacienteId == l.PacienteId)
                ?? (foneCanon is null ? null : conversasVivas.FirstOrDefault(c => c.TelefoneCanonical == foneCanon));

            return new SolicitacaoAtendimentoDto(
                l.Id, l.ExameId, l.CodigoSolicitacao, l.PacienteId, r?.Nome, r?.Cpf, fone,
                r?.TelefoneVerificado is not null && fone is not null && TelefoneWhatsApp.MesmoNumero(r.TelefoneVerificado, fone),
                l.Categoria.ToString(), l.Procedimento, l.UnidadeExecutanteId, l.Unidade, l.DataAgendada,
                l.StatusConfirmacao.ToString(), l.ConfirmadoCanal,
                l.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada ? l.ConfirmacaoCanceladaEm : l.ConfirmadoEm,
                l.MotivoCancelamentoPaciente,
                l.Envio,
                conversa is not null, conversa?.Id,
                negados.Contains(l.PacienteId),
                l.Atendimento is null ? null : new AtendimentoDto(
                    l.Atendimento.Id, l.Atendimento.AtendenteUsuarioId, l.Atendimento.AtendenteNome,
                    l.Atendimento.Situacao.ToString(), l.Atendimento.Motivo, l.Atendimento.IniciadoEm,
                    l.Atendimento.AtualizadoEm, l.Atendimento.AtendenteUsuarioId == me),
                comprometidos.TryGetValue(l.PacienteId, out var cc) ? cc.Motivo.ToString() : null,
                cc?.Ocorrencias ?? 0);
        }).ToList();

        return new PaginaAtendimentoDto(itens, total, pagina, tamanho);
    }

    public async Task<ResumoAbasAtendimentoDto> ResumoAsync(CancellationToken ct = default)
    {
        var me = usuarioAtual.UsuarioId;
        var naoConfirmados = await (await QueryDaAbaAsync(AbaAtendimentoConfirmacao.NaoConfirmados, ct)).CountAsync(ct);
        var confirmados = await (await QueryDaAbaAsync(AbaAtendimentoConfirmacao.Confirmados, ct)).CountAsync(ct);
        var contatoErrado = await (await QueryDaAbaAsync(AbaAtendimentoConfirmacao.ContatoErrado, ct)).CountAsync(ct);
        var pendentes = await (await QueryDaAbaAsync(AbaAtendimentoConfirmacao.Pendentes, ct)).CountAsync(ct);
        var semCanal = await (await QueryDaAbaAsync(AbaAtendimentoConfirmacao.TelefoneComprometido, ct)).CountAsync(ct);
        var cancelamento = await (await QueryDaAbaAsync(AbaAtendimentoConfirmacao.Cancelamento, ct)).CountAsync(ct);
        var comigo = me is null ? 0 : await db.AtendimentosConfirmacao.AsNoTracking()
            .CountAsync(a => a.EncerradoEm == null && a.AtendenteUsuarioId == me
                && a.Situacao == SituacaoAtendimentoConfirmacao.EmAtendimento, ct);
        return new ResumoAbasAtendimentoDto(
            naoConfirmados, confirmados, contatoErrado, pendentes, comigo, semCanal, cancelamento);
    }

    /// <summary>
    /// Quantas solicitações estão paradas por cada motivo — e quantas PESSOAS distintas estão por
    /// trás. Os dois números interessam: solicitação mede o prejuízo (vagas em risco), paciente
    /// mede o trabalho de recepção (cada um é um telefonema, não importa quantos exames tenha).
    /// </summary>
    public async Task<MotivosTelefoneComprometidoDto> MotivosTelefoneComprometidoAsync(CancellationToken ct = default)
    {
        var q = await QueryDaAbaAsync(AbaAtendimentoConfirmacao.TelefoneComprometido, ct);

        var porMotivo = await q
            .Join(db.ContatosComprometidos.Where(c => c.ResolvidoEm == null),
                s => s.PacienteId, c => c.PacienteId, (s, c) => new { s.PacienteId, c.Motivo })
            .GroupBy(x => x.Motivo)
            .Select(g => new { Motivo = g.Key, Solicitacoes = g.Count() })
            .ToListAsync(ct);

        int Do(MotivoContatoComprometido m) =>
            porMotivo.FirstOrDefault(x => x.Motivo == m)?.Solicitacoes ?? 0;

        var total = await q.CountAsync(ct);
        var pessoas = await q.Select(s => s.PacienteId).Distinct().CountAsync(ct);

        return new MotivosTelefoneComprometidoDto(
            Do(MotivoContatoComprometido.SemCelular),
            Do(MotivoContatoComprometido.NaoEhWhatsApp),
            total, pessoas);
    }

    /// <summary>
    /// As últimas mensagens trocadas com o paciente desta solicitação. Existe para a aba
    /// Cancelamento: o pedido chega em texto livre e ambíguo — na varredura de 01→20/09, no meio
    /// dos "quero cancelar" vinham "não quero cancelar", "não pretendo cancelar nenhum exame" e
    /// "qual o motivo do cancelamento?". Cancelar sem ler em volta erra, e errar aqui é tirar a
    /// vaga de quem queria ir.
    /// <para>Busca pelo paciente e, como rede, pelo telefone para onde a confirmação foi — linha
    /// antiga pode não ter <c>paciente_id</c>.</para>
    /// </summary>
    public async Task<IReadOnlyList<MensagemContextoDto>> ConversaAsync(
        Guid solicitacaoId, int quantas = 30, CancellationToken ct = default)
    {
        quantas = Math.Clamp(quantas, 1, 200);

        var alvo = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.Id == solicitacaoId)
            .Select(s => new { s.PacienteId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException("Solicitação", solicitacaoId);

        var fone = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.SolicitacaoId == solicitacaoId && c.Telefone != null)
            .OrderByDescending(c => c.CriadoEm)
            .Select(c => c.Telefone)
            .FirstOrDefaultAsync(ct);

        var msgs = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.PacienteId == alvo.PacienteId || (fone != null && m.Telefone == fone))
            .OrderByDescending(m => m.OcorridoEm)
            .Take(quantas)
            .Select(m => new MensagemContextoDto(
                m.Direcao == DirecaoMensagem.Entrada, m.Conteudo, m.Template, m.OcorridoEm,
                m.AutorNomeExibicao))
            .ToListAsync(ct);

        // Devolve em ordem de leitura (mais antiga primeiro) — é assim que se entende uma conversa.
        msgs.Reverse();
        return msgs;
    }

    public async Task<IReadOnlyList<EventoAtendimentoDto>> HistoricoAsync(Guid solicitacaoId, CancellationToken ct = default)
    {
        var eventos = await db.AtendimentoConfirmacaoEventos.AsNoTracking()
            .Where(e => e.Atendimento!.SolicitacaoId == solicitacaoId)
            .OrderByDescending(e => e.OcorridoEm)
            .Take(200)
            .ToListAsync(ct);
        var ids = eventos.SelectMany(e => new[] { e.AtorUsuarioId, e.DeUsuarioId, e.ParaUsuarioId })
            .Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
        var nomes = await db.Usuarios.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NomeCompleto, ct);
        string? Nome(Guid? id) => id is { } g && nomes.TryGetValue(g, out var n) ? n : null;
        return [.. eventos.Select(e => new EventoAtendimentoDto(
            e.Tipo.ToString(), e.AtorUsuarioId, Nome(e.AtorUsuarioId), e.DeUsuarioId, Nome(e.DeUsuarioId),
            e.ParaUsuarioId, Nome(e.ParaUsuarioId), e.Observacao, e.OcorridoEm))];
    }

    /// <summary>Usuários ativos que têm o módulo Confirmações com edição (por perfil ou acesso global).</summary>
    public async Task<IReadOnlyList<AtendenteConfirmacaoDto>> ListarAtendentesAsync(CancellationToken ct = default)
    {
        var porPerfil = db.UsuariosPerfis
            .Where(up => db.PermissoesPerfil.Any(pp => pp.PerfilId == up.PerfilId
                && pp.Modulo == ModuloPermissao.Confirmacoes
                && (pp.Acoes & AcoesPermissao.Edicao) == AcoesPermissao.Edicao))
            .Select(up => up.UsuarioId);

        return await db.Usuarios.AsNoTracking()
            .Where(u => u.Ativo && u.ExcluidoEm == null && (u.AcessoGlobal || porPerfil.Contains(u.Id)))
            .OrderBy(u => u.NomeCompleto)
            .Select(u => new AtendenteConfirmacaoDto(u.Id, u.NomeCompleto))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Universo comum + recorte da aba. Universo: solicitação viva, agendada de hoje em diante
    /// (Brasília), não cancelada pela equipe; só SISREG quando a régua manda (mesma do lote).
    /// </summary>
    private async Task<IQueryable<Solicitacao>> QueryDaAbaAsync(AbaAtendimentoConfirmacao aba, CancellationToken ct)
    {
        var cfg = await configuracao.ObterAsync(ct);
        var hoje = FusoBrasilia.InicioDoDiaAtualEmUtc();

        var q = db.Solicitacoes.AsNoTracking()
            .Where(s => s.ExcluidoEm == null
                && s.Status != StatusSolicitacao.Cancelada
                && s.DataAgendada != null && s.DataAgendada >= hoje);

        if (cfg.SomenteSisreg)
            q = q.Where(s => s.FonteCriacao == FonteSolicitacao.ImportacaoSisreg
                || s.FonteCriacao == FonteSolicitacao.ExtensaoNavegador
                || (s.FonteCriacao == null && s.RawSisreg != null));

        // Pendência aberta de "número errado" do paciente — tira da fila normal e põe em Contato errado.
        var pacientesNegados = db.PendenciasCadastro
            .Where(p => p.Status == StatusPendenciaCadastro.Aberta && p.PacienteId != null)
            .Select(p => p.PacienteId!.Value);

        // Marca aberta de contato que o canal não alcança (sem celular / não é WhatsApp). Sai da
        // fila de "não confirmados" por um motivo prático: ali a atendente trabalha mandando
        // mensagem, e para estes não adianta — o caminho é ligar.
        var pacientesSemCanal = db.ContatosComprometidos
            .Where(c => c.ResolvidoEm == null)
            .Select(c => c.PacienteId);

        return aba switch
        {
            AbaAtendimentoConfirmacao.NaoConfirmados => q.Where(s =>
                s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente
                && !pacientesNegados.Contains(s.PacienteId)
                && !pacientesSemCanal.Contains(s.PacienteId)
                && !db.AtendimentosConfirmacao.Any(a => a.SolicitacaoId == s.Id && a.EncerradoEm == null
                    && (a.Situacao == SituacaoAtendimentoConfirmacao.Pendente
                        || a.Situacao == SituacaoAtendimentoConfirmacao.ContatoErrado))
                && !db.ComunicacoesPaciente.Any(c => c.SolicitacaoId == s.Id
                    && c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                    && c.Status == StatusComunicacao.AguardandoCorrecaoContato)),

            AbaAtendimentoConfirmacao.Confirmados => q.Where(s =>
                s.StatusConfirmacao == StatusConfirmacaoAgendamento.Confirmada),

            AbaAtendimentoConfirmacao.ContatoErrado => q.Where(s =>
                s.StatusConfirmacao != StatusConfirmacaoAgendamento.Cancelada
                && (pacientesNegados.Contains(s.PacienteId)
                    || db.AtendimentosConfirmacao.Any(a => a.SolicitacaoId == s.Id && a.EncerradoEm == null
                        && a.Situacao == SituacaoAtendimentoConfirmacao.ContatoErrado)
                    || db.ComunicacoesPaciente.Any(c => c.SolicitacaoId == s.Id
                        && c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                        && c.Status == StatusComunicacao.AguardandoCorrecaoContato))),

            AbaAtendimentoConfirmacao.Pendentes => q.Where(s =>
                s.StatusConfirmacao != StatusConfirmacaoAgendamento.Cancelada
                && db.AtendimentosConfirmacao.Any(a => a.SolicitacaoId == s.Id && a.EncerradoEm == null
                    && a.Situacao == SituacaoAtendimentoConfirmacao.Pendente)),

            // Pediram para cancelar e ninguém tratou. O universo comum já exclui solicitação com
            // Status=Cancelada, então sobra exatamente a INTENÇÃO sem o cancelamento.
            AbaAtendimentoConfirmacao.Cancelamento => q.Where(s =>
                s.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada),

            // Quem já confirmou por outro caminho (recepção, app) não precisa ser perseguido,
            // mesmo com o telefone ruim; e quem negou ser o paciente é assunto da outra aba.
            AbaAtendimentoConfirmacao.TelefoneComprometido => q.Where(s =>
                s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente
                && pacientesSemCanal.Contains(s.PacienteId)
                && !pacientesNegados.Contains(s.PacienteId)),

            _ => throw new ValidacaoException("aba", "Aba desconhecida."),
        };
    }

    // ===================== AÇÕES =====================

    public async Task<AcaoAtendimentoResultadoDto> AtenderAsync(Guid solicitacaoId, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var agora = DateTime.UtcNow;
        var s = await CarregarSolicitacaoAsync(solicitacaoId, ct);
        var ativo = await AtivoAsync(solicitacaoId, ct);

        if (ativo is null)
        {
            ativo = Novo(s.Id, me, agora);
            db.AtendimentosConfirmacao.Add(ativo);
            AddEvento(ativo, TipoEventoAtendimentoConfirmacao.Atendido, me, agora, para: me);
        }
        else if (ativo.Situacao == SituacaoAtendimentoConfirmacao.EmAtendimento)
        {
            if (ativo.AtendenteUsuarioId != me)
                throw await ConflitoJaAtendidoAsync(ativo.AtendenteUsuarioId, ct);
            // Já é meu: idempotente.
            return Resultado(ativo);
        }
        else
        {
            // Estacionada (Pendente / Contato errado): quem clica retoma.
            var de = ativo.AtendenteUsuarioId;
            ativo.AtendenteUsuarioId = me;
            ativo.Situacao = SituacaoAtendimentoConfirmacao.EmAtendimento;
            Tocar(ativo, me, agora);
            AddEvento(ativo, TipoEventoAtendimentoConfirmacao.Retomado, me, agora, de: de, para: me);
        }

        await SubstituirEnvioAutomaticoAsync(s.Id, agora, ct);
        await SalvarComTraducaoDeCorridaAsync(ativo, ct);
        return Resultado(ativo);
    }

    public async Task<AcaoAtendimentoResultadoDto> AssumirAsync(Guid solicitacaoId, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var agora = DateTime.UtcNow;
        var ativo = await AtivoAsync(solicitacaoId, ct)
            ?? throw new ConflitoException("atendimento.nao_iniciado", "Ninguém está atendendo esta solicitação — use Atender.");
        if (ativo.AtendenteUsuarioId == me) return Resultado(ativo);

        var de = ativo.AtendenteUsuarioId;
        ativo.AtendenteUsuarioId = me;
        ativo.Situacao = SituacaoAtendimentoConfirmacao.EmAtendimento;
        Tocar(ativo, me, agora);
        AddEvento(ativo, TipoEventoAtendimentoConfirmacao.Assumido, me, agora, de: de, para: me);
        await SalvarComTraducaoDeCorridaAsync(ativo, ct);
        return Resultado(ativo);
    }

    public async Task<AcaoAtendimentoResultadoDto> TransferirAsync(
        Guid solicitacaoId, TransferirAtendimentoRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var agora = DateTime.UtcNow;
        var ativo = await ExigirMeuAsync(solicitacaoId, me, ct);

        var alvo = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == request.ParaUsuarioId && u.Ativo && u.ExcluidoEm == null)
            .Select(u => new { u.Id, u.NomeCompleto })
            .FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException("Usuário", request.ParaUsuarioId);
        if (alvo.Id == me)
            throw new ValidacaoException("atendimento.transferir_para_si", "Escolha outra atendente.");

        var de = ativo.AtendenteUsuarioId;
        ativo.AtendenteUsuarioId = alvo.Id;
        ativo.Situacao = SituacaoAtendimentoConfirmacao.EmAtendimento;
        Tocar(ativo, me, agora);
        AddEvento(ativo, TipoEventoAtendimentoConfirmacao.Transferido, me, agora, de: de, para: alvo.Id,
            observacao: request.Observacao);
        await SalvarComTraducaoDeCorridaAsync(ativo, ct);
        return Resultado(ativo);
    }

    public async Task<AcaoAtendimentoResultadoDto> LiberarAsync(Guid solicitacaoId, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var agora = DateTime.UtcNow;
        var ativo = await ExigirMeuAsync(solicitacaoId, me, ct);
        Encerrar(ativo, SituacaoAtendimentoConfirmacao.Liberado, me, agora);
        AddEvento(ativo, TipoEventoAtendimentoConfirmacao.Liberado, me, agora, de: me);
        await SalvarComTraducaoDeCorridaAsync(ativo, ct);
        return Resultado(ativo);
    }

    public async Task<AcaoAtendimentoResultadoDto> ConfirmarAsync(
        Guid solicitacaoId, ConfirmarAtendimentoRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var agora = DateTime.UtcNow;
        var s = await CarregarSolicitacaoAsync(solicitacaoId, ct);
        var ativo = await GarantirMeuAsync(s.Id, me, agora, ct);

        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
        s.ConfirmadoEm = agora;
        s.ConfirmadoCanal = CanalAtendente;
        s.ConfirmacaoCanceladaEm = null;
        s.MotivoCancelamentoPaciente = null;
        s.AtualizadoEm = agora;
        s.AtualizadoPor = me;

        RegistrarContato(s, LerMeio(request.Meio), ResultadoContato.Atendeu,
            request.Observacao ?? "Presença confirmada pela atendente.", me, agora);
        await SubstituirEnvioAutomaticoAsync(s.Id, agora, ct);

        Encerrar(ativo, SituacaoAtendimentoConfirmacao.Confirmado, me, agora);
        AddEvento(ativo, TipoEventoAtendimentoConfirmacao.Confirmado, me, agora, observacao: request.Observacao);
        await SalvarComTraducaoDeCorridaAsync(ativo, ct);
        return Resultado(ativo);
    }

    /// <summary>
    /// FASE 1: cancela no SMSMais (a vaga volta a contar por derivação — <c>CanceladoEm</c>), encerra
    /// o que ainda ia sair, revoga os links de acesso. O SISREG NÃO é tocado: a resposta carrega
    /// <c>OrientacaoSisreg</c> para a tela mandar cancelar lá pelo navegador (a extensão concilia).
    /// </summary>
    public async Task<AcaoAtendimentoResultadoDto> CancelarAsync(
        Guid solicitacaoId, CancelarAtendimentoRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var agora = DateTime.UtcNow;
        var motivo = (request.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0)
            throw new ValidacaoException("atendimento.motivo_obrigatorio", "Informe o motivo do cancelamento.");
        if (motivo.Length > 500) motivo = motivo[..500];

        var s = await CarregarSolicitacaoAsync(solicitacaoId, ct);
        var ativo = await GarantirMeuAsync(s.Id, me, agora, ct);

        s.Status = StatusSolicitacao.Cancelada;
        s.CanceladoEm = agora;
        s.CanceladoPorUsuarioId = me;
        s.MotivoCancelamento = motivo;
        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Cancelada;
        s.ConfirmacaoCanceladaEm = agora;
        s.ConfirmadoCanal = CanalAtendente;
        s.MotivoCancelamentoPaciente ??= motivo;
        s.AtualizadoEm = agora;
        s.AtualizadoPor = me;

        // Satélite de imagem: espelha o cancelamento (a limpeza da worklist fica com o worker,
        // que varre exames cancelados com WorklistItemUid).
        if (s.ExameImagem is { } exame && exame.Status is StatusSolicitacaoExame.Solicitada
                or StatusSolicitacaoExame.Enviada or StatusSolicitacaoExame.Recebida)
        {
            exame.Status = StatusSolicitacaoExame.Cancelada;
            exame.AtualizadoEm = agora;
            exame.AtualizadoPor = me;
        }

        RegistrarContato(s, LerMeio(request.Meio), ResultadoContato.Atendeu, "Cancelado pela atendente: " + motivo, me, agora);
        await SubstituirEnvioAutomaticoAsync(s.Id, agora, ct, encerrarTudo: true);
        await comunicacoes.RevogarAcessosAsync(s.Id, agora, ct);

        ativo.Motivo = motivo;
        Encerrar(ativo, SituacaoAtendimentoConfirmacao.Cancelado, me, agora);
        AddEvento(ativo, TipoEventoAtendimentoConfirmacao.Cancelado, me, agora, observacao: motivo);
        await SalvarComTraducaoDeCorridaAsync(ativo, ct);

        logger.LogInformation("Solicitação {Solicitacao} cancelada pela atendente {Usuario} (fase 1: só no SMSMais).", s.Id, me);
        return Resultado(ativo) with { OrientacaoSisreg = true };
    }

    public async Task<AcaoAtendimentoResultadoDto> EnviarParaPendenteAsync(
        Guid solicitacaoId, PendenteAtendimentoRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var agora = DateTime.UtcNow;
        var motivo = (request.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0)
            throw new ValidacaoException("atendimento.motivo_obrigatorio", "Informe o motivo da pendência.");
        if (motivo.Length > 500) motivo = motivo[..500];

        var s = await CarregarSolicitacaoAsync(solicitacaoId, ct);
        var ativo = await GarantirMeuAsync(s.Id, me, agora, ct);
        ativo.Situacao = SituacaoAtendimentoConfirmacao.Pendente;
        ativo.Motivo = motivo;
        Tocar(ativo, me, agora);
        RegistrarContato(s, MeioContato.Outro, ResultadoContato.NaoAtendeu, "Pendente: " + motivo, me, agora);
        await SubstituirEnvioAutomaticoAsync(s.Id, agora, ct);
        AddEvento(ativo, TipoEventoAtendimentoConfirmacao.EnviadoPendente, me, agora, observacao: motivo);
        await SalvarComTraducaoDeCorridaAsync(ativo, ct);
        return Resultado(ativo);
    }

    public async Task<AcaoAtendimentoResultadoDto> ContatoErradoAsync(
        Guid solicitacaoId, ContatoErradoAtendimentoRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var agora = DateTime.UtcNow;
        var s = await CarregarSolicitacaoAsync(solicitacaoId, ct);
        var ativo = await GarantirMeuAsync(s.Id, me, agora, ct);

        ativo.Situacao = SituacaoAtendimentoConfirmacao.ContatoErrado;
        ativo.Motivo = LimparObservacao(request.Observacao);
        Tocar(ativo, me, agora);
        RegistrarContato(s, MeioContato.Outro, ResultadoContato.NumeroInvalido,
            request.Observacao ?? "Quem atendeu disse que não é o paciente.", me, agora);
        await SubstituirEnvioAutomaticoAsync(s.Id, agora, ct);
        AddEvento(ativo, TipoEventoAtendimentoConfirmacao.ContatoErrado, me, agora, observacao: request.Observacao);
        await SalvarComTraducaoDeCorridaAsync(ativo, ct);

        // Pendência de cadastro pelo caminho humano (mesma que o robô abre) — carimba o número como
        // negado para este paciente e retém qualquer automático (ADR-0057).
        var fone = await TelefoneDoPacienteAsync(s, ct);
        if (fone is not null)
        {
            await pendencias.RegistrarNumeroErradoAsync(
                null, fone, s.PacienteId, VinculoContato.NaoInformado,
                request.Observacao ?? "Registrado pela atendente no menu Confirmações.", me, ct);
        }
        return Resultado(ativo);
    }

    /// <summary>
    /// A atendente já gravou/verificou o telefone novo (fluxo OTP do painel). Aqui: fecha as
    /// pendências abertas do paciente (o que também solta as comunicações retidas), encerra o
    /// atendimento e devolve a solicitação à fila automática.
    /// </summary>
    public async Task<AcaoAtendimentoResultadoDto> ContatoCorrigidoAsync(
        Guid solicitacaoId, ContatoCorrigidoAtendimentoRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var agora = DateTime.UtcNow;
        var s = await CarregarSolicitacaoAsync(solicitacaoId, ct);

        var abertas = await db.PendenciasCadastro.AsNoTracking()
            .Where(p => p.Status == StatusPendenciaCadastro.Aberta && p.PacienteId == s.PacienteId)
            .Select(p => p.Id)
            .ToListAsync(ct);
        foreach (var id in abertas)
            await pendencias.ResolverAsync(id, "Contato corrigido no menu Confirmações"
                + (string.IsNullOrWhiteSpace(request.Telefone) ? "" : $" ({request.Telefone.Trim()})"), ct);

        // Comunicação que foi encerrada por atendimento humano volta a valer com o número novo.
        var substituidas = await db.ComunicacoesPaciente
            .Where(c => c.SolicitacaoId == s.Id && c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                && c.Status == StatusComunicacao.SubstituidaPorAtendente)
            .ToListAsync(ct);
        foreach (var c in substituidas)
        {
            c.Status = StatusComunicacao.Pendente;
            c.MotivoFalha = null;
            c.Telefone = null;
            c.Tentativas = 0;
            c.ProximaTentativaEm = agora;
            c.AtualizadoEm = agora;
        }

        var ativo = await AtivoAsync(s.Id, ct);
        if (ativo is not null)
        {
            Encerrar(ativo, SituacaoAtendimentoConfirmacao.ContatoCorrigido, me, agora);
            AddEvento(ativo, TipoEventoAtendimentoConfirmacao.ContatoCorrigido, me, agora, observacao: request.Observacao);
        }
        RegistrarContato(s, MeioContato.Outro, ResultadoContato.Outro,
            "Contato corrigido" + (string.IsNullOrWhiteSpace(request.Telefone) ? "." : $": {request.Telefone.Trim()}"), me, agora);
        await db.SaveChangesAsync(ct);
        return ativo is null
            ? new AcaoAtendimentoResultadoDto(Guid.Empty, SituacaoAtendimentoConfirmacao.ContatoCorrigido.ToString())
            : Resultado(ativo);
    }

    // ===================== INTERNOS =====================

    private Guid ExigirUsuario() =>
        usuarioAtual.UsuarioId ?? throw new ValidacaoException("operador", "Operador não identificado na requisição.");

    private async Task<Solicitacao> CarregarSolicitacaoAsync(Guid id, CancellationToken ct) =>
        await db.Solicitacoes.Include(s => s.ExameImagem)
            .FirstOrDefaultAsync(s => s.Id == id && s.ExcluidoEm == null, ct)
        ?? throw new NaoEncontradoException("Solicitação", id);

    private Task<AtendimentoConfirmacao?> AtivoAsync(Guid solicitacaoId, CancellationToken ct) =>
        db.AtendimentosConfirmacao.FirstOrDefaultAsync(a => a.SolicitacaoId == solicitacaoId && a.EncerradoEm == null, ct);

    /// <summary>Ação que exige posse: o ativo tem de ser meu.</summary>
    private async Task<AtendimentoConfirmacao> ExigirMeuAsync(Guid solicitacaoId, Guid me, CancellationToken ct)
    {
        var ativo = await AtivoAsync(solicitacaoId, ct)
            ?? throw new ConflitoException("atendimento.nao_iniciado", "Ninguém está atendendo esta solicitação — use Atender.");
        if (ativo.AtendenteUsuarioId != me)
            throw await ConflitoJaAtendidoAsync(ativo.AtendenteUsuarioId, ct);
        return ativo;
    }

    /// <summary>Desfecho direto (confirmar/cancelar/pendente sem ter clicado em Atender): cria o
    /// atendimento na hora; se já está com outra pessoa, 409.</summary>
    private async Task<AtendimentoConfirmacao> GarantirMeuAsync(Guid solicitacaoId, Guid me, DateTime agora, CancellationToken ct)
    {
        var ativo = await AtivoAsync(solicitacaoId, ct);
        if (ativo is null)
        {
            ativo = Novo(solicitacaoId, me, agora);
            db.AtendimentosConfirmacao.Add(ativo);
            AddEvento(ativo, TipoEventoAtendimentoConfirmacao.Atendido, me, agora, para: me);
            return ativo;
        }
        if (ativo.AtendenteUsuarioId != me && ativo.Situacao == SituacaoAtendimentoConfirmacao.EmAtendimento)
            throw await ConflitoJaAtendidoAsync(ativo.AtendenteUsuarioId, ct);
        if (ativo.AtendenteUsuarioId != me)
        {
            // Estacionada por outra pessoa: quem dá o desfecho retoma.
            AddEvento(ativo, TipoEventoAtendimentoConfirmacao.Retomado, me, agora, de: ativo.AtendenteUsuarioId, para: me);
            ativo.AtendenteUsuarioId = me;
        }
        return ativo;
    }

    private static AtendimentoConfirmacao Novo(Guid solicitacaoId, Guid me, DateTime agora) => new()
    {
        Id = Guid.CreateVersion7(),
        SolicitacaoId = solicitacaoId,
        AtendenteUsuarioId = me,
        Situacao = SituacaoAtendimentoConfirmacao.EmAtendimento,
        IniciadoEm = agora,
        CriadoPor = me,
    };

    private static void Tocar(AtendimentoConfirmacao a, Guid me, DateTime agora)
    {
        a.AtualizadoEm = agora;
        a.AtualizadoPor = me;
    }

    private static void Encerrar(AtendimentoConfirmacao a, SituacaoAtendimentoConfirmacao situacao, Guid me, DateTime agora)
    {
        a.Situacao = situacao;
        a.EncerradoEm = agora;
        Tocar(a, me, agora);
    }

    private void AddEvento(
        AtendimentoConfirmacao a, TipoEventoAtendimentoConfirmacao tipo, Guid ator, DateTime agora,
        Guid? de = null, Guid? para = null, string? observacao = null)
    {
        db.AtendimentoConfirmacaoEventos.Add(new AtendimentoConfirmacaoEvento
        {
            Id = Guid.CreateVersion7(),
            AtendimentoId = a.Id,
            Tipo = tipo,
            AtorUsuarioId = ator,
            DeUsuarioId = de,
            ParaUsuarioId = para,
            Observacao = LimparObservacao(observacao),
            OcorridoEm = agora,
        });
    }

    private void RegistrarContato(Solicitacao s, MeioContato meio, ResultadoContato resultado, string? obs, Guid me, DateTime agora)
    {
        obs = obs?.Trim();
        db.ContatosRegistro.Add(new ContatoRegistro
        {
            Id = Guid.CreateVersion7(),
            SolicitacaoId = s.Id,
            PacienteId = s.PacienteId,
            Meio = meio,
            Resultado = resultado,
            Observacao = string.IsNullOrEmpty(obs) ? null : (obs.Length <= 500 ? obs : obs[..500]),
            CriadoEm = agora,
            CriadoPor = me,
        });
    }

    /// <summary>
    /// Depois que uma pessoa entra no circuito o automático não tenta mais: o que ainda não saiu
    /// (na fila, em retry, retido esperando verificação) vira terminal. O que já foi enviado fica.
    /// Com <paramref name="encerrarTudo"/> (cancelamento), também as outras finalidades pendentes.
    /// </summary>
    private async Task SubstituirEnvioAutomaticoAsync(Guid solicitacaoId, DateTime agora, CancellationToken ct, bool encerrarTudo = false)
    {
        var pendentes = await db.ComunicacoesPaciente
            .Where(c => c.SolicitacaoId == solicitacaoId
                && (encerrarTudo || c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento)
                && (c.Status == StatusComunicacao.Pendente
                    || c.Status == StatusComunicacao.AguardandoVerificacaoCadastral
                    || c.Status == StatusComunicacao.AguardandoCorrecaoContato
                    || c.Status == StatusComunicacao.AguardandoTelefoneVerificado))
            .ToListAsync(ct);
        foreach (var c in pendentes)
        {
            c.Status = StatusComunicacao.SubstituidaPorAtendente;
            c.ProximaTentativaEm = null;
            c.MotivoFalha = encerrarTudo ? "Agendamento cancelado pela atendente." : "Atendimento humano iniciado.";
            c.AtualizadoEm = agora;
        }
    }

    private async Task<string?> TelefoneDoPacienteAsync(Solicitacao s, CancellationToken ct)
    {
        var daComunicacao = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.SolicitacaoId == s.Id && c.Telefone != null)
            .OrderByDescending(c => c.EnviadoEm ?? c.CriadoEm)
            .Select(c => c.Telefone)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrEmpty(daComunicacao)) return TelefoneWhatsApp.Canonizar(daComunicacao);

        var r = await pacienteResolver.ResolverAsync(s.PacienteId, ct);
        var fone = r?.Celular ?? r?.TelefoneVerificado;
        return string.IsNullOrEmpty(fone) ? null : TelefoneWhatsApp.Canonizar(fone);
    }

    private static MeioContato LerMeio(string? meio) =>
        Enum.TryParse<MeioContato>(meio, ignoreCase: true, out var m) ? m : MeioContato.Ligacao;

    private static string? LimparObservacao(string? observacao)
    {
        observacao = observacao?.Trim();
        if (string.IsNullOrEmpty(observacao)) return null;
        return observacao.Length <= 500 ? observacao : observacao[..500];
    }

    private static AcaoAtendimentoResultadoDto Resultado(AtendimentoConfirmacao a) =>
        new(a.Id, a.Situacao.ToString());

    private async Task<ConflitoException> ConflitoJaAtendidoAsync(Guid donoId, CancellationToken ct)
    {
        var nome = await db.Usuarios.AsNoTracking().Where(u => u.Id == donoId).Select(u => u.NomeCompleto).FirstOrDefaultAsync(ct) ?? "outra atendente";
        return new ConflitoException("atendimento.ja_atendido", $"Em atendimento por {nome}. Use \"Assumir\" para pegar.");
    }

    /// <summary>Corrida de posse (xmin) → 409 legível, como na posse de conversa (ADR-0047).</summary>
    private async Task SalvarComTraducaoDeCorridaAsync(AtendimentoConfirmacao a, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var donoAtual = await db.AtendimentosConfirmacao.AsNoTracking()
                .Where(x => x.Id == a.Id)
                .Select(x => (Guid?)x.AtendenteUsuarioId)
                .FirstOrDefaultAsync(ct);
            if (donoAtual is { } dono && dono != usuarioAtual.UsuarioId)
                throw await ConflitoJaAtendidoAsync(dono, ct);
            throw new ConflitoException("atendimento.alterado",
                "O atendimento mudou enquanto você agia — atualize a lista e tente de novo.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("ux_atendimento_confirmacao_ativo") == true)
        {
            var dono = await db.AtendimentosConfirmacao.AsNoTracking()
                .Where(x => x.SolicitacaoId == a.SolicitacaoId && x.EncerradoEm == null)
                .Select(x => (Guid?)x.AtendenteUsuarioId)
                .FirstOrDefaultAsync(ct);
            throw dono is { } d ? await ConflitoJaAtendidoAsync(d, ct)
                : new ConflitoException("atendimento.alterado", "Atualize a lista e tente de novo.");
        }
    }
}
