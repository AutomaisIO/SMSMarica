namespace SMSMais.Data.Entities;

/// <summary>Quem vai acompanhar o paciente numa viagem (sessão). No máximo
/// <see cref="Tratamento.QuantidadeAcompanhantes"/> por sessão, todos da lista do paciente.</summary>
public class SessaoAcompanhante
{
    public Guid SessaoId { get; set; }
    public Guid AcompanhanteId { get; set; }

    public SessaoDeTratamento? Sessao { get; set; }
    public Acompanhante? Acompanhante { get; set; }
}
