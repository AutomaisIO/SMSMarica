namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// A que título aquele número recebe as mensagens daquele paciente. Declarado por quem passa na
/// verificação do contato e gravado no cadastro (LGPD: precisa ficar registrado por que uma
/// pessoa recebe dado de outra).
/// <para>É o que permite o celular da mãe atender pelos três filhos sem virar "número duplicado".</para>
/// </summary>
public enum VinculoContatoVerificado
{
    /// <summary>É o telefone do próprio paciente.</summary>
    Proprio = 1,

    /// <summary>Mãe, pai ou responsável legal de quem não tem telefone próprio (criança, idoso).</summary>
    MaeOuPaiOuResponsavel = 2,

    /// <summary>Outro parente ou cuidador que recebe pela pessoa.</summary>
    OutroParenteOuCuidador = 3,
}
