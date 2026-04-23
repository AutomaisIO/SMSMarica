namespace SMSMarica.Data.Entities;

public class Tratamento
{
    public Guid Id { get; set; }
    public Guid PacienteId { get; set; }
    public Guid UnidadeId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? EncerradoEm { get; set; }

    public Paciente? Paciente { get; set; }
    public Unidade? Unidade { get; set; }
    public Periodicidade? Periodicidade { get; set; }
    public List<SessaoDeTranslado> Sessoes { get; set; } = [];
}
