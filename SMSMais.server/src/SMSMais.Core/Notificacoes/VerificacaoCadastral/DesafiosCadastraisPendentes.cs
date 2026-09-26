using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Notificacoes.VerificacaoCadastral;

/// <summary>
/// "Há desafio cadastral pendente para este número?" — a pergunta que a máquina de verificação
/// (<c>VerificacaoCadastralWhatsAppHandler</c>) e o robô (<c>RoboAtendimentoProcessador</c>) fazem.
/// Fica num lugar só para as duas pontas darem a mesma resposta: quando divergiam, o robô tratava
/// como conversa solta a resposta que a máquina esperava (ou o contrário).
///
/// <para><b>Retidas</b> são as comunicações paradas esperando a pessoa se identificar: a
/// confirmação (a "principal") e o lembrete de quem não respondeu, que repete a primeira
/// mensagem. <b>A janela conta do último TOQUE</b> da solicitação — a criação da retida, ou o envio
/// de um lembrete, reforço ou orientação ao posto. Contar só da criação deixava sem resposta quem
/// toca "Quero mais informações" na orientação ao posto de um agendamento importado há um mês.</para>
/// </summary>
internal static class DesafiosCadastraisPendentes
{
    /// <summary>Desafio sem toque há mais que isto não conta mais — a conversa esfriou.</summary>
    public static readonly TimeSpan Janela = TimeSpan.FromDays(20);

    /// <summary>Finalidades que ficam retidas em <see cref="StatusComunicacao.AguardandoVerificacaoCadastral"/>.</summary>
    internal static readonly FinalidadeComunicacao[] Retidas =
    [
        FinalidadeComunicacao.ConfirmacaoAgendamento,
        FinalidadeComunicacao.LembreteAgendamento,
    ];

    /// <summary>Mensagens que, ao sair, reabrem a janela do desafio da mesma solicitação.</summary>
    internal static readonly FinalidadeComunicacao[] Toques =
    [
        FinalidadeComunicacao.LembreteAgendamento,
        FinalidadeComunicacao.ReforcoConfirmacao,
        FinalidadeComunicacao.OrientacaoPosto,
    ];

    /// <summary>
    /// Comunicações retidas deste número dentro da janela, a do toque mais recente primeiro.
    /// Tudo no banco: o número é comparado em todas as formas com que aparece gravado (com e sem o
    /// nono dígito — ver <see cref="ReguaReforcoConfirmacao.FormasDoNumero"/>).
    /// </summary>
    public static async Task<List<(Guid Id, Guid PacienteId)>> DoTelefoneAsync(
        SmsMaisDbContext db, string telefone, CancellationToken ct)
    {
        var formas = ReguaReforcoConfirmacao.FormasDoNumero(telefone);
        if (formas.Length == 0) return [];
        var limite = DateTime.UtcNow.Subtract(Janela);

        var linhas = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(n => n.Status == StatusComunicacao.AguardandoVerificacaoCadastral
                && Retidas.Contains(n.Finalidade)
                && n.Telefone != null && formas.Contains(n.Telefone))
            .Select(n => new
            {
                n.Id,
                n.PacienteId,
                n.CriadoEm,
                UltimoToque = db.ComunicacoesPaciente
                    .Where(t => t.SolicitacaoId != null && t.SolicitacaoId == n.SolicitacaoId
                        && Toques.Contains(t.Finalidade))
                    .Max(t => t.EnviadoEm),
            })
            .Where(x => x.CriadoEm >= limite || x.UltimoToque >= limite)
            .OrderByDescending(x => x.UltimoToque > x.CriadoEm ? x.UltimoToque : (DateTime?)x.CriadoEm)
            .ToListAsync(ct);

        return [.. linhas.Select(x => (x.Id, x.PacienteId))];
    }
}
