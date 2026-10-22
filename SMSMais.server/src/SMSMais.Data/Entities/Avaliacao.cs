using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

public class Avaliacao
{
    public Guid Id { get; set; }
    public Guid SessaoId { get; set; }
    public int Nota { get; set; }
    public string? Comentario { get; set; }
    public DateTime CriadoEm { get; set; }

    /// <summary>Por onde chegou: painel (equipe) ou app do cidadão (o próprio paciente).</summary>
    public CanalAvaliacao Canal { get; set; } = CanalAvaliacao.Painel;

    /// <summary>Quem levou, em qual veículo e rota — guardados na hora da avaliação (a sessão pode
    /// ser corrigida depois; a nota fica com quem fez a viagem).</summary>
    public Guid? MotoristaId { get; set; }
    public Guid? VeiculoId { get; set; }
    public Guid? RotaId { get; set; }

    /// <summary>De qual parte da viagem é a nota: a ida (motorista que levou à unidade) ou o resgate
    /// (quem buscou e deixou em casa). Uma de cada por viagem.</summary>
    public TrechoViagem Trecho { get; set; } = TrechoViagem.Ida;

    public SessaoDeTratamento? Sessao { get; set; }
}
