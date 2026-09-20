using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Ouvidoria;

/// <summary>
/// Linha da trilha da manifestação. <b>Append-only</b>: nunca UPDATE nem DELETE — correção é
/// evento novo. É daqui que sai o acompanhamento do cidadão (só os <see cref="VisivelAoCidadao"/>)
/// e o histórico interno da ouvidoria.
/// </summary>
public sealed class OuvidoriaEvento
{
    public Guid Id { get; set; }
    public Guid ManifestacaoId { get; set; }

    public OuvidoriaTipoEvento Tipo { get; set; }

    /// <summary>Status antes/depois quando o evento é uma transição; nulos em anotação, cobrança etc.</summary>
    public OuvidoriaStatus? StatusAnterior { get; set; }
    public OuvidoriaStatus? StatusNovo { get; set; }

    /// <summary>Usuário que fez a ação; nulo em ação do cidadão pelo canal público ou de rotina automática.</summary>
    public Guid? AutorId { get; set; }

    /// <summary>Nome do autor desnormalizado: o histórico não pode mudar se o usuário for renomeado ou excluído.</summary>
    public string? AutorNome { get; set; }

    /// <summary>Ponto de resposta envolvido (encaminhamento, resposta da área, cobrança). Filtro do "meu ponto".</summary>
    public Guid? PontoRespostaId { get; set; }

    public string? Texto { get; set; }

    /// <summary>
    /// Se aparece no acompanhamento público. Decidido por evento e não pelo tipo: um encaminhamento
    /// é visível, mas sem nomear pessoas; a resposta da área não é (o cidadão recebe a da ouvidoria).
    /// </summary>
    public bool VisivelAoCidadao { get; set; }

    public DateTime CriadoEm { get; set; }

    public OuvidoriaManifestacao? Manifestacao { get; set; }
}
