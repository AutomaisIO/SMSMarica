using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Equipamentos;

/// <summary>
/// O que o aparelho de um exame diz sobre ele depois que chega (ADR-0075): onde nasce o laudo,
/// quando o paciente recebe e se há anamnese. Quem resolve é o <see cref="IRegrasDoExameService"/>,
/// para que a chegada, o envio, o app do cidadão, o PACS e o laudo deem a mesma resposta.
/// </summary>
/// <param name="EquipamentoNome">Para as mensagens ("o aparelho X só libera com o laudo"). Com mais
/// de um candidato, os nomes de todos.</param>
public sealed record RegraDoExame(
    Guid? EquipamentoId,
    string? EquipamentoNome,
    OrigemLaudo Laudo,
    LiberacaoExamePaciente Liberacao,
    bool UsaAnamnese)
{
    public bool LaudoNaPlataforma => Laudo == OrigemLaudo.Plataforma;

    public bool LiberaAoChegar => Liberacao == LiberacaoExamePaciente.AoChegar;

    /// <summary>
    /// Nenhum aparelho a apontar (exame sem tipo, modalidade sem aparelho na unidade). Fica no lado
    /// seguro: retido até o laudo, laudo na plataforma, com anamnese — que é o que valia para todo
    /// exame, quanto a laudo e anamnese, antes do ADR-0075.
    /// </summary>
    public static readonly RegraDoExame SemAparelho = new(
        null, null, OrigemLaudo.Plataforma, LiberacaoExamePaciente.ComLaudoAssinado, UsaAnamnese: true);

    /// <summary>
    /// A regra que um aparelho novo recebe pela modalidade quando o cadastro não diz — a mesma da
    /// implantação de 20/10/2026 e da tela: ultrassom é laudado fora e libera ao chegar; o resto é
    /// laudado aqui e retém até o laudo.
    /// </summary>
    public static (OrigemLaudo Laudo, LiberacaoExamePaciente Liberacao) PadraoDaModalidade(ModalidadeDicom m) =>
        m == ModalidadeDicom.US
            ? (OrigemLaudo.ForaDaPlataforma, LiberacaoExamePaciente.AoChegar)
            : (OrigemLaudo.Plataforma, LiberacaoExamePaciente.ComLaudoAssinado);

    public static RegraDoExame De(Equipamento e) =>
        new(e.Id, e.Nome, e.Laudo, e.LiberacaoPaciente, e.UsaAnamnese);

    /// <summary>
    /// Vários aparelhos possíveis e nenhum escolhido — os dois ultrassons do CDT, os dois do CMI.
    /// Concordando todos, vale a regra deles. Discordando, cada campo cai para o lado que não erra:
    /// libera ao chegar só se TODOS liberam (soltar a imagem antes da hora não tem volta); laudo na
    /// plataforma e anamnese valem se ALGUM usa (mostrar um botão a mais custa menos do que esconder
    /// o que alguém precisa).
    /// <para>Nunca sai "só com o laudo" com o laudo fora: "só com o laudo" vem de algum candidato,
    /// que por isso lauda na plataforma — e aí o laudo combinado também é na plataforma.</para>
    /// </summary>
    public static RegraDoExame Combinar(IReadOnlyList<RegraDoExame> candidatos)
    {
        if (candidatos.Count == 0) return SemAparelho;
        if (candidatos.Count == 1) return candidatos[0];

        var liberacao = candidatos.All(c => c.LiberaAoChegar)
            ? LiberacaoExamePaciente.AoChegar
            : LiberacaoExamePaciente.ComLaudoAssinado;
        var laudo = candidatos.Any(c => c.LaudoNaPlataforma) ? OrigemLaudo.Plataforma
            : candidatos.All(c => c.Laudo == candidatos[0].Laudo) ? candidatos[0].Laudo
            : OrigemLaudo.ForaDaPlataforma;

        return new RegraDoExame(
            EquipamentoId: null,
            EquipamentoNome: string.Join(" ou ", candidatos.Select(c => c.EquipamentoNome).Distinct()),
            laudo,
            liberacao,
            UsaAnamnese: candidatos.Any(c => c.UsaAnamnese));
    }
}
