using System.Globalization;
using System.Text.Json;
using SMSMais.Data.Entities.EsusSg;

namespace SMSMais.Core.Integracoes.EsusSgWeb;

/// <summary>
/// Uma linha de EXAME do "Histórico de Atendimentos do Paciente"
/// (<c>pacientes/controller-paciente/buscar-historico-geral-paciente</c>).
///
/// <para><b><see cref="IdFila"/> não é o nosso <c>fil_id</c></b> (medido em 01/10/2026: 2105231 no
/// histórico × 4064691 nas listas). O casamento com o espelho é pela data do agendamento e pelo
/// <c>fil_id</c> que o DETALHE do exame traz.</para>
/// </summary>
/// <param name="IdsExame">O campo <c>id</c> pode vir com vários exames: <c>"78090,78093"</c>.</param>
public sealed record EsusSgHistoricoExame(string? IdFila, IReadOnlyList<long> IdsExame, DateOnly? DataAgendamento)
{
    /// <summary>Só módulo de EXAMES (33); consulta, internação etc. ficam de fora.</summary>
    public const string ModuloExames = "33";

    public static EsusSgHistoricoExame? De(JsonElement r)
    {
        if (EsusSgJson.Texto(r, "id_modulo") != ModuloExames) return null;
        var ids = (EsusSgJson.Texto(r, "id") ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => long.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToList();
        return ids.Count == 0 ? null : new(EsusSgJson.Texto(r, "id_fila"), ids, EsusSgJson.Data(r, "data_agendamento"));
    }
}

/// <summary>
/// Um evento da trilha de um exame (<c>buscar-detalhes-historico-exame-paciente</c>): AGENDADO, ALTERADO,
/// EFETIVADO, NÃO EFETIVADO, ALTERADO NA EFETIVAÇÃO, MODIFICADO PARA EM ABERTO… O ESUS repete a mesma
/// linha várias vezes (junção interna) — inofensivo para <see cref="EfetivacaoEsusSgResolvida"/>.
/// </summary>
/// <param name="Ordem">O <c>@rownum</c> do ESUS — desempate entre eventos do mesmo minuto.</param>
public sealed record EsusSgEventoExame(
    string? FilId,
    string? Evento,
    EfetivacaoEsusSg? Efetivacao,
    DateTime? EfetivadoEm,
    string? MotivoNaoEfetivacao,
    DateTime? Registro,
    DateOnly? DataExame,
    int Ordem)
{
    public static EsusSgEventoExame De(JsonElement r) => new(
        FilId: EsusSgJson.Texto(r, "fil_id"),
        Evento: EsusSgJson.Texto(r, "tlg_nome"),
        Efetivacao: EsusSgJson.Inteiro(r, "efl_id_exames_efetivacao") is int e && Enum.IsDefined(typeof(EfetivacaoEsusSg), e)
            ? (EfetivacaoEsusSg)e
            : null,
        EfetivadoEm: DataHoraComTraco(EsusSgJson.Texto(r, "data_efetivacao")),
        MotivoNaoEfetivacao: EsusSgJson.Texto(r, "motivo_nao_efetivacao"),
        Registro: DataHoraComTraco(EsusSgJson.Texto(r, "data_hora_log")),
        DataExame: EsusSgJson.Data(r, "data_exame"),
        Ordem: EsusSgJson.Inteiro(r, "@rownum := @rownum+1") ?? 0);

    /// <summary>A trilha escreve <c>"16/06/2026 - 15:42"</c> (às vezes com segundos) em Brasília.</summary>
    internal static DateTime? DataHoraComTraco(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : EsusSgJson.DataHora(s.Replace(" - ", " ", StringComparison.Ordinal));
}

/// <summary>O estado final de um agendamento, tirado da trilha do(s) exame(s).</summary>
public sealed record EfetivacaoEsusSgResolvida(EfetivacaoEsusSg? Estado, DateTime? EfetivadoEm, string? Motivo)
{
    public static readonly EfetivacaoEsusSgResolvida Nada = new(null, null, null);

    /// <summary>
    /// Vale o ÚLTIMO apontamento (a unidade pode efetivar e depois mudar). Com vários exames no mesmo
    /// agendamento, um efetivado basta para "compareceu"; senão vale o último de todos.
    /// </summary>
    public static EfetivacaoEsusSgResolvida Resolver(IEnumerable<IEnumerable<EsusSgEventoExame>> trilhasPorExame)
    {
        var finais = trilhasPorExame
            .Select(t => t.Where(e => e.Efetivacao is not null)
                .OrderBy(e => e.Registro ?? DateTime.MinValue)
                .ThenBy(e => e.Ordem)
                .LastOrDefault())
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();
        if (finais.Count == 0) return Nada;

        var escolhido = finais.FirstOrDefault(e => e.Efetivacao == EfetivacaoEsusSg.Efetivado)
                        ?? finais.OrderBy(e => e.Registro ?? DateTime.MinValue).ThenBy(e => e.Ordem).Last();
        return new(escolhido.Efetivacao, escolhido.EfetivadoEm,
            escolhido.Efetivacao == EfetivacaoEsusSg.NaoEfetivado ? escolhido.MotivoNaoEfetivacao : null);
    }
}
