using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Armazenamento;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.DocumentosPaciente;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.Conversas.Midias;

/// <summary>Conteúdo de uma mídia da conversa, para o visualizador.</summary>
public sealed record ConteudoMidiaConversa(byte[] Conteudo, string MimeType, string NomeArquivo);

/// <summary>Aceitar a mídia no cadastro: de qual paciente, com que nome.</summary>
public sealed record AceitarMidiaRequest(Guid PacienteId, string Titulo, string? Descricao);

/// <summary>
/// Mídias (foto, PDF) que o paciente manda pelo WhatsApp.
///
/// <para><b>O ciclo:</b> o webhook só anota o id da mídia (<see cref="SituacaoMidiaWhatsApp.Recebendo"/>);
/// o worker baixa pelo Zap e guarda (<see cref="SituacaoMidiaWhatsApp.Pendente"/>); alguém da equipe
/// abre no visualizador e <b>aceita no cadastro</b> (vira documento do acervo) ou <b>descarta</b>
/// (o arquivo sai do armazenamento).</para>
///
/// <para><b>A trava:</b> com <see cref="LimitePendentes"/> mídias pendentes no mesmo número sem
/// ninguém decidir, as próximas não são guardadas (<see cref="SituacaoMidiaWhatsApp.Bloqueada"/>) —
/// quem manda arquivo em massa não enche o armazenamento. Decidir as pendentes libera de novo.</para>
/// </summary>
public interface IMidiasConversaService
{
    const int LimitePendentes = 10;

    /// <summary>Baixa as mídias que chegaram e ainda não foram guardadas. Devolve quantas processou.</summary>
    Task<int> ProcessarRecebidasAsync(int maximo, CancellationToken ct = default);

    Task<ConteudoMidiaConversa> ObterConteudoAsync(Guid mensagemId, CancellationToken ct = default);

    Task AceitarAsync(Guid mensagemId, AceitarMidiaRequest req, CancellationToken ct = default);

    Task DescartarAsync(Guid mensagemId, CancellationToken ct = default);

    /// <summary>Tenta baixar de novo uma mídia que falhou ou foi travada (a Meta guarda ~30 dias).</summary>
    Task TentarDeNovoAsync(Guid mensagemId, CancellationToken ct = default);
}

