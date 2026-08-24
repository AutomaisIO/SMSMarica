namespace SMSMais.Core.Laudos.Pdf;

/// <summary>
/// Estado visual do PDF do laudo. Governa marca d'água, tarja de rodapé e a
/// AUSÊNCIA do bloco de identificação do médico em texto solto.
/// <para>
/// Regra médico-legal (Resolução CFM 2.299/2021): a identidade do médico
/// (nome/CRM/RQE + rubrica) só tem fé pública quando acompanha a assinatura
/// digital ICP-Brasil — por isso ela aparece <b>exclusivamente</b> no carimbo
/// estampado pelo Automais.Assinador no PDF assinado, nunca no PDF on-demand.
/// </para>
/// </summary>
public enum ModoRodapeLaudo
{
    /// <summary>
    /// Rascunho (laudo não finalizado): marca d'água diagonal forte
    /// "RASCUNHO — SEM VALIDADE" no corpo. Sem tarja, sem "Emitido em", sem
    /// bloco do médico. Estado derivado do <c>StatusLaudo</c>, nunca explícito.
    /// </summary>
    Rascunho,

    /// <summary>
    /// Finalizado, porém sem assinatura digital (PDF on-demand): tarja neutra
    /// "documento sem assinatura digital" + "Emitido em". Sem bloco do médico.
    /// </summary>
    FinalizadoNaoAssinado,

    /// <summary>
    /// PDF-base entregue ao Automais.Assinador para estampar o carimbo: sem
    /// tarja, sem marca d'água e sem bloco do médico — o documento que será
    /// assinado não pode declarar que não está assinado, e a identidade entra
    /// pelo carimbo da assinatura.
    /// </summary>
    PreparandoAssinatura,
}
