using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Conversas;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.Notificacoes.Comunicacao;

/// <summary>
/// Guarda, no cadastro, o que o envio descobriu sobre o contato do paciente: que não há celular,
/// ou que o número existe mas não está no WhatsApp. É o que alimenta a aba <b>Telefone
/// comprometido</b> em Confirmações.
///
/// <para>A marca é um fato observado, não um julgamento — e por isso ela mesma se desfaz: assim
/// que o cadastro passa a ter um número diferente (ou passa a ter algum), as marcas que falavam do
/// número antigo são fechadas por <see cref="ReconciliarAsync"/>. Sem isso a lista viraria um
/// cemitério de problemas já resolvidos, e a recepção deixaria de confiar nela.</para>
/// </summary>
public interface IContatoComprometidoService
{
    /// <summary>
    /// Registra (ou reforça) a marca. Repetição não cria linha nova: incrementa o contador e move
    /// a última ocorrência — o número de tentativas queimadas é o que mostra urgência.
    /// </summary>
    Task MarcarAsync(
        Guid pacienteId, string? telefone, MotivoContatoComprometido motivo,
        string? detalhe = null, Guid? comunicacaoId = null, CancellationToken ct = default);

    /// <summary>
    /// Fecha as marcas que o cadastro atual já não sustenta: as de outro número, e as de
    /// "sem celular" quando agora existe um. Chamada sempre que um telefone é resolvido com sucesso.
    /// </summary>
    Task ReconciliarAsync(Guid pacienteId, string? telefoneAtual, CancellationToken ct = default);
}

public sealed class ContatoComprometidoService(SmsMaisDbContext db) : IContatoComprometidoService
{
    public async Task MarcarAsync(
        Guid pacienteId, string? telefone, MotivoContatoComprometido motivo,
        string? detalhe = null, Guid? comunicacaoId = null, CancellationToken ct = default)
    {
        // "Sem celular" não tem número para guardar; o índice único exige uma chave, então vazio.
        var canonical = string.IsNullOrWhiteSpace(telefone) ? string.Empty : TelefoneWhatsApp.Canonizar(telefone);
        var agora = DateTime.UtcNow;

        var aberta = await db.ContatosComprometidos.FirstOrDefaultAsync(
            c => c.PacienteId == pacienteId && c.TelefoneCanonical == canonical
                 && c.Motivo == motivo && c.ResolvidoEm == null, ct);

        if (aberta is not null)
        {
            aberta.UltimaOcorrenciaEm = agora;
            aberta.Ocorrencias++;
            if (detalhe is not null) aberta.Detalhe = Truncar(detalhe);
            return;
        }

        db.ContatosComprometidos.Add(new ContatoComprometido
        {
            Id = Guid.NewGuid(),
            PacienteId = pacienteId,
            TelefoneCanonical = canonical,
            Motivo = motivo,
            Detalhe = Truncar(detalhe),
            ComunicacaoId = comunicacaoId,
            DescobertoEm = agora,
            UltimaOcorrenciaEm = agora,
            Ocorrencias = 1,
        });
    }

    public async Task ReconciliarAsync(Guid pacienteId, string? telefoneAtual, CancellationToken ct = default)
    {
        var canonical = string.IsNullOrWhiteSpace(telefoneAtual) ? null : TelefoneWhatsApp.Canonizar(telefoneAtual);
        if (canonical is null) return;

        var abertas = await db.ContatosComprometidos
            .Where(c => c.PacienteId == pacienteId && c.ResolvidoEm == null)
            .ToListAsync(ct);

        foreach (var c in abertas)
        {
            // A marca só continua valendo se ainda fala do número que o cadastro usa hoje.
            var aindaVale = c.Motivo != MotivoContatoComprometido.SemCelular
                            && c.TelefoneCanonical == canonical;
            if (aindaVale) continue;

            c.ResolvidoEm = DateTime.UtcNow;
            c.ResolucaoNota = c.Motivo == MotivoContatoComprometido.SemCelular
                ? "Cadastro passou a ter celular."
                : "Cadastro passou a usar outro número.";
        }
    }

    private static string? Truncar(string? s) =>
        s is null ? null : s.Length <= 500 ? s : s[..500];
}