public sealed class MidiasConversaService(
    SmsMaisDbContext db,
    IZapMidiaCliente zap,
    IArmazenamentoArquivos armazenamento,
    IDocumentosPacienteService acervo,
    IPacientesService pacientes,
    IUsuarioAtualAccessor usuarioAtual,
    IConversaNotificador notificador,
    ILogger<MidiasConversaService> logger) : IMidiasConversaService
{
    /// <summary>A Meta guarda a mídia por ~30 dias; depois disso não adianta tentar.</summary>
    private static readonly TimeSpan ValidadeNaMeta = TimeSpan.FromDays(29);

    public async Task<int> ProcessarRecebidasAsync(int maximo, CancellationToken ct = default)
    {
        var limite = DateTime.UtcNow - ValidadeNaMeta;
        var fila = await db.MensagensWhatsApp
            .Where(m => m.MidiaSituacao == SituacaoMidiaWhatsApp.Recebendo && m.MidiaWaId != null)
            .OrderBy(m => m.CriadoEm)
            .Take(maximo)
            .ToListAsync(ct);

        foreach (var msg in fila)
        {
            if (msg.CriadoEm < limite)
            {
                msg.MidiaSituacao = SituacaoMidiaWhatsApp.Falhou;
                await db.SaveChangesAsync(ct);
                continue;
            }
            await BaixarAsync(msg, ct);
        }
        return fila.Count;
    }

    private async Task BaixarAsync(MensagemWhatsApp msg, CancellationToken ct)
    {
        var pendentes = await db.MensagensWhatsApp.CountAsync(
            m => m.Telefone == msg.Telefone && m.MidiaSituacao == SituacaoMidiaWhatsApp.Pendente, ct);
        if (pendentes >= IMidiasConversaService.LimitePendentes)
        {
            msg.MidiaSituacao = SituacaoMidiaWhatsApp.Bloqueada;
            await db.SaveChangesAsync(ct);
            logger.LogWarning(
                "WhatsApp: mídia de …{Final} não guardada — {Pendentes} pendentes sem decisão.",
                Final(msg.Telefone), pendentes);
            await AvisarAsync(msg, ct);
            return;
        }

        var r = await zap.BaixarAsync(msg.MidiaWaId!, DocumentosPacienteService.TamanhoMaximoBytes, ct);
        if (r.Conteudo is null)
        {
            logger.LogWarning("WhatsApp: download da mídia {Mensagem} falhou: {Erro}", msg.Id, r.Erro);
            // Falha passageira (Zap reiniciando) fica para a próxima passagem; a definitiva para.
            if (!r.Definitivo && msg.CriadoEm > DateTime.UtcNow.AddHours(-6)) return;
            msg.MidiaSituacao = SituacaoMidiaWhatsApp.Falhou;
            await db.SaveChangesAsync(ct);
            await AvisarAsync(msg, ct);
            return;
        }

        var mime = (r.MimeType ?? msg.MidiaMimeType ?? "application/octet-stream").Split(';')[0].Trim().ToLowerInvariant();
        if (!DocumentosPacienteService.TiposAceitos.Contains(mime))
        {
            // Áudio, vídeo, planilha… não vão para o cadastro; não ocupam armazenamento.
            msg.MidiaSituacao = SituacaoMidiaWhatsApp.Falhou;
            msg.MidiaMimeType = mime;
            await db.SaveChangesAsync(ct);
            await AvisarAsync(msg, ct);
            return;
        }

        var chave = armazenamento.MontarChaveDocumento(
            msg.PacienteId ?? Guid.Empty, msg.Id, DocumentosPacienteService.Extensao(mime));
        await armazenamento.SalvarAsync(chave, r.Conteudo, ct);

        msg.MidiaChave = chave;
        msg.MidiaMimeType = mime;
        msg.MidiaTamanho = r.Conteudo.LongLength;
        msg.MidiaSha256 = Convert.ToHexStringLower(SHA256.HashData(r.Conteudo));
        msg.MidiaSituacao = SituacaoMidiaWhatsApp.Pendente;
        await db.SaveChangesAsync(ct);
        await AvisarAsync(msg, ct);
    }

    public async Task<ConteudoMidiaConversa> ObterConteudoAsync(Guid mensagemId, CancellationToken ct = default)
    {
        var msg = await db.MensagensWhatsApp.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mensagemId, ct)
            ?? throw new NaoEncontradoException("Mensagem", mensagemId);

        // Aceita: o arquivo mora no cadastro do paciente (a cópia da conversa saiu ao aceitar).
        var chave = msg.MidiaSituacao switch
        {
            SituacaoMidiaWhatsApp.Pendente => msg.MidiaChave,
            SituacaoMidiaWhatsApp.Aceita when msg.DocumentoPacienteId is { } docId =>
                await db.DocumentosPaciente.AsNoTracking()
                    .Where(d => d.Id == docId && d.ExcluidoEm == null)
                    .Select(d => d.ChaveArmazenamento)
                    .FirstOrDefaultAsync(ct),
            _ => null,
        };
        if (chave is null) throw new NaoEncontradoException("Arquivo da mensagem", mensagemId);

        var bytes = await armazenamento.LerAsync(chave, ct)
            ?? throw new NaoEncontradoException("Arquivo da mensagem", mensagemId);
        var mime = msg.MidiaMimeType ?? "application/octet-stream";
        var nome = msg.MidiaNomeArquivo
            ?? DocumentosPacienteService.NomeArquivo($"whatsapp-{msg.OcorridoEm:yyyyMMdd-HHmmss}", mime);
        return new ConteudoMidiaConversa(bytes, mime, nome);
    }

    public async Task AceitarAsync(Guid mensagemId, AceitarMidiaRequest req, CancellationToken ct = default)
    {
        var msg = await ExigirAsync(mensagemId, ct);
        if (msg.MidiaSituacao == SituacaoMidiaWhatsApp.Aceita)
        {
            throw new ConflitoException("midia.ja_aceita", "Este arquivo já está no cadastro do paciente.");
        }
        if (msg.MidiaSituacao != SituacaoMidiaWhatsApp.Pendente || msg.MidiaChave is null)
        {
            throw new ConflitoException("midia.indisponivel", "Este arquivo não está guardado.");
        }

        await ExigirPacienteDaConversaAsync(msg, req.PacienteId, ct);

        var conteudo = await armazenamento.LerAsync(msg.MidiaChave, ct)
            ?? throw new NaoEncontradoException("Arquivo da mensagem", mensagemId);

        // O acervo guarda a própria cópia (a pasta é a do paciente escolhido); a da conversa sai
        // logo depois — a mesma foto não precisa morar em dois lugares.
        var doc = await acervo.AdicionarAsync(new NovoDocumentoPaciente(
            req.PacienteId, req.Titulo, req.Descricao,
            msg.MidiaNomeArquivo ?? DocumentosPacienteService.NomeArquivo(req.Titulo, msg.MidiaMimeType ?? string.Empty),
            msg.MidiaMimeType ?? "application/octet-stream", conteudo,
            OrigemDocumentoPaciente.WhatsApp, $"whatsapp:{msg.Id:D}", SituacaoDocumentoPaciente.Aceito), ct);

        await armazenamento.ExcluirAsync(msg.MidiaChave, ct);
        msg.MidiaChave = null;
        msg.MidiaSituacao = SituacaoMidiaWhatsApp.Aceita;
        msg.DocumentoPacienteId = doc.Id;
        msg.MidiaDecididaEm = DateTime.UtcNow;
        msg.MidiaDecididaPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
        await AvisarAsync(msg, ct);
    }

    public async Task DescartarAsync(Guid mensagemId, CancellationToken ct = default)
    {
        var msg = await ExigirAsync(mensagemId, ct);
        if (msg.MidiaSituacao == SituacaoMidiaWhatsApp.Aceita)
        {
            throw new ConflitoException(
                "midia.ja_aceita", "Este arquivo já está no cadastro do paciente — exclua por lá, se for o caso.");
        }
        if (msg.MidiaChave is not null)
        {
            await armazenamento.ExcluirAsync(msg.MidiaChave, ct);
        }
        msg.MidiaChave = null;
        msg.MidiaSituacao = SituacaoMidiaWhatsApp.Descartada;
        msg.MidiaDecididaEm = DateTime.UtcNow;
        msg.MidiaDecididaPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
        await AvisarAsync(msg, ct);
    }

    public async Task TentarDeNovoAsync(Guid mensagemId, CancellationToken ct = default)
    {
        var msg = await ExigirAsync(mensagemId, ct);
        if (msg.MidiaSituacao is not (SituacaoMidiaWhatsApp.Falhou or SituacaoMidiaWhatsApp.Bloqueada)
            || msg.MidiaWaId is null)
        {
            throw new ConflitoException("midia.nada_a_baixar", "Não há o que baixar de novo nesta mensagem.");
        }
        if (msg.CriadoEm < DateTime.UtcNow - ValidadeNaMeta)
        {
            throw new ConflitoException("midia.expirada", "O WhatsApp só guarda o arquivo por 30 dias — peça ao paciente para mandar de novo.");
        }

        msg.MidiaSituacao = SituacaoMidiaWhatsApp.Recebendo;
        await db.SaveChangesAsync(ct);
        await BaixarAsync(msg, ct);
        if (msg.MidiaSituacao == SituacaoMidiaWhatsApp.Bloqueada)
        {
            throw new ConflitoException(
                "midia.limite_pendentes",
                $"Este número já tem {IMidiasConversaService.LimitePendentes} arquivos esperando decisão. "
                + "Aceite ou descarte os pendentes antes.");
        }
    }

    // ---------------------------------------------------------------- apoio

    private async Task<MensagemWhatsApp> ExigirAsync(Guid mensagemId, CancellationToken ct) =>
        await db.MensagensWhatsApp.FirstOrDefaultAsync(
            m => m.Id == mensagemId && m.Direcao == DirecaoMensagem.Entrada && m.MidiaSituacao != null, ct)
        ?? throw new NaoEncontradoException("Arquivo da mensagem", mensagemId);

    /// <summary>
    /// O arquivo só vai para o cadastro de quem tem a ver com a conversa: o paciente da mensagem,
    /// o da conversa ou alguém cadastrado com aquele telefone. Mandar a foto de um para o
    /// cadastro de outro é o erro que isto impede.
    /// </summary>
    private async Task ExigirPacienteDaConversaAsync(MensagemWhatsApp msg, Guid pacienteId, CancellationToken ct)
    {
        if (msg.PacienteId == pacienteId) return;
        var daConversa = msg.ConversaId is { } cid
            && await db.Conversas.AsNoTracking().AnyAsync(c => c.Id == cid && c.PacienteId == pacienteId, ct);
        if (daConversa) return;
        var doTelefone = await pacientes.ListarPorTelefoneAsync(msg.Telefone, ct);
        if (doTelefone.Any(p => p.Id == pacienteId)) return;

        throw new ValidacaoException(
            "midia.paciente", "Esse paciente não tem ligação com esta conversa (nem pelo telefone).");
    }

    private async Task AvisarAsync(MensagemWhatsApp msg, CancellationToken ct)
    {
        if (msg.ConversaId is not { } conversaId) return;
        try
        {
            var c = await db.Conversas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == conversaId, ct);
            if (c is null) return;
            await notificador.ConversaAtualizadaAsync(new ConversaEventoRealtime(
                c.Id, c.OperadorResponsavelId, c.UnidadeId, c.TelefoneCanonical, c.NomeContato,
                c.UltimaMensagemPreview, c.NaoLidas, c.UltimaMensagemEm), ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Aviso em tempo real da mídia {Mensagem} falhou.", msg.Id);
        }
    }

    private static string Final(string telefone) => telefone.Length <= 4 ? telefone : telefone[^4..];
}
