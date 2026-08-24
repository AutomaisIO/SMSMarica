using Microsoft.EntityFrameworkCore;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Notificacoes.WhatsApp.Manipuladores;

/// <summary>
/// Interpreta a resposta sim/não do cidadão como confirmação de acompanhante do TFD e atualiza a
/// próxima sessão pendente. Antes vivia hard-coded no webhook; virou manipulador plugável para o
/// caminho principal (chat multi-operador) ficar genérico.
/// </summary>
public sealed class AcompanhanteWhatsAppHandler(SmsMaisDbContext db) : IManipuladorMensagemWhatsApp
{
    public int Ordem => 100;

    public async Task TratarAsync(ManipuladorContexto ctx, CancellationToken ct)
    {
        if (ctx.PacienteId is not { } pacienteId) return;

        var resposta = InterpretarSimNao(ctx.Texto);
        if (resposta is null) return;

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var sessao = await (
            from s in db.Sessoes
            join t in db.Tratamentos on s.TratamentoId equals t.Id
            where t.PacienteId == pacienteId
                && s.AcompanhanteEsperado == null
                && s.DataPrevista >= hoje
                && (s.Status == StatusSessao.Pendente || s.Status == StatusSessao.Confirmada)
            orderby s.DataPrevista
            select s).FirstOrDefaultAsync(ct);

        if (sessao is null) return;

        sessao.AcompanhanteEsperado = resposta.Value;
        sessao.AcompanhanteConfirmadoEm = DateTime.UtcNow;
        sessao.AcompanhanteCanal = CanalConfirmacao.WhatsApp;
        sessao.AtualizadoEm = DateTime.UtcNow;
    }

    private static bool? InterpretarSimNao(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim().ToLowerInvariant();
        if (t is "1" or "sim" or "s" or "com" || t.Contains("com acompanhante")) return true;
        if (t is "2" or "nao" or "não" or "n" or "sem" || t.Contains("sem acompanhante")) return false;
        return null;
    }
}
