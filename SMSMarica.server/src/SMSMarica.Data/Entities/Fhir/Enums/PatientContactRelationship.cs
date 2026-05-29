namespace SMSMarica.Data.Entities.Fhir.Enums;

/// <summary>
/// Subset de FHIR v3-RoleCode (relacionamento de contato/responsável).
/// Cobre os parentescos mais comuns no contexto BR.
/// </summary>
public enum PatientContactRelationship
{
    /// <summary>Outro / não especificado.</summary>
    Other = 0,
    /// <summary>MTH — Mother.</summary>
    Mother = 1,
    /// <summary>FTH — Father.</summary>
    Father = 2,
    /// <summary>SPS — Spouse / cônjuge / companheiro(a).</summary>
    Spouse = 3,
    /// <summary>CHILD — Filho/filha.</summary>
    Child = 4,
    /// <summary>SIB — Sibling (irmão/irmã).</summary>
    Sibling = 5,
    /// <summary>GRPRN — Grandparent.</summary>
    Grandparent = 6,
    /// <summary>GRNDCHILD — Grandchild.</summary>
    Grandchild = 7,
    /// <summary>GUARD — Guardian (responsável legal).</summary>
    Guardian = 8,
    /// <summary>EMRG — Contato de emergência.</summary>
    Emergency = 9,
    /// <summary>FRND — Amigo(a).</summary>
    Friend = 10,
    /// <summary>NBOR — Vizinho(a).</summary>
    Neighbor = 11,
    /// <summary>CARE — Cuidador(a).</summary>
    Caregiver = 12,
}
