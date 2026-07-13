using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Common.Texto;
using SMSMarica.Core.Conversas.Dtos;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Pacientes;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Conversas;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Conversas;

public sealed class ConversaService(
    SmsMaricaDbContext db,
    IWhatsAppCliente whats,
    IPacientesService pacientes,
    IUsuarioAtualAccessor usuarioAtual,
    IIdentidadeService identidade,
    IUsuarioUnidadeService vinculos,
    IConversaNotificador notificador,
    Pacientes.Fhir.IPacienteResolver pacienteResolver,
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
            var solicitacoes = await db.SolicitacoesExame
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
        var conversa = await db.Conversas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversaId && c.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Conversa", conversaId);

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
        var interpretado = TelefoneWhatsApp.Interpretar(request.Telefone);
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
            conversa.OperadorResponsavelId ??= me;
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
        var conversa = await db.Conversas.FirstOrDefaultAsync(c => c.Id == conversaId && c.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Conversa", conversaId);

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
        conversa.UltimaMensagemEm = agora;
        conversa.UltimaMensagemDirecao = DirecaoMensagem.Saida;
        conversa.UltimaMensagemPreview = Truncar(request.Texto);
        conversa.AtualizadoEm = agora;
        conversa.AtualizadoPor = me;
        await db.SaveChangesAsync(ct);

        await notificador.MensagemEnviadaAsync(ParaEvento(conversa, request.Texto), ct);
    }

    public async Task<IReadOnlyList<ConversaListItemDto>> ListarAsync(AbaConversas aba, string? busca, CancellationToken ct = default)
    {
        var me = ExigirUsuario();
        var minhasUnidades = await vinculos.ObterUnidadeIdsAsync(me, ct);
        var supervisor = await EhSupervisorAsync(me, ct);
        var agora = DateTime.UtcNow;

        var query = db.Conversas.AsNoTracking().Where(c => c.ExcluidoEm == null);

        query = aba switch
        {
            AbaConversas.Minhas => query.Where(c => c.OperadorResponsavelId == me),
            AbaConversas.NaoAtribuidas => query.Where(c => c.OperadorResponsavelId == null
                && (c.UnidadeId == null || minhasUnidades.Contains(c.UnidadeId.Value))),
            AbaConversas.Todas when supervisor => query,
            _ => query.Where(c => c.OperadorResponsavelId == me
                || c.UnidadeId == null
                || (c.UnidadeId != null && minhasUnidades.Contains(c.UnidadeId.Value))),
        };

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            query = query.Where(c => c.TelefoneCanonical.Contains(termo)
                || (c.NomeContato != null && c.NomeContato.Contains(termo)));
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

    public async Task<IReadOnlyList<MensagemDto>> ObterMensagensAsync(Guid conversaId, CancellationToken ct = default) =>
        await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.ConversaId == conversaId)
            .OrderBy(m => m.OcorridoEm)
            .Take(500)
            .Select(m => new MensagemDto(
                m.Id, m.ConversaId, m.Direcao, m.TipoMensagem, m.Conteudo, m.Template,
                m.AutorUsuarioId, m.AutorNomeExibicao, m.Status, m.OcorridoEm))
            .ToListAsync(ct);

    public async Task MarcarLidaAsync(Guid conversaId, CancellationToken ct = default)
    {
        var conversa = await db.Conversas.FirstOrDefaultAsync(c => c.Id == conversaId && c.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Conversa", conversaId);

        if (conversa.NaoLidas == 0) return;

        conversa.NaoLidas = 0;
        conversa.AtualizadoEm = DateTime.UtcNow;
        conversa.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);

        await notificador.ConversaAtualizadaAsync(ParaEvento(conversa, conversa.UltimaMensagemPreview), ct);
    }

    // --- helpers -------------------------------------------------------------------------------

    private Guid ExigirUsuario() =>
        usuarioAtual.UsuarioId ?? throw new ValidacaoException("operador", "Operador não identificado na requisição.");

    private async Task<string> ObterNomeAsync(Guid usuarioId, CancellationToken ct) =>
        await db.Usuarios.Where(u => u.Id == usuarioId).Select(u => u.NomeCompleto).FirstOrDefaultAsync(ct) ?? "Atendente";

    private async Task<bool> EhSupervisorAsync(Guid usuarioId, CancellationToken ct)
    {
        try
        {
            var perms = await identidade.ObterPermissoesResolvidasAsync(usuarioId, ct);
            return perms.Resolvidas.Any(p => p.Modulo == ModuloPermissao.ConversasSupervisao && p.Acoes != AcoesPermissao.Nenhuma);
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
