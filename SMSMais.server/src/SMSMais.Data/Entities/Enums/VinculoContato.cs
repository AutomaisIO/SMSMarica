namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Vínculo que a pessoa que atende o número declara ter com o paciente do cadastro, quando
/// diz não ser o próprio. Só para REGISTRO — o robô não corrige cadastro. Valor estável.
/// </summary>
public enum VinculoContato
{
    NaoInformado = 1,
    Parente = 2,
    Responsavel = 3,
    /// <summary>Sem vínculo — engano/número trocado.</summary>
    SemVinculo = 4,
}
