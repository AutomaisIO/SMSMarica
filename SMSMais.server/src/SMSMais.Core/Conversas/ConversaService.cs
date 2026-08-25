using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Texto;
using SMSMais.Core.Conversas.Dtos;
using SMSMais.Core.Identidade;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Conversas;

public sealed class ConversaService(
    SmsMaisDbContext db,
    IWhatsAppCliente whats,
    IPacientesService pacientes,
    IUsuarioAtualAccessor usuarioAtual,
    IIdentidadeService identidade,
    IUsuarioUnidadeService vinculos,
    IConversaNotificador notificador,
    Pacientes.Fhir.IPacienteResolver pacienteResolver,
    Institucional.IInstituicaoService instituicao,
    IOptions<ConversasOptions> opcoes,
    IConfiguration configuration) : IConversaService
{
    private const int LimiteContatos = 10;

    /// <summary>
    /// Só os templates que fazem sentido para ABRIR conversa (lista branca em
    /// <see cref="ConversasOptions.TemplatesInicioConversa"/>) — os de confirmação/laudo saem
    /// pela fila de comunicações, não pela mão do operador.
    /// </summary>
    public async Task<IReadOnlyList<TemplateWhatsApp>> ListarTemplatesAsync(CancellationToken ct = default)
    {
        var todos = await whats.ListarTemplatesAsync(ct);
        var permitidos = opcoes.Value.TemplatesInicioConversa;
        if (permitidos.Length == 0) return todos;

        return [.. todos.Where(t => permitidos.Contains(t.Nome, StringComparer.OrdinalIgnoreCase))];
    }

    public async Task<IReadOnlyList<ContatoConversaDto>> BuscarContatosAsync(
        string? termo, CancellationToken ct = default)
    {
        termo = termo?.Trim() ?? "";
        if (termo.Length < 3) return [];

        var achados = new List<ContatoConversaDto>();
        var vistos = new HashSet<Guid>();

        // Nº da solicitação (o do SISREG, gravado em codigo_solicitacao). É o caminho que a
        // recepção tem na mão quando liga para o paciente.
        var digitos = new string([.. termo.Where(char.IsDigit)]);
        if (digitos.Length >= 4)
        {
            var solicitacoes = await db.Solicitacoes
                .Where(s => s.ExcluidoEm == null && s.CodigoSolicitacao == digitos)
                .OrderByDescending(s => s.CriadoEm)
                .Select(s => new { s.PacienteId, s.CodigoSolicitacao })
                .Take(LimiteContatos)
                .ToListAsync(ct);

            foreach (var s in solicitacoes)
            {
                if (!vistos.Add(s.PacienteId)) continue;
                var pac = await pacientes.ObterPorIdAsync(s.PacienteId, ct);
                achados.Add(new ContatoConversaDto(
                    pac.Id, pac.NomeCompleto, pac.TelefonePrincipal, pac.Cpf, pac.DataNascimento,
                    $"Solicitação SISREG {s.CodigoSolicitacao}"));
            }
        }

        // Cadastro: nome (qualquer parte), CPF ou CNS — a busca do hub FHIR já cobre os três.
        foreach (var p in await pacientes.BuscarAsync(termo, ct))
        {
            if (achados.Count >= LimiteContatos) break;
            if (!vistos.Add(p.Id)) continue;
            achados.Add(new ContatoConversaDto(
                p.Id, p.NomeCompleto, p.TelefonePrincipal, p.Cpf, p.DataNascimento, "Cadastro"));
        }

        return achados;
    }

    public async Task<IReadOnlyList<PacienteDoTelefoneDto>> ListarPacientesDoTelefoneAsync(
        Guid conversaId, CancellationToken ct = default)
    {
        // ADR-0048: leitura destravada — painel de pacientes do telefone é parte do "ver a thread".
        var conversa = await CarregarVivaAsync(conversaId, rastrear: false, ct);

        var achados = await pacientes.ListarPorTelefoneAsync(conversa.TelefoneCanonical, ct);

        return [.. achados
            .Select(p => new PacienteDoTelefoneDto(
                p.Id, p.NomeCompleto, p.Cpf, p.DataNascimento, p.Id == conversa.PacienteId))
            // O titular primeiro; o resto por nome, para a lista não dançar entre requests.
            .OrderByDescending(p => p.Titular)
            .ThenBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<Guid> IniciarComTemplateAsync(IniciarConversaRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        // DDD do município desta instância (ADR-0043) para completar número digitado sem ele.
        var inst = await instituicao.ObterAsync(ct);
        var interpretado = TelefoneWhatsApp.Interpretar(
            request.Telefone,
            inst.DddPadrao?.ToString() ?? TelefoneWhatsApp.DddPadraoFallback);
        if (!interpretado.Ok) throw new ValidacaoException("telefone", interpretado.Erro!);
        var fone = TelefoneWhatsApp.Canonizar(interpretado.Fone!);

        Guid? pacienteId = request.PacienteId;
        var nome = request.NomeContato;
        if (pacienteId is null)
        {
            var pac = await pacientes.ObterPorTelefoneAsync(fone, ct);
            if (pac is not null) { pacienteId = pac.Id; nome ??= pac.NomeCompleto; }
        }

        var agora = DateTime.UtcNow;
        var conversa = await db.Conversas.FirstOrDefaultAsync(
            c => c.TelefoneCanonical == fone && c.Canal == CanalConversa.WhatsApp && c.ExcluidoEm == null
              && (c.Status == StatusConversa.Aberta || c.Status == StatusConversa.Pendente), ct);

        if (conversa is null)
        {
            conversa = new Conversa
            {
                Id = Guid.CreateVersion7(),
                Canal = CanalConversa.WhatsApp,
                TelefoneCanonical = fone,
                PacienteId = pacienteId,
                NomeContato = nome,
                Assunto = request.Assunto,
                Status = StatusConversa.Pendente,
                OperadorResponsavelId = me,
                UnidadeId = await vinculos.ObterPrincipalIdAsync(me, ct),
                JanelaExpiraEm = null, // abre só quando o cidadão responder
                PrimeiroContatoEm = agora,
                NaoLidas = 0,
                CriadoEm = agora,
                CriadoPor = me,
            };
            db.Conversas.Add(conversa);
            db.ConversaEventos.Add(NovoEvento(conversa.Id, TipoEventoConversa.Criada, me, agora));
        }
        else
        {
            // Disparar template numa conversa viva SEM dono é claim (com evento na trilha);
            // com dono, a posse fica onde está — só a autoria da mensagem registra quem falou.
            if (conversa.OperadorResponsavelId is null)
                await AplicarClaimAsync(conversa, me, agora, ct);
            conversa.Assunto ??= request.Assunto;
            conversa.PacienteId ??= pacienteId;
            conversa.NomeContato ??= nome;
            conversa.AtualizadoEm = agora;
            conversa.AtualizadoPor = me;
        }

        await db.SaveChangesAsync(ct); // garante a conversa antes de enviar/vincular

        var nomeOperador = await ObterNomeAsync(me, ct);
        var envio = await whats.EnviarTemplateAsync(
            fone, request.Template, request.Idioma, request.Parametros, pacienteId: conversa.PacienteId, ct: ct);
        if (!envio.Ok)
            throw new ConflitoException("conversa.envio_falhou", $"Falha ao enviar o modelo: {envio.Erro}");

        var preview = $"[modelo: {request.Template}]";
        await VincularAsync(envio.WaMessageId, conversa.Id, me, nomeOperador, TipoMensagem.Template, ct);
        conversa.UltimaMensagemEm = agora;
        conversa.UltimaMensagemDirecao = DirecaoMensagem.Saida;
        conversa.UltimaMensagemPreview = preview;
        await db.SaveChangesAsync(ct);

        await notificador.ConversaAtualizadaAsync(ParaEvento(conversa, preview), ct);
        return conversa.Id;
    }

    public async Task EnviarTextoAsync(Guid conversaId, EnviarMensagemRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var conversa = await ObterNoEscopoAsync(conversaId, rastrear: true, ct);

        if (conversa.JanelaExpiraEm is null || conversa.JanelaExpiraEm <= DateTime.UtcNow)
            throw new ConflitoException("janela.expirada",
                "A janela de 24h expirou. Envie um modelo (template) para reabrir a conversa.");

        if (string.IsNullOrWhiteSpace(request.Texto))
            throw new ValidacaoException("texto", "A mensagem não pode ser vazia.");

        var nomeOperador = await ObterNomeAsync(me, ct);
        var corpo = MontarCorpo(nomeOperador, request.Texto);

        var envio = await whats.EnviarTextoAsync(conversa.TelefoneCanonical, corpo, pacienteId: conversa.PacienteId, ct: ct);
        if (!envio.Ok)
            throw new ConflitoException("conversa.envio_falhou", $"Falha ao enviar a mensagem: {envio.Erro}");

        await VincularAsync(envio.WaMessageId, conversa.Id, me, nomeOperador, TipoMensagem.Texto, ct);

        var agora = DateTime.UtcNow;
        // Responder conversa SEM dono é o claim implícito: ela sai da fila e vira minha. Com dono
        // (meu ou de colega), a posse NÃO muda — a autoria da mensagem já registra quem falou.
        var eraSemDono = conversa.OperadorResponsavelId is null;
        var unidadeAntes = conversa.UnidadeId;
        ConversaEvento? eventoClaim = null;
        if (eraSemDono) eventoClaim = await AplicarClaimAsync(conversa, me, agora, ct);

        conversa.NaoLidas = 0; // responder = ler
        conversa.UltimaMensagemEm = agora;
        conversa.UltimaMensagemDirecao = DirecaoMensagem.Saida;
        conversa.UltimaMensagemPreview = Truncar(request.Texto);
        conversa.AtualizadoEm = agora;
        conversa.AtualizadoPor = me;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A mensagem JÁ FOI ao cidadão — daqui em diante a corrida nunca vira erro para o
            // operador. Alguém mexeu na linha no meio (colega assumiu, inbound chegou): recarrega
            // o estado que venceu e reaplica só os efeitos da mensagem. O claim só permanece se a
            // conversa CONTINUAR sem dono; se um colega ganhou, a posse é dele.
            var entry = db.Entry(conversa);
            var banco = await entry.GetDatabaseValuesAsync(ct);
            if (banco is null) throw; // conversa sumiu no meio — deixa o middleware responder
            entry.OriginalValues.SetValues(banco); // token xmin novo para o próximo save
            entry.CurrentValues.SetValues(banco);  // parte do estado do vencedor (posse incluída)

            if (eventoClaim is not null && conversa.OperadorResponsavelId is not null)
            {
                // Perdeu a corrida do claim — o Assumida enfileirado não aconteceu.
                db.Entry(eventoClaim).State = EntityState.Detached;
                eventoClaim = null;
            }
            else if (eraSemDono && conversa.OperadorResponsavelId is null)
            {
                // O conflito veio de outra mudança (ex.: inbound) e ela segue sem dono — o claim vale.
                conversa.OperadorResponsavelId = me;
                conversa.UnidadeId ??= unidadeAntes ?? await vinculos.ObterPrincipalIdAsync(me, ct);
            }

            conversa.NaoLidas = 0;
            conversa.UltimaMensagemEm = agora;
            conversa.UltimaMensagemDirecao = DirecaoMensagem.Saida;
            conversa.UltimaMensagemPreview = Truncar(request.Texto);
            conversa.AtualizadoEm = agora;
            conversa.AtualizadoPor = me;
            await db.SaveChangesAsync(ct);
        }

        if (eventoClaim is not null)
            await notificador.ConversaMovidaAsync(
                ParaEvento(conversa, request.Texto), deOperadorId: null, deUnidadeId: unidadeAntes, ct);
        await notificador.MensagemEnviadaAsync(ParaEvento(conversa, request.Texto), ct);
    }

    public async Task<IReadOnlyList<ConversaListItemDto>> ListarAsync(AbaConversas aba, string? busca, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var minhasUnidades = await vinculos.ObterUnidadeIdsAsync(me, ct);
        var agora = DateTime.UtcNow;

        var query = db.Conversas.AsNoTracking().Where(c => c.ExcluidoEm == null);

        // Minhas e a fila são DISJUNTAS: conversa com dono aparece só na lista pessoal do dono;
        // a fila é o que ninguém puxou — das minhas unidades ou da triagem geral. NaoAtribuidas
        // sobrevive como alias da fila (compat com front antigo).
        // ADR-0048: a aba Todas foi DESTRAVADA — todo operador do módulo Conversas vê todas as
        // conversas (era exclusiva da supervisão). É só VISIBILIDADE de leitura; a posse continua
        // trava de AÇÃO (responder/assumir/devolver/encaminhar/transferir seguem gated).
        query = aba switch
        {
            AbaConversas.Minhas => FiltrarMinhas(query, me),
            AbaConversas.Todas => query,
            _ => FiltrarFila(query, minhasUnidades),
        };

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();

            // O nome cadastrado do paciente vive no schema fhir (resolvido pelo hub), não na
            // tabela conversa. Para achar "telma" pelo cadastro, resolvemos os PacienteIds cujo
            // nome/CPF/CNS casam com o termo (mesma busca do hub usada em BuscarContatosAsync) e
            // incluímos no filtro. Só a partir de 3 caracteres (a lista é refeita a cada tecla,
            // não vale bater no hub para termos muito curtos). Best-effort: qualquer falha do hub
            // degrada para a busca antiga (telefone + nome do WhatsApp), sem quebrar a listagem.
            var idsPacientes = new HashSet<Guid>();
            if (termo.Length >= 3)
            {
                try
                {
                    idsPacientes = (await pacientes.BuscarAsync(termo, ct)).Select(p => p.Id).ToHashSet();
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    // hub indisponível: segue só com telefone + nome do WhatsApp + conteúdo
                }
            }

            // Busca insensível a ACENTO e a maiúsculas: unaccent() (extensão) normaliza os dois
            // lados e o ILIKE cuida do case. Campos cobertos: telefone (substring; + variante só
            // de dígitos quando a pessoa digita com máscara/separadores), nome do contato (perfil
            // do WhatsApp), paciente vinculado (via hub) e — novidade do #45 — o CONTEÚDO das
            // mensagens da conversa, por EXISTS correlacionado em tfd_mensagem_whatsapp. Escala
            // atual (~10 mil mensagens) dispensa índice full-text; se crescer, indexar depois.
            var padrao = $"%{termo}%";
            var digitos = new string([.. termo.Where(char.IsDigit)]);
            var padraoDigitos = digitos.Length >= 3 ? $"%{digitos}%" : null;
            query = query.Where(c =>
                EF.Functions.ILike(c.TelefoneCanonical, padrao)
                || (padraoDigitos != null && EF.Functions.ILike(c.TelefoneCanonical, padraoDigitos))
                || (c.NomeContato != null && EF.Functions.ILike(EF.Functions.Unaccent(c.NomeContato), EF.Functions.Unaccent(padrao)))
                || (c.PacienteId != null && idsPacientes.Contains(c.PacienteId.Value))
                || db.MensagensWhatsApp.Any(m => m.ConversaId == c.Id
                    && m.Conteudo != null
                    && EF.Functions.ILike(EF.Functions.Unaccent(m.Conteudo), EF.Functions.Unaccent(padrao))));
        }

        var itens = await query
            .OrderByDescending(c => c.UltimaMensagemEm ?? c.CriadoEm)
            .Take(200)
            .Select(c => new ConversaListItemDto(
                c.Id, c.TelefoneCanonical, c.NomeContato, c.PacienteId, c.Assunto, c.Status,
                c.OperadorResponsavelId, c.OperadorResponsavel!.NomeCompleto,
                c.UnidadeId, c.Unidade!.Nome,
                c.UltimaMensagemEm, c.UltimaMensagemDirecao, c.UltimaMensagemPreview,
                c.NaoLidas, c.JanelaExpiraEm,
                c.JanelaExpiraEm != null && c.JanelaExpiraEm > agora))
            .ToListAsync(ct);

        // Nome do paciente resolvido do hub (best-effort; hub fora → segue sem nome).
        var nomes = await pacienteResolver.ResolverManyAsync(
            itens.Where(i => i.PacienteId.HasValue).Select(i => i.PacienteId!.Value), ct);
        return [.. itens.Select(i => i.PacienteId is { } pid && nomes.TryGetValue(pid, out var r)
            ? i with { PacienteNome = r.Nome }
            : i)];
    }

    public async Task<ConversaListItemDto> ObterAsync(Guid conversaId, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var dto = await db.Conversas.AsNoTracking()
            .Where(c => c.Id == conversaId && c.ExcluidoEm == null)
            .Select(c => new ConversaListItemDto(
                c.Id, c.TelefoneCanonical, c.NomeContato, c.PacienteId, c.Assunto, c.Status,
                c.OperadorResponsavelId, c.OperadorResponsavel!.NomeCompleto,
                c.UnidadeId, c.Unidade!.Nome,
                c.UltimaMensagemEm, c.UltimaMensagemDirecao, c.UltimaMensagemPreview,
                c.NaoLidas, c.JanelaExpiraEm,
                c.JanelaExpiraEm != null && c.JanelaExpiraEm > agora))
            .FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException("Conversa", conversaId);

        // ADR-0048: leitura destravada — qualquer operador do módulo abre qualquer conversa. A
        // trava de posse continua valendo para as AÇÕES (responder/assumir/devolver/…).

        // Nome COMPLETO do paciente (título da thread): pelo vínculo ou, sem vínculo, achando
        // pelo telefone no hub (só exibição — não grava). Best-effort: hub fora → sem nome.
        try
        {
            if (dto.PacienteId is { } pid)
            {
                var resumo = await pacienteResolver.ResolverAsync(pid, ct);
                if (resumo is not null) dto = dto with { PacienteNome = resumo.Nome };
            }
            else
            {
                var porFone = await pacientes.ObterPorTelefoneAsync(dto.TelefoneCanonical, ct);
                if (porFone is not null)
                    dto = dto with { PacienteId = porFone.Id, PacienteNome = porFone.NomeCompleto };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Resolver o nome é enfeite da thread — nunca derruba o chat.
        }
        return dto;
    }

    public async Task<IReadOnlyList<MensagemDto>> ObterMensagensAsync(Guid conversaId, CancellationToken ct = default)
    {
        // ADR-0048: leitura destravada (sem trava de escopo) + HISTÓRICO COMPLETO do interlocutor.
        var conversa = await CarregarVivaAsync(conversaId, rastrear: false, ct);
        var fone = conversa.TelefoneCanonical;
        var pacienteId = conversa.PacienteId;

        // Não só as mensagens desta thread: TODAS as do mesmo telefone (qualquer conversa, de
        // qualquer unidade/operador) + as AUTOMÁTICAS (confirmação de agendamento, laudo etc.,
        // que têm conversa_id nulo) + as do mesmo paciente em outros números. O telefone canônico
        // é a mesma chave em toda origem (Canonizar no inbound; NormalizarTelefone no outbound —
        // mesmo algoritmo). Teto nas 500 MAIS RECENTES, devolvidas em ordem cronológica.
        var recentes = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.Telefone == fone || (pacienteId != null && m.PacienteId == pacienteId))
            .OrderByDescending(m => m.OcorridoEm)
            .Take(500)
            .Select(m => new MensagemDto(
                m.Id, m.ConversaId, m.Direcao, m.TipoMensagem, m.Conteudo, m.Template,
                m.AutorUsuarioId, m.AutorNomeExibicao, m.Status, m.OcorridoEm))
            .ToListAsync(ct);

        recentes.Reverse(); // a UI rola para o fim: ordem ascendente por OcorridoEm
        return recentes;
    }

    public async Task MarcarLidaAsync(Guid conversaId, CancellationToken ct = default)
    {
        // NÃO muda a posse: o front antigo chama isto ao abrir a thread — claim aqui roubaria a
        // conversa em silêncio. O claim explícito é AssumirAsync.
        var conversa = await ObterNoEscopoAsync(conversaId, rastrear: true, ct);

        if (conversa.NaoLidas == 0) return;

        conversa.NaoLidas = 0;
        conversa.AtualizadoEm = DateTime.UtcNow;
        conversa.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);

        await notificador.ConversaAtualizadaAsync(ParaEvento(conversa, conversa.UltimaMensagemPreview), ct);
    }

    // --- posse (assumir / devolver / encaminhar / transferir) ----------------------------------

    public async Task AssumirAsync(Guid conversaId, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var conversa = await ObterNoEscopoAsync(conversaId, rastrear: true, ct);

        if (conversa.OperadorResponsavelId == me)
        {
            // Já é minha — só garante o contador zerado (idempotente).
            if (conversa.NaoLidas == 0) return;
            conversa.NaoLidas = 0;
            conversa.AtualizadoEm = DateTime.UtcNow;
            conversa.AtualizadoPor = me;
            await db.SaveChangesAsync(ct);
            await notificador.ConversaAtualizadaAsync(ParaEvento(conversa, conversa.UltimaMensagemPreview), ct);
            return;
        }

        if (conversa.OperadorResponsavelId is { } dono)
            throw await ConflitoJaAssumidaAsync(dono, ct);

        var agora = DateTime.UtcNow;
        var unidadeAntes = conversa.UnidadeId;
        await AplicarClaimAsync(conversa, me, agora, ct);
        await SalvarComTraducaoDeCorridaAsync(conversa, ct);
        await notificador.ConversaMovidaAsync(
            ParaEvento(conversa, conversa.UltimaMensagemPreview), deOperadorId: null, deUnidadeId: unidadeAntes, ct);
    }

    public async Task DevolverAsync(Guid conversaId, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var conversa = await ObterNoEscopoAsync(conversaId, rastrear: true, ct);
        await ExigirPosseOuSupervisaoAsync(conversa, me, ct);

        if (conversa.OperadorResponsavelId is not { } deOperador) return; // já está na fila

        var agora = DateTime.UtcNow;
        conversa.OperadorResponsavelId = null; // a unidade fica — a conversa volta à fila de onde estava
        conversa.AtualizadoEm = agora;
        conversa.AtualizadoPor = me;

        var evento = NovoEvento(conversa.Id, TipoEventoConversa.Devolvida, me, agora);
        evento.DeUsuarioId = deOperador;
        db.ConversaEventos.Add(evento);

        await SalvarComTraducaoDeCorridaAsync(conversa, ct);
        await notificador.ConversaMovidaAsync(
            ParaEvento(conversa, conversa.UltimaMensagemPreview), deOperador, conversa.UnidadeId, ct);
    }

    public async Task EncaminharAsync(
        Guid conversaId, EncaminharConversaRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var conversa = await ObterNoEscopoAsync(conversaId, rastrear: true, ct);
        await ExigirPosseOuSupervisaoAsync(conversa, me, ct);

        var alvo = request.ParaUsuarioId;
        if (conversa.OperadorResponsavelId == alvo) return; // já é dele

        var alvoInfo = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == alvo && u.ExcluidoEm == null)
            .Select(u => new { u.Ativo })
            .FirstOrDefaultAsync(ct);
        if (alvoInfo is null || !alvoInfo.Ativo)
            throw new ValidacaoException("paraUsuarioId", "O atendente de destino não existe ou está inativo.");
        if (!await TemModuloAsync(alvo, ModuloPermissao.Conversas, ct))
            throw new ValidacaoException("paraUsuarioId",
                "O atendente de destino não tem acesso à Central de Atendimento.");

        if (conversa.UnidadeId is { } unidadeConversa
            && !await db.UsuarioUnidades.AsNoTracking()
                .AnyAsync(x => x.UsuarioId == alvo && x.UnidadeId == unidadeConversa, ct))
            throw new ValidacaoException("paraUsuarioId",
                "O atendente de destino não é vinculado à unidade desta conversa.");

        var agora = DateTime.UtcNow;
        var deOperador = conversa.OperadorResponsavelId;
        var deUnidade = conversa.UnidadeId;
        conversa.OperadorResponsavelId = alvo;
        // Conversa da triagem geral entra na unidade do atendente que a recebeu.
        conversa.UnidadeId ??= await vinculos.ObterPrincipalIdAsync(alvo, ct);
        // NaoLidas fica como está: a pendência de leitura agora é do novo responsável.
        conversa.AtualizadoEm = agora;
        conversa.AtualizadoPor = me;

        // Na trilha, "Transferida" é o encaminhamento a outro operador (nomenclatura do enum).
        var evento = NovoEvento(conversa.Id, TipoEventoConversa.Transferida, me, agora);
        evento.DeUsuarioId = deOperador;
        evento.ParaUsuarioId = alvo;
        evento.Observacao = LimparObservacao(request.Observacao);
        db.ConversaEventos.Add(evento);

        await SalvarComTraducaoDeCorridaAsync(conversa, ct);
        await notificador.ConversaMovidaAsync(
            ParaEvento(conversa, conversa.UltimaMensagemPreview), deOperador, deUnidade, ct);
    }

    public async Task TransferirUnidadeAsync(
        Guid conversaId, TransferirConversaRequest request, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var conversa = await ObterNoEscopoAsync(conversaId, rastrear: true, ct);
        await ExigirPosseOuSupervisaoAsync(conversa, me, ct);

        if (conversa.UnidadeId == request.ParaUnidadeId)
            throw new ValidacaoException("paraUnidadeId", "A conversa já está nessa unidade.");

        var destino = await db.Unidades.AsNoTracking()
            .Where(u => u.Id == request.ParaUnidadeId)
            .Select(u => new { u.Ativo, u.Externa })
            .FirstOrDefaultAsync(ct);
        if (destino is null || !destino.Ativo || destino.Externa)
            throw new ValidacaoException("paraUnidadeId",
                "A unidade de destino não existe, está inativa ou é externa à rede.");

        var agora = DateTime.UtcNow;
        var deOperador = conversa.OperadorResponsavelId;
        var deUnidade = conversa.UnidadeId;
        conversa.OperadorResponsavelId = null; // entra na fila de lá, sem responsável
        conversa.UnidadeId = request.ParaUnidadeId;
        conversa.AtualizadoEm = agora;
        conversa.AtualizadoPor = me;

        var evento = NovoEvento(conversa.Id, TipoEventoConversa.EncaminhadaUnidade, me, agora);
        evento.DeUsuarioId = deOperador;
        evento.DeUnidadeId = deUnidade;
        evento.ParaUnidadeId = request.ParaUnidadeId;
        evento.Observacao = LimparObservacao(request.Observacao);
        db.ConversaEventos.Add(evento);

        await SalvarComTraducaoDeCorridaAsync(conversa, ct);
        await notificador.ConversaMovidaAsync(
            ParaEvento(conversa, conversa.UltimaMensagemPreview), deOperador, deUnidade, ct);
    }

    public async Task<IReadOnlyList<AtendenteElegivelDto>> ListarAtendentesElegiveisAsync(
        Guid conversaId, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var conversa = await ObterNoEscopoAsync(conversaId, rastrear: false, ct);

        // Conversa com unidade: os colegas de lá. Da triagem geral: o MEU time (minhas
        // unidades) — quem puxa do balde geral distribui dentro do próprio time.
        IReadOnlyList<Guid> unidadesAlvo = conversa.UnidadeId is { } unidade
            ? [unidade]
            : await vinculos.ObterUnidadeIdsAsync(me, ct);
        if (unidadesAlvo.Count == 0) return [];

        var candidatos = await db.UsuarioUnidades.AsNoTracking()
            .Where(x => unidadesAlvo.Contains(x.UnidadeId)
                && x.Usuario!.Ativo && x.Usuario.ExcluidoEm == null)
            .Select(x => new { x.UsuarioId, x.Usuario!.NomeCompleto })
            .Distinct()
            .OrderBy(x => x.NomeCompleto)
            .ToListAsync(ct);

        var elegiveis = new List<AtendenteElegivelDto>(candidatos.Count);
        foreach (var candidato in candidatos)
        {
            // Sem o módulo Conversas o encaminhamento seria recusado — nem oferecer.
            if (!await TemModuloAsync(candidato.UsuarioId, ModuloPermissao.Conversas, ct)) continue;
            elegiveis.Add(new AtendenteElegivelDto(
                candidato.UsuarioId, candidato.NomeCompleto,
                candidato.UsuarioId == conversa.OperadorResponsavelId));
        }
        return elegiveis;
    }

    public async Task<IReadOnlyList<UnidadeDestinoDto>> ListarUnidadesDestinoAsync(CancellationToken ct = default) =>
        await db.Unidades.AsNoTracking()
            .Where(u => u.Ativo && !u.Externa)
            .OrderBy(u => u.Nome)
            .Select(u => new UnidadeDestinoDto(u.Id, u.Nome))
            .ToListAsync(ct);

    public async Task<ResumoConversasDto> ObterResumoAsync(CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var minhasUnidades = await vinculos.ObterUnidadeIdsAsync(me, ct);

        var vivas = db.Conversas.AsNoTracking().Where(c => c.ExcluidoEm == null);
        var minhas = await FiltrarMinhas(vivas, me).SumAsync(c => c.NaoLidas, ct);
        var fila = await FiltrarFila(vivas, minhasUnidades).SumAsync(c => c.NaoLidas, ct);
        return new ResumoConversasDto(minhas, fila);
    }

    // --- helpers -------------------------------------------------------------------------------

    /// <summary>Predicado da lista pessoal — compartilhado entre a aba Minhas e o resumo do sino.</summary>
    private static IQueryable<Conversa> FiltrarMinhas(IQueryable<Conversa> query, Guid me) =>
        query.Where(c => c.OperadorResponsavelId == me);

    /// <summary>
    /// Predicado da fila (sem responsável, nas minhas unidades ou na triagem geral) —
    /// compartilhado entre a listagem e o resumo. Se divergirem, o sino conta o que a lista
    /// não mostra.
    /// </summary>
    private static IQueryable<Conversa> FiltrarFila(IQueryable<Conversa> query, IReadOnlyList<Guid> minhasUnidades) =>
        query.Where(c => c.OperadorResponsavelId == null
            && (c.UnidadeId == null || minhasUnidades.Contains(c.UnidadeId.Value)));

    /// <summary>Carrega a conversa viva (não excluída) sem aplicar escopo — só existência → 404.
    /// Usado nos caminhos de LEITURA, destravados pelo ADR-0048.</summary>
    private async Task<Conversa> CarregarVivaAsync(Guid conversaId, bool rastrear, CancellationToken ct)
    {
        IQueryable<Conversa> query = rastrear ? db.Conversas : db.Conversas.AsNoTracking();
        return await query.FirstOrDefaultAsync(c => c.Id == conversaId && c.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Conversa", conversaId);
    }

    /// <summary>
    /// Carrega a conversa aplicando o escopo de acesso por id — fora do escopo → 404 (não vaza a
    /// existência). Trava das AÇÕES (responder/assumir/devolver/encaminhar/transferir); a LEITURA
    /// foi destravada no ADR-0048. Token de serviço (X-API-Key, sem operador) vê tudo.
    /// </summary>
    private async Task<Conversa> ObterNoEscopoAsync(Guid conversaId, bool rastrear, CancellationToken ct)
    {
        var conversa = await CarregarVivaAsync(conversaId, rastrear, ct);

        if (usuarioAtual.UsuarioId is { } me
            && !await NoEscopoAsync(conversa.OperadorResponsavelId, conversa.UnidadeId, me, ct))
            throw new NaoEncontradoException("Conversa", conversaId);

        return conversa;
    }

    /// <summary>
    /// A posse como trava de acesso, não só filtro de listagem: supervisão vê tudo; o dono vê a
    /// sua; conversa sem dono é visível na triagem geral (sem unidade) ou aos vinculados à
    /// unidade dela; conversa COM dono continua acessível aos colegas da mesma unidade (responder
    /// sem roubar a posse é permitido). Dono sem unidade: só ele e a supervisão.
    /// </summary>
    private async Task<bool> NoEscopoAsync(
        Guid? operadorResponsavelId, Guid? unidadeId, Guid me, CancellationToken ct)
    {
        if (operadorResponsavelId == me) return true;
        if (operadorResponsavelId is null && unidadeId is null) return true;
        if (unidadeId is { } unidade && (await vinculos.ObterUnidadeIdsAsync(me, ct)).Contains(unidade))
            return true;
        return await EhSupervisorAsync(me, ct);
    }

    /// <summary>Agir sobre conversa de TERCEIRO (devolver/encaminhar/transferir) exige supervisão.</summary>
    private async Task ExigirPosseOuSupervisaoAsync(Conversa conversa, Guid me, CancellationToken ct)
    {
        if (conversa.OperadorResponsavelId is { } dono && dono != me && !await EhSupervisorAsync(me, ct))
            throw new ConflitoException("conversa.sem_posse",
                "Só o responsável pela conversa (ou a supervisão) pode fazer isso.");
    }

    /// <summary>
    /// Aplica o claim: o operador vira o responsável, a conversa herda a unidade principal dele
    /// quando não tinha nenhuma, o contador zera e a trilha ganha o Assumida. NÃO salva — o
    /// SaveChanges é do chamador. Devolve o evento para o chamador poder descartá-lo se perder a
    /// corrida (EnviarTextoAsync).
    /// </summary>
    private async Task<ConversaEvento> AplicarClaimAsync(
        Conversa conversa, Guid me, DateTime agora, CancellationToken ct)
    {
        conversa.OperadorResponsavelId = me;
        conversa.UnidadeId ??= await vinculos.ObterPrincipalIdAsync(me, ct);
        conversa.NaoLidas = 0;
        conversa.AtualizadoEm = agora;
        conversa.AtualizadoPor = me;

        var evento = NovoEvento(conversa.Id, TipoEventoConversa.Assumida, me, agora);
        evento.ParaUsuarioId = me;
        db.ConversaEventos.Add(evento);
        return evento;
    }

    /// <summary>
    /// SaveChanges traduzindo a corrida de posse (token xmin) num 409 legível: relê quem ficou
    /// com a conversa e devolve <c>conversa.ja_assumida</c> com o nome do vencedor.
    /// </summary>
    private async Task SalvarComTraducaoDeCorridaAsync(Conversa conversa, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var donoAtual = await db.Conversas.AsNoTracking()
                .Where(c => c.Id == conversa.Id)
                .Select(c => c.OperadorResponsavelId)
                .FirstOrDefaultAsync(ct);
            if (donoAtual is { } dono && dono != usuarioAtual.UsuarioId)
                throw await ConflitoJaAssumidaAsync(dono, ct);
            throw new ConflitoException("conversa.alterada",
                "A conversa mudou enquanto você agia — atualize a lista e tente de novo.");
        }
    }

    private async Task<ConflitoException> ConflitoJaAssumidaAsync(Guid donoId, CancellationToken ct) =>
        new("conversa.ja_assumida", $"Conversa já assumida por {await ObterNomeAsync(donoId, ct)}.");

    private static string? LimparObservacao(string? observacao)
    {
        observacao = observacao?.Trim();
        if (string.IsNullOrEmpty(observacao)) return null;
        return observacao.Length <= 1000 ? observacao : observacao[..1000];
    }

    private Guid ExigirUsuario() =>
        usuarioAtual.UsuarioId ?? throw new ValidacaoException("operador", "Operador não identificado na requisição.");

    private async Task<string> ObterNomeAsync(Guid usuarioId, CancellationToken ct) =>
        await db.Usuarios.Where(u => u.Id == usuarioId).Select(u => u.NomeCompleto).FirstOrDefaultAsync(ct) ?? "Atendente";

    private Task<bool> EhSupervisorAsync(Guid usuarioId, CancellationToken ct) =>
        TemModuloAsync(usuarioId, ModuloPermissao.ConversasSupervisao, ct);

    private async Task<bool> TemModuloAsync(Guid usuarioId, ModuloPermissao modulo, CancellationToken ct)
    {
        try
        {
            var perms = await identidade.ObterPermissoesResolvidasAsync(usuarioId, ct);
            return perms.Resolvidas.Any(p => p.Modulo == modulo && p.Acoes != AcoesPermissao.Nenhuma);
        }
        catch (NaoEncontradoException) { return false; }
    }

    /// <summary>Prefixa o texto com o nome (curto) do operador — o cidadão vê quem está falando.</summary>
    private string MontarCorpo(string nomeOperador, string texto)
    {
        var primeiro = PrimeiroNome(nomeOperador);
        var formato = configuration["Conversas:FormatoPrefixo"];
        if (!string.IsNullOrWhiteSpace(formato))
            return string.Format(formato, primeiro, texto);
        return string.IsNullOrWhiteSpace(primeiro) ? texto : $"*{primeiro}:*\n{texto}";
    }

    private async Task VincularAsync(string? waMessageId, Guid conversaId, Guid autorId, string autorNome, TipoMensagem tipo, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(waMessageId)) return;
        // A mensagem já foi persistida pelo WhatsAppCliente no MESMO contexto (scoped) — está
        // rastreada (Local); localizamos e vinculamos. A gravação acontece no SaveChanges do chamador.
        var msg = db.MensagensWhatsApp.Local.FirstOrDefault(m => m.WaMessageId == waMessageId)
            ?? await db.MensagensWhatsApp.FirstOrDefaultAsync(m => m.WaMessageId == waMessageId, ct);
        if (msg is null) return;
        msg.ConversaId = conversaId;
        msg.AutorUsuarioId = autorId;
        msg.AutorNomeExibicao = autorNome;
        msg.TipoMensagem = tipo;
    }

    private static ConversaEvento NovoEvento(Guid conversaId, TipoEventoConversa tipo, Guid? ator, DateTime agora) => new()
    {
        Id = Guid.CreateVersion7(),
        ConversaId = conversaId,
        Tipo = tipo,
        AtorUsuarioId = ator,
        OcorridoEm = agora,
        CriadoEm = agora,
    };

    private static ConversaEventoRealtime ParaEvento(Conversa c, string? preview) => new(
        c.Id, c.OperadorResponsavelId, c.UnidadeId, c.TelefoneCanonical, c.NomeContato,
        Truncar(preview), c.NaoLidas, c.UltimaMensagemEm);

    // O nome de exibição do operador costuma vir em caixa alta do cadastro — no WhatsApp
    // isso lê como grito.
    private static string PrimeiroNome(string nome) => NomePessoa.PrimeiroNome(nome);

    private static string? Truncar(string? s) => s is null ? null : s.Length <= 200 ? s : s[..200];
}
