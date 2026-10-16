using SMSMais.Core.Worklist;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Equipamentos.Validators;

/// <summary>
/// Régua das tags dos campos opcionais da worklist (ADR-0074, adendo de 16/10/2026) — a mesma para
/// cadastrar e atualizar. A tag em si é julgada por <see cref="TagDicomWorklist.Problema"/>; aqui
/// fica o que depende do formulário: vazio = o padrão do campo, e dois campos não dividem uma tag.
/// </summary>
internal static class RegrasCamposWorklist
{
    /// <summary>Problema da tag do código, ou null. Vazio e (0008,0100) são o padrão (na sequência).</summary>
    public static string? ProblemaTagCodigo(string? tag) =>
        EquipamentosMapper.TagCodigo(tag) is null ? null : TagDicomWorklist.Problema(tag);

    /// <summary>Problema da tag de um campo ligado, ou null. Vazio = o padrão do campo.</summary>
    public static string? ProblemaTagCampo(string? tag) =>
        string.IsNullOrWhiteSpace(tag) ? null : TagDicomWorklist.Problema(tag);

    /// <summary>
    /// Dois campos na mesma tag — o segundo apagaria o primeiro no item. Recebe as tags
    /// <b>efetivas</b> (já com o padrão de cada campo aplicado; null = campo desligado).
    /// </summary>
    public static string? Repetida(string? codigo, string? medico, string? unidade)
    {
        var repetida = new[] { codigo, medico, unidade }
            .Where(t => t is not null)
            .GroupBy(t => t!)
            .FirstOrDefault(g => g.Count() > 1)?.Key;
        return repetida is null
            ? null
            : $"Dois campos estão na mesma tag ({TagDicomWorklist.Formatar(repetida)}): um apagaria o outro no item da worklist.";
    }

    /// <summary>As três tags efetivas de um formulário completo (o que o painel manda).</summary>
    public static string? Repetida(
        string? codigoTag, bool enviarMedico, string? medicoTag,
        bool enviarUnidade, OrigemUnidadeWorklist origem, string? unidadeTag) =>
        Repetida(
            EquipamentosMapper.TagCodigo(codigoTag),
            EquipamentosMapper.TagLigada(enviarMedico, medicoTag, TagDicomWorklist.MedicoSolicitantePadrao),
            EquipamentosMapper.TagLigada(enviarUnidade, unidadeTag, TagDicomWorklist.UnidadePadrao(origem)));
}
