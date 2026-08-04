namespace Automais.Fhir.Data.Entities;

/// <summary>
/// Armazenamento do recurso FHIR <c>Organization</c> — a UNIDADE DE SAÚDE (ADR-0039).
///
/// <para><b>Por que este recurso existe.</b> A unidade é o eixo DURÁVEL do dado clínico; o PEP
/// é proveniência transitória. A UPA Inoã e o PA Santa Rita saíram do Salux para o Klinikos em
/// abril/2025 sem deixar de ser as mesmas unidades, e o Conde fará o mesmo caminho. Sem uma
/// Organization canônica, a linha do tempo de cada unidade quebraria a cada troca de sistema —
/// e é exatamente isso que um repositório longitudinal não pode fazer.</para>
///
/// <para><b>Identidade</b>: a âncora é o <see cref="ResourceRow.Id"/> interno. O <b>CNES é
/// identifier, não chave</b> — ele muda (recredenciamento, correção de cadastro) e uma unidade
/// pode operar antes de ter CNES definitivo. Cada PEP anexa o seu código próprio
/// (<c>urn:salux:hospital</c>, <c>urn:klinikos:unidade</c>) ao MESMO recurso, do mesmo jeito
/// que o Patient acumula CPF, CNS e código de prontuário de cada base.</para>
/// </summary>
public sealed class OrganizationRow : ResourceRow
{
    /// <summary>Nome oficial (Organization.name).</summary>
    public string? Name { get; set; }

    /// <summary>CNES, quando houver — coluna própria porque é a busca natural por unidade.</summary>
    public string? Cnes { get; set; }

    /// <summary>Ativa (Organization.active).</summary>
    public bool Ativa { get; set; } = true;

    /// <summary>Hierarquia futura (rede/município) via Organization.partOf — reservado pelo ADR-0039.</summary>
    public Guid? PartOfId { get; set; }

    /// <summary>System do identifier usado no upsert condicional desta linha.</summary>
    public string? IdentifierSystem { get; set; }

    /// <summary>Valor do identifier (CNES, ou o código interno prefixado pelo slug da base).</summary>
    public string? IdentifierValue { get; set; }
}
