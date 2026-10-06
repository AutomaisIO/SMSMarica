using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Notificacoes.WhatsApp.Manipuladores;
using SMSMais.Data;
using SMSMais.Data.Entities.AgenteIa;

namespace SMSMais.Core.AgenteIa.WhatsApp;

/// <summary>Acorda o <see cref="AgenteWhatsAppWorker"/> quando chega pedido (sem esperar o próximo ciclo).</summary>
public sealed class SinalAgenteWhatsApp
{
    private readonly Channel<bool> _canal = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Sinalizar() => _canal.Writer.TryWrite(true);

    internal ChannelReader<bool> Leitor => _canal.Reader;
}

/// <summary>
/// Mensagem do celular de aviso com o Agente IA ligado (ADR-0068): vira um
/// <see cref="AgenteWhatsAppPedido"/> e mais nada. Roda PRIMEIRO e encerra a cadeia — robô,
/// confirmação de agendamento, acompanhante e verificação cadastral nunca veem a mensagem.
/// Não chama SaveChanges: o webhook grava o pedido junto com os efeitos dos manipuladores.
/// </summary>
public sealed class AgenteIaWhatsAppHandler(
    SmsMaisDbContext db,
    ITelefonesAgenteIa telefones,
    SinalAgenteWhatsApp sinal) : IManipuladorMensagemWhatsApp
{
    /// <summary>Limite do texto citado levado ao agente — um aviso tem até ~3.000 caracteres.</summary>
    private const int MaxCitado = 6000;

    public int Ordem => 1;

    public async Task TratarAsync(ManipuladorContexto ctx, CancellationToken ct)
    {
        var dono = await telefones.ObterAsync(ctx.Conversa.TelefoneCanonical, ct);
        if (dono is null) return;

        ctx.Consumido = true;
        ctx.Encerrado = true;
        // A conversa não aparece no módulo Conversas; sem isto o contador do sino somaria o que
        // ninguém consegue abrir.
        ctx.Conversa.NaoLidas = 0;
        // Foto/PDF mandado ao agente não é documento de paciente: fora da fila de download, que
        // termina no acervo (ADR-0066) quando o número também é cadastro de alguém.
        ctx.Mensagem.MidiaSituacao = null;

        // Respondeu citando uma mensagem (normalmente um aviso de erro): o texto dela vai junto.
        string? citado = null;
        if (!string.IsNullOrEmpty(ctx.Mensagem.ContextoWaMessageId))
        {
            citado = await db.MensagensWhatsApp.AsNoTracking()
                .Where(m => m.WaMessageId == ctx.Mensagem.ContextoWaMessageId)
                .Select(m => m.Conteudo)
                .FirstOrDefaultAsync(ct);
            if (citado is { Length: > MaxCitado }) citado = citado[..MaxCitado];
        }

        db.AgenteWhatsAppPedidos.Add(new AgenteWhatsAppPedido
        {
            Id = Guid.CreateVersion7(),
            Telefone = TelefonesAgenteIa.Chave(ctx.Conversa.TelefoneCanonical),
            UsuarioId = dono.UsuarioId,
            MensagemId = ctx.Mensagem.Id,
            // Sem texto = áudio, foto, documento: o worker responde que por enquanto só lê texto.
            Texto = ctx.Texto?.Trim() ?? string.Empty,
            Citado = string.IsNullOrWhiteSpace(citado) ? null : citado,
            Situacao = SituacaoPedidoAgente.Pendente,
            CriadoEm = DateTime.UtcNow,
        });

        // Antes do commit do webhook: se o worker acordar cedo demais, acha o pedido no próximo ciclo.
        sinal.Sinalizar();
    }
}
