namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Modalidades DICOM (tag 0008,0060). Nomes batem com o padrão DICOM PS3.3 C.7.3.1.1.1.
/// Persistido como int — valores estáveis.
/// </summary>
public enum ModalidadeDicom
{
    /// <summary>Computed Radiography (RX digital placas CR).</summary>
    CR = 1,

    /// <summary>Digital Radiography (RX digital direto).</summary>
    DX = 2,

    /// <summary>Mammography.</summary>
    MG = 3,

    /// <summary>Ultrasound.</summary>
    US = 4,

    /// <summary>Computed Tomography.</summary>
    CT = 5,

    /// <summary>Magnetic Resonance.</summary>
    MR = 6,

    /// <summary>Nuclear Medicine.</summary>
    NM = 7,

    /// <summary>Positron Emission Tomography.</summary>
    PT = 8,

    /// <summary>Other.</summary>
    OT = 99,
}
