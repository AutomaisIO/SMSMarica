namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Modalidades DICOM (tag 0008,0060). Nomes batem com o padrão DICOM PS3.3 C.7.3.1.1.1.
/// Persistido como int — valores estáveis.
/// </summary>
public enum ModalidadeDicom
{
    /// <summary>
    /// Ainda não configurada. Não é modalidade DICOM — é a ausência dela, no tipo de exame que o
    /// SISREG criou sozinho e que ninguém configurou ainda. Um tipo assim nunca vai à worklist:
    /// mandar 0008,0060 vazio (ou chutado) ao equipamento é pior que não mandar nada.
    /// </summary>
    Indefinida = 0,

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

    /// <summary>
    /// Electrocardiography — também o Holter (Ambulatory ECG Waveform é modalidade ECG no padrão).
    /// No CDT, o Holter sai do CardioNet DICOM (AE <c>CARDIO-CDT</c>); o eletrocardiograma comum
    /// não tem exame (a máquina imprime) e o tipo dele fica Indefinida. A MAPA foi ECG até
    /// 19/10/2026 e passou a <see cref="OT"/>, em aparelho próprio (<c>MAPAS-CDT</c>): o DICOM não
    /// tem modalidade de pressão arterial ambulatorial, e OT é a do padrão para "outro". No CDT a
    /// densitometria também é OT — cada uma tem aparelho fixo no escopo da unidade.
    /// </summary>
    ECG = 9,

    /// <summary>Other.</summary>
    OT = 99,
}
