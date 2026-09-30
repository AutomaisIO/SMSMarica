namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// O que fazer com a autorização do VIDaaS de um médico no modo Nuvem (ADR-0061 §2.1). O próprio
/// médico escolhe no modal da primeira assinatura ("Não perguntar de novo") e o administrador
/// pode trocar no cadastro, junto do modo.
/// </summary>
public enum PreferenciaSessaoNuvem
{
    /// <summary>
    /// Padrão. Na primeira assinatura de cada acesso, o painel pergunta se mantém a autorização
    /// até sair do sistema ou se vale só para aquele laudo.
    /// </summary>
    Perguntar = 0,

    /// <summary>Aprova no app uma vez e assina os demais laudos sem o celular, até sair do sistema.</summary>
    Manter = 1,

    /// <summary>Cada assinatura pede aprovação no app (o comportamento anterior à sessão).</summary>
    CadaLaudo = 2,
}
