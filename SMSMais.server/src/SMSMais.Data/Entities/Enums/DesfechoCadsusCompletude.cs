namespace SMSMais.Data.Entities.Enums;

/// <summary>Desfecho de uma tentativa de completar ficha sem CPF via CADSUS (ver
/// <see cref="Sisreg.CadsusCompletude"/>).</summary>
public enum DesfechoCadsusCompletude
{
    /// <summary>A fonte respondeu e o CNS não está no CADSUS (temporário ou errado na origem).</summary>
    SemFicha = 1,

    /// <summary>A ficha existe mas não traz CPF com dígito verificador válido.</summary>
    SemCpf = 2,

    /// <summary>O CPF estava livre e foi carimbado na própria ficha.</summary>
    CpfCarimbado = 3,

    /// <summary>O CPF já pertencia a outro cadastro — este CNS passa a resolver para ele
    /// (<see cref="Sisreg.CadsusCompletude.PacienteDestinoId"/>).</summary>
    RepontadoParaExistente = 4,
}
