namespace SMSMais.Data.Entities.Enums;

/// <summary>Como um estudo do PACS foi vinculado a uma solicitação de exame.</summary>
public enum OrigemAssociacaoExame
{
    /// <summary>Associação manual feita por um operador na tela de Exames.</summary>
    Manual = 1,

    /// <summary>
    /// Associação automática na chegada do exame — o campo DICOM Patient ID
    /// (0010,0020) trazia o número da solicitação e o sincronizador casou.
    /// </summary>
    Automatica = 2,
}
