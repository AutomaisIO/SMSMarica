namespace SMSMais.Data.Entities;

/// <summary>
/// Tipo (catálogo) de tratamento do Transporte de Pacientes: Hemodiálise, Radioterapia, etc.
/// Mantido pela tela "Tipos de tratamento". É do tipo que vem o tempo médio que o paciente fica
/// na unidade — o atendimento não tem tempo próprio.
/// </summary>
public class TipoTratamento
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Tempo médio que o paciente fica no tratamento, em minutos (da chegada à
    /// liberação). Base para prever a volta na rota. Nulo só em linha anterior ao campo.</summary>
    public int? TempoMedioMinutos { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
}
