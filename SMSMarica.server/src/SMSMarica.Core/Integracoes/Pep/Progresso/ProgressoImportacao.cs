namespace SMSMarica.Core.Integracoes.Pep.Progresso;

/// <summary>
/// Acumulador mutável de progresso de uma importação: fase corrente, contadores por tipo
/// de recurso, cronometragem por fase e falhas por paciente. É escrito por UMA thread
/// (o runner) e lido pelo endpoint de status via <see cref="PepSincronizacaoEstadoVivo"/>.
/// </summary>
public sealed class ProgressoImportacao
{
    public string FaseAtual { get; set; } = "iniciando";

    public int Medicos;
    public int Pacientes;
    public int Encounters;
    public int Conditions;
    public int MedicationRequests;
    public int DocumentReferences;
    public int Observations;

    /// <summary>
    /// Total de reenvios por motivo transitório (saturação de conexão no hub). É o
    /// "farol" de backpressure: sobe enquanto há registros pendentes esperando o
    /// banco liberar; não é falha — esses registros não foram descartados.
    /// </summary>
    /// <summary>
    /// Pacientes importados SEM CPF, marcados com a tag de identidade incompleta. É o número
    /// que responde "quantos dados incertos entraram neste ciclo" — antes de 03/08 eles eram
    /// descartados em silêncio, sem nenhum contador.
    /// </summary>
    public int PacientesIdentidadeIncompleta;

    public int Retentativas;

    /// <summary>Falhas por paciente: (cd_paciente, mensagem).</summary>
    public List<(long Cd, string Mensagem)> Falhas { get; } = [];

    /// <summary>Duração por fase em segundos (ex.: "medicos", "pacientes", "atendimentos").</summary>
    public Dictionary<string, double> Tempos { get; } = [];
}
