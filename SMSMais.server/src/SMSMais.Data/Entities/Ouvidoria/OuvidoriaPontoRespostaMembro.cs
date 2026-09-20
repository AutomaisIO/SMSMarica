namespace SMSMais.Data.Entities.Ouvidoria;

/// <summary>
/// Usuário que responde por um ponto de resposta. É o pertencimento aqui que dá ao usuário com
/// <c>OuvidoriaPontoResposta</c> o direito de ver as manifestações encaminhadas ao ponto.
/// </summary>
public sealed class OuvidoriaPontoRespostaMembro
{
    public Guid Id { get; set; }
    public Guid PontoRespostaId { get; set; }

    /// <summary>FK para <c>smsmarica.usuario</c>.</summary>
    public Guid UsuarioId { get; set; }

    /// <summary>Titular recebe a cobrança primeiro; os demais são suplentes.</summary>
    public bool Titular { get; set; }

    public DateTime CriadoEm { get; set; }

    public OuvidoriaPontoResposta? PontoResposta { get; set; }
}
