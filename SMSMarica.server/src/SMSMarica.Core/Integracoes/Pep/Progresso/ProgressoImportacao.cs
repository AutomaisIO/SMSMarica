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

    /// <summary>
    /// Quantos dos <see cref="Pacientes"/> a guarda de no-op descartou — o recurso montado era
    /// idêntico ao que o hub já tinha. É o complemento honesto do contador bruto: num ciclo
    /// incremental a maior parte do que entra é <b>releitura obrigatória</b> (internação em
    /// curso volta todo poll — ADR-0025), não gente que mudou. Quem mudou de verdade é
    /// <c>Pacientes - PacientesInalterados</c>.
    /// </summary>
    public int PacientesInalterados;

    /// <summary>O mesmo para <see cref="Medicos"/>, que é re-scan integral do cadastro.</summary>
    public int MedicosInalterados;

    /// <summary>
    /// Teto do DETALHE de falhas em memória. No incidente de 04/08 um run acumulou 1,43 milhão
    /// de tuplas nesta lista — dezenas de MB no heap e um <c>falhas_json</c> gigante — sem
    /// nenhuma informação nova depois da centésima: eram todas o mesmo 405. O TOTAL continua
    /// exato em <see cref="FalhasTotal"/>; só o detalhe é amostrado.
    /// </summary>
    public const int MaxDetalheFalhas = 500;

    /// <summary>Total REAL de falhas do run — conta além do teto do detalhe.</summary>
    public int FalhasTotal;

    /// <summary>Falhas por paciente: (cd_paciente, mensagem). Amostra — ver <see cref="MaxDetalheFalhas"/>.</summary>
    public List<(long Cd, string Mensagem)> Falhas { get; } = [];

    private readonly Lock _falhasLock = new();

    /// <summary>Registra uma falha: total sempre conta; o detalhe para no teto. Thread-safe.</summary>
    public void RegistrarFalha(long cd, string mensagem)
    {
        lock (_falhasLock)
        {
            FalhasTotal++;
            if (Falhas.Count < MaxDetalheFalhas) Falhas.Add((cd, mensagem));
        }
    }

    /// <summary>Duração por fase em segundos (ex.: "medicos", "pacientes", "atendimentos").</summary>
    public Dictionary<string, double> Tempos { get; } = [];
}
